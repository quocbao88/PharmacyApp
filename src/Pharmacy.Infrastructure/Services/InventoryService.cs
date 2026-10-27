using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Exceptions;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pharmacy.Infrastructure.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly PharmacyDbContext _context;

        public InventoryService(PharmacyDbContext context)
        {
            _context = context;
        }

        #region Main logic

        public async Task<BatchDto> ReceiveAsync(Guid productId, CreateBatchRequest request, Guid userId)
        {
            if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.BatchNumber) ||
                request.ExpirationDate == default || request.ImportPrice < 0)
            {
                throw new PharmacyValidationException("Thông tin nhập hàng không hợp lệ.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { productId, request }))));
            if (request.RequestId.HasValue)
            {
                var key = BitConverter.ToInt64(request.RequestId.Value.ToByteArray(), 0);
                await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
                var previous = await _context.StockMovements.Include(x => x.Batch).ThenInclude(x => x!.Product)
                    .FirstOrDefaultAsync(x => x.RequestId == request.RequestId);
                if (previous != null)
                {
                    if (previous.UserId != userId || previous.RequestHash != hash)
                    {
                        throw new PharmacyValidationException("Mã nhập hàng đã dùng cho yêu cầu khác.");
                    }
                    return MapBatch(previous.Batch!);
                }
            }
            // Lock the product before finding a lot, so repeated receipts use one existing lot.
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({120501}, {productId.GetHashCode()})");
            var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == productId);
            if (product == null)
            {
                throw new PharmacyValidationException("Không tìm thấy thuốc.");
            }

            var number = request.BatchNumber.Trim();
            var expiry = DateTime.SpecifyKind(request.ExpirationDate.Date, DateTimeKind.Utc);
            var batch = await _context.ProductBatches.FirstOrDefaultAsync(x =>
                x.ProductId == productId && x.BatchNumber == number && x.ExpirationDate == expiry);
            var price = request.ImportPrice ?? product.ImportPrice;
            if (batch == null)
            {
                batch = new ProductBatch
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Product = product,
                    BatchNumber = number,
                    ExpirationDate = expiry,
                    UnitCost = price
                };
                _context.ProductBatches.Add(batch);
            }
            else
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM product_batches WHERE id = {batch.Id} FOR UPDATE");
                await _context.Entry(batch).ReloadAsync();
            }

            // Maintain the remaining lot's weighted cost when the same lot is received again.
            var quantity = checked(batch.CurrentQuantity + request.Quantity);
            batch.UnitCost = decimal.Round(
                (batch.CurrentQuantity * batch.UnitCost + request.Quantity * price) / quantity,
                2, MidpointRounding.AwayFromZero);
            batch.CurrentQuantity = quantity;
            _context.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                Batch = batch,
                Type = "Receipt",
                RequestId = request.RequestId,
                RequestHash = hash,
                Quantity = request.Quantity,
                StockValue = request.Quantity * price,
                UserId = userId,
                Reference = "NH-" + Guid.NewGuid().ToString("N"),
                Reason = "Nhập hàng"
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return MapBatch(batch);
        }

        public async Task<BatchDto> AdjustAsync(Guid batchId, StockAdjustmentRequest request, Guid userId)
        {
            if (request.Quantity < 0 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3)
            {
                throw new PharmacyValidationException("Nhập số lượng thực tế và lý do điều chỉnh (ít nhất 3 ký tự).");
            }
            using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM product_batches WHERE id = {batchId} FOR UPDATE");
            var batch = await _context.ProductBatches.Include(x => x.Product).FirstOrDefaultAsync(x => x.Id == batchId);
            if (batch == null)
            {
                throw new PharmacyValidationException("Không tìm thấy lô hàng.");
            }
            if (batch.CurrentQuantity != request.ExpectedQuantity)
            {
                throw new InsufficientStockException("Tồn kho đã thay đổi. Hãy tải lại số lượng trước khi điều chỉnh.");
            }
            var difference = request.Quantity - batch.CurrentQuantity;
            if (difference == 0)
            {
                throw new PharmacyValidationException("Số lượng không thay đổi.");
            }
            _context.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                Type = "Adjustment",
                Quantity = difference,
                StockValue = difference * batch.UnitCost,
                UserId = userId,
                Reference = "DC-" + Guid.NewGuid().ToString("N"),
                Reason = request.Reason.Trim()
            });
            batch.CurrentQuantity = request.Quantity;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return MapBatch(batch);
        }

        public async Task<List<StockMovementDto>> GetHistoryAsync(Guid batchId)
        {
            return await _context.StockMovements.AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new StockMovementDto
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    Type = x.Type,
                    Quantity = x.Quantity,
                    Reference = x.Reference,
                    Reason = x.Reason
                }).ToListAsync();
        }

        public async Task<StockReportDto> GetReportAsync(DateTime start, DateTime end)
        {
            BusinessDate.Check(start, end);
            var from = BusinessDate.Start(start);
            var until = BusinessDate.Start(end.AddDays(1));
            var coverage = await _context.StockMovements.MinAsync(x => (DateTime?)x.CreatedAt);
            if (coverage.HasValue && from < BusinessDate.Start(coverage.Value.AddHours(7)))
            {
                throw new PharmacyValidationException($"Sổ kho chỉ có dữ liệu từ {coverage.Value.AddHours(7):dd/MM/yyyy}. Không suy đoán tồn kho của kỳ trước.");
            }
            var movements = await _context.StockMovements.AsNoTracking()
                .Include(x => x.Batch).ThenInclude(x => x!.Product)
                .Where(x => x.CreatedAt < until)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .ToListAsync();
            var result = new StockReportDto { CoverageStart = coverage };
            foreach (var group in movements.GroupBy(x => x.BatchId))
            {
                var batch = group.First().Batch!;
                var product = batch.Product!;
                var period = group.Where(x => x.CreatedAt >= from && x.Type != "Opening").ToList();
                var before = group.Where(x => x.CreatedAt < from || x.Type == "Opening").ToList();
                result.Items.Add(new StockReportItemDto
                {
                    ProductId = product.Id,
                    BatchId = batch.Id,
                    Name = product.Name,
                    Unit = product.Unit,
                    BatchNumber = batch.BatchNumber,
                    ExpirationDate = batch.ExpirationDate,
                    ImportPrice = batch.UnitCost,
                    SellingPrice = product.SellingPrice,
                    OpeningQuantity = before.Sum(x => x.Quantity),
                    ReceivedQuantity = period.Where(x => x.Type == "Receipt").Sum(x => x.Quantity),
                    SoldQuantity = -period.Where(x => x.Type == "Sale").Sum(x => x.Quantity),
                    ReturnedQuantity = period.Where(x => x.Type == "Return" || x.Type == "Cancellation").Sum(x => x.Quantity),
                    AdjustedQuantity = period.Where(x => x.Type == "Adjustment").Sum(x => x.Quantity),
                    ClosingQuantity = group.Sum(x => x.Quantity),
                    ReceiptAmount = period.Where(x => x.Type == "Receipt").Sum(x => x.StockValue),
                    CostAmount = period.Sum(x => x.CostAmount),
                    RevenueAmount = period.Sum(x => x.RevenueAmount),
                    ClosingValue = group.Sum(x => x.StockValue)
                });
            }
            result.Items = result.Items.OrderBy(x => x.Name).ThenBy(x => x.ExpirationDate).ToList();
            return result;
        }

        #endregion

        #region Helpers

        private static BatchDto MapBatch(ProductBatch batch)
        {
            return new BatchDto
            {
                Id = batch.Id,
                ProductId = batch.ProductId,
                ProductName = batch.Product?.Name,
                BatchNumber = batch.BatchNumber,
                ExpirationDate = batch.ExpirationDate,
                CurrentQuantity = batch.CurrentQuantity,
                ImportPrice = batch.UnitCost
            };
        }

        #endregion
    }
}
