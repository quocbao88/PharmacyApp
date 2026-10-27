using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Exceptions;
using Pharmacy.Core.Rules;

namespace Pharmacy.Infrastructure.Services
{
    public partial class OrderService
    {
        #region Main logic

        public async Task CancelOrderAsync(Guid orderId, string reason, Guid userId)
        {
            CheckReason(reason);
            using var transaction = await _context.Database.BeginTransactionAsync();
            var order = await LockOrderAsync(orderId);
            if (order.Status != "Completed" || order.Details.Any(x => x.ReturnedQuantity != 0))
            {
                throw new PharmacyValidationException("Chỉ hủy đơn hoàn tất chưa có hàng trả. Đơn đã trả phải tiếp tục dùng Trả hàng.");
            }
            var batches = await _productRepository.GetBatchesForUpdateAsync(
                order.Details.Select(x => x.ProductId), includeUnavailable: true);
            var batchMap = batches.ToDictionary(x => x.Id);
            var customer = await ReadCustomerAsync(order.CustomerId);

            foreach (var detail in order.Details)
            {
                var quantity = checked(detail.Quantity * detail.ConversionValue);
                if (!batchMap.TryGetValue(detail.BatchId, out var batch))
                {
                    throw new PharmacyValidationException("Không tìm thấy lô hàng cần hoàn kho.");
                }
                batch.CurrentQuantity = checked(batch.CurrentQuantity + quantity);
                AddMovement(order, detail, "Cancellation", quantity,
                    -detail.Quantity * detail.CostPrice, -OrderAmounts.NetLine(order, detail), userId, reason);
            }
            if (customer != null)
            {
                customer.RewardPoints = Math.Max(0, customer.RewardPoints - (int)(order.TotalAmount / 10000));
            }
            order.Status = "Cancelled";
            order.CancelledAt = DateTime.UtcNow;
            order.CancelledBy = userId;
            order.CancellationReason = reason.Trim();
            order.RefundedAmount = order.TotalAmount;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task<OrderDto> ReturnOrderAsync(Guid orderId, ReturnOrderRequest request, Guid userId)
        {
            CheckReason(request.Reason);
            if (request.RequestId == Guid.Empty || request.Items == null || request.Items.Count == 0 ||
                request.Items.Any(x => x == null || x.Quantity <= 0) ||
                request.Items.Select(x => x.OrderDetailId).Distinct().Count() != request.Items.Count)
            {
                throw new PharmacyValidationException("Chọn chi tiết và số lượng trả hợp lệ.");
            }
            var hash = HashRequest(request);
            using var transaction = await _context.Database.BeginTransactionAsync();
            await LockRequestAsync(request.RequestId);
            var previous = await _context.OrderReturns.FirstOrDefaultAsync(x => x.RequestId == request.RequestId);
            if (previous != null)
            {
                if (previous.OrderId != orderId || previous.UserId != userId || previous.RequestHash != hash)
                {
                    throw new PharmacyValidationException("Mã trả hàng đã dùng cho yêu cầu khác.");
                }
                return await GetOrderByIdAsync(orderId);
            }
            var order = await LockOrderAsync(orderId);
            if (order.Status != "Completed" && order.Status != "PartiallyReturned")
            {
                throw new PharmacyValidationException("Đơn đã hủy hoặc đã trả hết hàng.");
            }
            var batches = await _productRepository.GetBatchesForUpdateAsync(
                order.Details.Select(x => x.ProductId), includeUnavailable: true);
            var batchMap = batches.ToDictionary(x => x.Id);
            var customer = await ReadCustomerAsync(order.CustomerId);
            var record = new OrderReturn
            {
                Id = Guid.NewGuid(),
                RequestId = request.RequestId,
                RequestHash = hash,
                OrderId = orderId,
                Order = order,
                UserId = userId,
                Reason = request.Reason.Trim()
            };
            var oldPoints = (int)((order.TotalAmount - order.RefundedAmount) / 10000);

            // Validate all requested lines before saving any stock or refund changes.
            foreach (var item in request.Items)
            {
                var detail = order.Details.FirstOrDefault(x => x.Id == item.OrderDetailId);
                if (detail == null)
                {
                    throw new PharmacyValidationException("Chi tiết trả hàng không thuộc đơn hàng này.");
                }
                var amount = OrderAmounts.Refund(order, detail, item.Quantity);
                var quantity = 0;
                decimal cost = 0;
                if (item.Restock)
                {
                    if (!batchMap.TryGetValue(detail.BatchId, out var batch))
                    {
                        throw new PharmacyValidationException("Không tìm thấy lô hàng gốc.");
                    }
                    quantity = checked(item.Quantity * detail.ConversionValue);
                    batch.CurrentQuantity = checked(batch.CurrentQuantity + quantity);
                    detail.RestockedQuantity += item.Quantity;
                    cost = -item.Quantity * detail.CostPrice;
                }
                detail.ReturnedQuantity += item.Quantity;
                record.RefundAmount += amount;
                record.Details.Add(new OrderReturnDetail
                {
                    Id = Guid.NewGuid(),
                    OrderDetailId = detail.Id,
                    Quantity = item.Quantity,
                    Restock = item.Restock,
                    RefundAmount = amount
                });
                AddMovement(order, detail, "Return", quantity, cost, -amount, userId, request.Reason);
            }
            order.RefundedAmount += record.RefundAmount;
            record.RevokedPoints = oldPoints - (int)((order.TotalAmount - order.RefundedAmount) / 10000);
            if (customer != null)
            {
                customer.RewardPoints = Math.Max(0, customer.RewardPoints - record.RevokedPoints);
            }
            order.Status = "PartiallyReturned";
            if (order.Details.All(x => x.ReturnedQuantity == x.Quantity))
            {
                order.Status = "Returned";
            }
            _context.OrderReturns.Add(record);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetOrderByIdAsync(orderId);
        }

        #endregion

        #region Helpers

        private static void CheckReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3 || reason.Length > 500)
            {
                throw new PharmacyValidationException("Nhập lý do từ 3 đến 500 ký tự.");
            }
        }

        private static string HashRequest<T>(T request)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        }

        private async Task LockRequestAsync(Guid requestId)
        {
            var key = BitConverter.ToInt64(requestId.ToByteArray(), 0);
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
        }

        private IQueryable<Order> ReadOrders()
        {
            return _context.Orders
                .Include(x => x.Details).ThenInclude(x => x.Product)
                .Include(x => x.Details).ThenInclude(x => x.Batch)
                .Include(x => x.Returns).ThenInclude(x => x.Details)
                .Include(x => x.User)
                .Include(x => x.Customer);
        }

        private async Task<Order> LockOrderAsync(Guid orderId)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM orders WHERE id = {orderId} FOR UPDATE");
            var order = await _context.Orders.Include(x => x.Details).FirstOrDefaultAsync(x => x.Id == orderId);
            if (order == null)
            {
                throw new PharmacyValidationException("Không tìm thấy đơn hàng.");
            }
            return order;
        }

        private void AddMovement(
            Order order,
            OrderDetail detail,
            string type,
            int quantity,
            decimal cost,
            decimal revenue,
            Guid userId,
            string reason)
        {
            _context.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                BatchId = detail.BatchId,
                OrderDetailId = detail.Id,
                Type = type,
                Quantity = quantity,
                StockValue = -cost,
                CostAmount = cost,
                RevenueAmount = revenue,
                UserId = userId,
                Reference = order.OrderCode,
                Reason = reason.Trim()
            });
        }

        #endregion
    }
}
