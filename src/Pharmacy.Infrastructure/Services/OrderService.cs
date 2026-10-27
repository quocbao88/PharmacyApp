using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Exceptions;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Services
{
    public partial class OrderService : IOrderService
    {
        #region Main logic

        private readonly PharmacyDbContext _context;
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<OrderService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public OrderService(
            PharmacyDbContext context,
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            ICustomerRepository customerRepository,
            ILogger<OrderService> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _context = context;
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<OrderDto> CheckoutAsync(Guid userId, CheckoutOrderRequest request)
        {
            CheckCheckout(request);

            _logger.LogInformation("Nhân viên [User: {UserId}] bắt đầu tạo đơn hàng với {Count} mặt hàng.", userId, request.Items.Count);

            // Wrap in a secure database transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var requestHash = HashRequest(request);
                if (request.RequestId.HasValue)
                {
                    await LockRequestAsync(request.RequestId.Value);
                    var existing = await ReadOrders().FirstOrDefaultAsync(x => x.RequestId == request.RequestId);
                    if (existing != null)
                    {
                        if (existing.UserId != userId || existing.RequestHash != requestHash)
                        {
                            throw new PharmacyValidationException("Mã thanh toán đã được dùng cho nội dung khác.");
                        }
                        return await MapToDtoAsync(existing);
                    }
                }

                var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();

                // Pessimistic locking (SELECT FOR UPDATE) to prevent concurrent stock deduction
                var lockedBatches = await _productRepository.GetBatchesForUpdateAsync(productIds);
                var customer = await ReadCustomerAsync(request.CustomerId);

                var orderDetails = new List<OrderDetail>();
                decimal totalBeforeDiscount = 0;

                // Validate and prepare every line before saving any database changes.
                foreach (var item in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId);
                    if (product == null)
                    {
                        throw new PharmacyValidationException($"Không tìm thấy sản phẩm ID: {item.ProductId}");
                    }

                    var details = PrepareDetails(product, item, lockedBatches);
                    orderDetails.AddRange(details);
                    totalBeforeDiscount += details.Sum(d => d.Subtotal);
                }

                if (request.DiscountAmount > totalBeforeDiscount)
                {
                    throw new PharmacyValidationException("Giảm giá không được vượt quá tổng tiền hàng.");
                }

                var totalAmount = totalBeforeDiscount - request.DiscountAmount;
                var orderCode = await _orderRepository.GetNextOrderCodeAsync();

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    OrderCode = orderCode,
                    UserId = userId,
                    RequestId = request.RequestId,
                    RequestHash = requestHash,
                    CustomerId = request.CustomerId,
                    Customer = customer,
                    // Khách vãng lai
                    GuestName = !request.CustomerId.HasValue ? request.GuestName : null,
                    GuestDateOfBirth = !request.CustomerId.HasValue ? request.GuestDateOfBirth : null,
                    Notes = request.Notes,
                    CreatedAt = DateTime.UtcNow,
                    TotalAmount = totalAmount,
                    DiscountAmount = request.DiscountAmount,
                    PaymentMethod = request.PaymentMethod,
                    Details = orderDetails,
                    PrescriptionCode = request.PrescriptionCode,
                    PrescribingDoctor = request.PrescribingDoctor,
                    MedicalFacility = request.MedicalFacility,
                    Diagnostic = request.Diagnostic,
                    NationalSyncStatus = "Pending"
                };

                // CRM reward points: 10,000 VND = 1 điểm
                if (customer != null)
                {
                    var pointsEarned = (int)(totalAmount / 10000);
                    customer.RewardPoints = checked(customer.RewardPoints + pointsEarned);
                }

                await _orderRepository.AddAsync(order);
                foreach (var detail in order.Details)
                {
                    AddMovement(order, detail, "Sale", -checked(detail.Quantity * detail.ConversionValue),
                        detail.Quantity * detail.CostPrice, Pharmacy.Core.Rules.OrderAmounts.NetLine(order, detail),
                        userId, "Bán hàng");
                }
                await _orderRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Đơn hàng {OrderCode} THÀNH CÔNG - Tổng: {TotalAmount}đ [{PaymentMethod}]", orderCode, totalAmount, request.PaymentMethod);

                return await MapToDtoAsync(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi thanh toán - ROLLBACK giao dịch.");
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<OrderDto> GetOrderByIdAsync(Guid id)
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                throw new PharmacyValidationException("Không tìm thấy đơn hàng.");
            }

            return await MapToDtoAsync(order);
        }

        public async Task<OrderDto> SyncNationalAsync(Guid orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                throw new PharmacyValidationException("Không tìm thấy đơn hàng.");
            }

            if (order.Status == "Cancelled")
            {
                throw new PharmacyValidationException("Không thể liên thông đơn hàng đã hủy.");
            }

            _logger.LogInformation("Bắt đầu liên thông đơn hàng {OrderCode} lên Cổng Dược Quốc gia.", order.OrderCode);

            var syncPayload = new
            {
                NhaThuocId = "NT-000123",
                ApiKey = "SEC-KEY-999-XYZ",
                SoHoaDon = order.OrderCode,
                NgayBan = order.CreatedAt.ToString("o"),
                NguoiBan = order.User?.FullName ?? "Nhân viên hệ thống",
                KhachHang = order.Customer?.FullName ?? order.GuestName ?? "Khách vãng lai",
                SoDienThoai = order.Customer?.Phone ?? "",
                TongTien = order.TotalAmount,
                GiamGia = order.DiscountAmount,
                PhuongThucThanhToan = order.PaymentMethod,
                ChanDoan = order.Diagnostic ?? "",
                BacSi = order.PrescribingDoctor ?? "",
                MaDonThuoc = order.PrescriptionCode ?? "",
                ChiTiet = order.Details.Select(d => new
                {
                    MaThuoc = d.Product?.Id.ToString() ?? "",
                    TenThuoc = d.Product?.Name ?? "",
                    DonViTinh = d.SoldUnit ?? d.Product?.Unit ?? "",
                    SoLo = d.Batch?.BatchNumber ?? "",
                    SoLuong = d.Quantity,
                    DonGia = d.UnitPrice,
                    ThanhTien = d.Subtotal
                }).ToList()
            };

            try
            {
                var client = _httpClientFactory.CreateClient();
                var targetUrl = _configuration["NationalPortal:Url"] ?? "http://localhost:1033/api/national-portal-mock";
                _logger.LogInformation("Gửi request POST tới URL liên thông: {Url}", targetUrl);

                using var response = await client.PostAsJsonAsync(targetUrl, syncPayload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SyncResponse>();
                    if (result == null || !result.Success)
                    {
                        order.NationalSyncStatus = "Failed";
                        order.NationalSyncMessage = result?.Message ?? "Cổng liên thông không xác nhận thành công.";
                        order.NationalSyncedAt = DateTime.UtcNow;
                        await _orderRepository.SaveChangesAsync();
                        return await MapToDtoAsync(order);
                    }

                    order.NationalSyncStatus = "Synced";
                    order.NationalSyncMessage = $"Liên thông thành công. Mã GD: {result?.TransactionId}. {result?.Message}";
                    order.NationalSyncedAt = DateTime.UtcNow;
                    _logger.LogInformation("Đơn hàng {OrderCode} liên thông thành công. Mã GD: {TxId}", order.OrderCode, result?.TransactionId);
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    order.NationalSyncStatus = "Failed";
                    order.NationalSyncMessage = $"Cổng Dược Quốc gia từ chối (HTTP {response.StatusCode}): {errorMsg}";
                    order.NationalSyncedAt = DateTime.UtcNow;
                    _logger.LogWarning("Đơn hàng {OrderCode} liên thông thất bại. HTTP {Status} - {Error}", order.OrderCode, response.StatusCode, errorMsg);
                }
            }
            catch (Exception ex)
            {
                order.NationalSyncStatus = "Failed";
                order.NationalSyncMessage = $"Lỗi kết nối cổng liên thông: {ex.Message}";
                order.NationalSyncedAt = DateTime.UtcNow;
                _logger.LogError(ex, "Lỗi kết nối khi liên thông đơn hàng {OrderCode}", order.OrderCode);
            }

            await _orderRepository.SaveChangesAsync();
            return await MapToDtoAsync(order);
        }

        public async Task<List<OrderDto>> GetRecentOrdersAsync(int limit)
        {
            if (limit <= 0)
            {
                throw new PharmacyValidationException("Số lượng đơn hàng phải lớn hơn 0.");
            }

            var orders = await _context.Orders
                .Include(o => o.Returns).ThenInclude(x => x.Details)
                .Include(o => o.User)
                .Include(o => o.Customer)
                .Include(o => o.Details)
                    .ThenInclude(d => d.Product)
                .Include(o => o.Details)
                    .ThenInclude(d => d.Batch)
                .OrderByDescending(o => o.CreatedAt)
                .Take(limit)
                .ToListAsync();

            var dtos = new List<OrderDto>();
            foreach (var o in orders)
            {
                dtos.Add(await MapToDtoAsync(o));
            }
            return dtos;
        }

        public async Task<List<OrderDto>> GetOrdersAsync(DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue && startDate.Value.Date == DateTime.MinValue.Date
                || endDate.HasValue && (endDate.Value.Date == DateTime.MinValue.Date || endDate.Value.Date == DateTime.MaxValue.Date))
            {
                throw new PharmacyValidationException("Khoảng ngày không hợp lệ.");
            }

            if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
            {
                throw new PharmacyValidationException("Ngày bắt đầu không được sau ngày kết thúc.");
            }

            var query = _context.Orders
                .Include(o => o.Returns).ThenInclude(x => x.Details)
                .Include(o => o.User)
                .Include(o => o.Customer)
                .Include(o => o.Details)
                    .ThenInclude(d => d.Product)
                .Include(o => o.Details)
                    .ThenInclude(d => d.Batch)
                .AsQueryable();

            if (startDate.HasValue)
            {
                // Set to start of the day in local time/specified kind, then to UTC
                var startUtc = BusinessDate.Start(startDate.Value);
                query = query.Where(o => o.CreatedAt >= startUtc);
            }

            if (endDate.HasValue)
            {
                // Set to end of the day (23:59:59.999) in local time/specified kind, then to UTC
                var endUtc = BusinessDate.Start(endDate.Value.AddDays(1));
                query = query.Where(o => o.CreatedAt < endUtc);
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
            var dtos = new List<OrderDto>();
            foreach (var o in orders)
            {
                dtos.Add(await MapToDtoAsync(o));
            }
            return dtos;
        }

        #endregion

        #region Helpers

        private static void CheckCheckout(CheckoutOrderRequest request)
        {
            if (request.Items == null || request.Items.Count == 0)
            {
                throw new PharmacyValidationException("Giỏ hàng rỗng, không thể thanh toán.");
            }

            if (request.Items.Any(i => i == null || i.ProductId == Guid.Empty || i.Quantity <= 0))
            {
                throw new PharmacyValidationException("Sản phẩm không hợp lệ hoặc số lượng phải lớn hơn 0.");
            }

            if (request.DiscountAmount < 0)
            {
                throw new PharmacyValidationException("Giảm giá không được âm.");
            }

            if (request.PaymentMethod != "Cash" && request.PaymentMethod != "Transfer")
            {
                throw new PharmacyValidationException("Hình thức thanh toán không hợp lệ.");
            }
        }

        private async Task<Customer?> ReadCustomerAsync(Guid? customerId)
        {
            if (!customerId.HasValue)
            {
                return null;
            }

            // Serialize reward updates before reading the balance to avoid lost points.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM customers WHERE id = {customerId.Value} FOR UPDATE");

            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer == null)
            {
                throw new PharmacyValidationException("Không tìm thấy khách hàng.");
            }

            return customer;
        }

        private static List<OrderDetail> PrepareDetails(
            Product product,
            CheckoutItemDto item,
            List<ProductBatch> lockedBatches)
        {
            var soldUnit = product.Unit;
            var conversionValue = 1;
            var unitPrice = product.SellingPrice;

            if (!string.IsNullOrWhiteSpace(item.SoldUnit) &&
                !item.SoldUnit.Equals(product.Unit, StringComparison.OrdinalIgnoreCase))
            {
                var conversion = product.UnitConversions.FirstOrDefault(
                    u => u.UnitName.Equals(item.SoldUnit, StringComparison.OrdinalIgnoreCase));
                if (conversion == null)
                {
                    throw new PharmacyValidationException($"Đơn vị bán của '{product.Name}' không hợp lệ.");
                }

                soldUnit = conversion.UnitName;
                conversionValue = conversion.ConversionValue;
                unitPrice = conversion.SellingPrice;
            }

            if (conversionValue <= 0 || unitPrice < 0 || product.ImportPrice < 0 ||
                item.Quantity > int.MaxValue / conversionValue)
            {
                throw new PharmacyValidationException("Số lượng, giá hoặc hệ số quy đổi không hợp lệ.");
            }

            var totalBasicQuantity = item.Quantity * conversionValue;
            var batches = lockedBatches
                .Where(b => b.ProductId == product.Id && b.CurrentQuantity > 0 &&
                    b.ExpirationDate >= DateTime.SpecifyKind(BusinessDate.Today, DateTimeKind.Utc))
                .OrderBy(b => b.ExpirationDate)
                .ThenBy(b => b.Id)
                .ToList();

            if (batches.Sum(b => (long)b.CurrentQuantity) < totalBasicQuantity)
            {
                throw new InsufficientStockException($"Sản phẩm '{product.Name}' không đủ tồn kho còn hạn sử dụng.");
            }

            var details = new List<OrderDetail>();
            var remainingQuantity = totalBasicQuantity;
            var lineTotal = item.Quantity * unitPrice;
            var remainingAmount = lineTotal;

            foreach (var batch in batches)
            {
                if (remainingQuantity == 0)
                {
                    break;
                }

                var quantity = Math.Min(remainingQuantity, batch.CurrentQuantity);
                var detail = new OrderDetail
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Product = product,
                    BatchId = batch.Id,
                    Batch = batch,
                    Quantity = quantity,
                    UnitPrice = decimal.Round(unitPrice / conversionValue, 2, MidpointRounding.AwayFromZero),
                    CostPrice = batch.UnitCost,
                    SoldUnit = product.Unit,
                    ConversionValue = 1
                };

                if (quantity == totalBasicQuantity)
                {
                    detail.Quantity = item.Quantity;
                    detail.UnitPrice = unitPrice;
                    detail.CostPrice = batch.UnitCost * conversionValue;
                    detail.SoldUnit = soldUnit;
                    detail.ConversionValue = conversionValue;
                }

                // Split at the selected unit's price, assigning the rounding remainder to the last batch.
                var allocatedQuantity = totalBasicQuantity - remainingQuantity + quantity;
                var allocatedAmount = decimal.Round(lineTotal * allocatedQuantity / totalBasicQuantity, 2, MidpointRounding.AwayFromZero);
                var subtotal = allocatedAmount - (lineTotal - remainingAmount);
                if (quantity == remainingQuantity)
                {
                    subtotal = remainingAmount;
                }

                detail.Subtotal = subtotal;
                remainingAmount -= subtotal;
                remainingQuantity -= quantity;
                batch.CurrentQuantity -= quantity;
                details.Add(detail);
            }

            return details;
        }

        private Task<OrderDto> MapToDtoAsync(Order order)
        {
            var dto = new OrderDto
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                UserId = order.UserId,
                StaffName = order.User?.FullName,
                CustomerId = order.CustomerId,
                CustomerName = order.Customer?.FullName,
                CustomerPhone = order.Customer?.Phone,
                GuestName = order.GuestName,
                GuestDateOfBirth = order.GuestDateOfBirth,
                Notes = order.Notes,
                CreatedAt = order.CreatedAt,
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                PaymentMethod = order.PaymentMethod,
                Status = order.Status,
                RefundedAmount = order.RefundedAmount,
                CancellationReason = order.CancellationReason,
                CancelledAt = order.CancelledAt,
                Returns = order.Returns.OrderByDescending(x => x.CreatedAt).Select(x => new OrderReturnDto
                {
                    CreatedAt = x.CreatedAt,
                    Reason = x.Reason,
                    RefundAmount = x.RefundAmount,
                    Details = x.Details.Select(d => new OrderReturnItemDto
                    {
                        OrderDetailId = d.OrderDetailId,
                        Quantity = d.Quantity,
                        Restock = d.Restock,
                        RefundAmount = d.RefundAmount
                    }).ToList()
                }).ToList(),
                NationalSyncStatus = order.NationalSyncStatus,
                NationalSyncMessage = order.NationalSyncMessage,
                NationalSyncedAt = order.NationalSyncedAt,
                PrescriptionCode = order.PrescriptionCode,
                PrescribingDoctor = order.PrescribingDoctor,
                MedicalFacility = order.MedicalFacility,
                Diagnostic = order.Diagnostic,
                Details = order.Details.Select(d => new OrderDetailDto
                {
                    Id = d.Id,
                    ProductId = d.ProductId,
                    ProductName = d.Product?.Name ?? "Sản phẩm",
                    Unit = d.Product?.Unit ?? "Đơn vị",
                    BatchId = d.BatchId,
                    BatchNumber = d.Batch?.BatchNumber ?? "Lô",
                    Quantity = d.Quantity,
                    ReturnedQuantity = d.ReturnedQuantity,
                    RestockedQuantity = d.RestockedQuantity,
                    UnitPrice = d.UnitPrice,
                    CostPrice = d.CostPrice,
                    Subtotal = d.Subtotal,
                    SoldUnit = d.SoldUnit,
                    ConversionValue = d.ConversionValue
                }).ToList()
            };
            return Task.FromResult(dto);
        }

        #endregion

        #region Models

        private class SyncResponse
        {
            public bool Success { get; set; }
            public string? TransactionId { get; set; }
            public string? Message { get; set; }
        }

        #endregion
    }
}
