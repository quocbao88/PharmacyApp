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
    public class OrderService : IOrderService
    {
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
            if (request.Items == null || !request.Items.Any())
            {
                throw new PharmacyValidationException("Giỏ hàng rỗng, không thể thanh toán.");
            }

            _logger.LogInformation("Nhân viên [User: {UserId}] bắt đầu tạo đơn hàng với {Count} mặt hàng.", userId, request.Items.Count);

            // Wrap in a secure database transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();

                // Pessimistic locking (SELECT FOR UPDATE) to prevent concurrent stock deduction
                var lockedBatches = await _productRepository.GetBatchesForUpdateAsync(productIds);

                var orderDetails = new List<OrderDetail>();
                decimal totalBeforeDiscount = 0;

                // FIFO stock deduction per item
                foreach (var item in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId);
                    if (product == null)
                        throw new PharmacyValidationException($"Không tìm thấy sản phẩm ID: {item.ProductId}");

                    var soldUnit = item.SoldUnit ?? product.Unit;
                    var conversionValue = 1;
                    var unitPrice = product.SellingPrice;
                    var costPrice = product.ImportPrice;

                    if (!string.IsNullOrWhiteSpace(item.SoldUnit) && !item.SoldUnit.Equals(product.Unit, StringComparison.OrdinalIgnoreCase))
                    {
                        var conversion = product.UnitConversions.FirstOrDefault(uc => uc.UnitName.Equals(item.SoldUnit, StringComparison.OrdinalIgnoreCase));
                        if (conversion != null)
                        {
                            conversionValue = conversion.ConversionValue;
                            unitPrice = conversion.SellingPrice;
                            costPrice = product.ImportPrice * conversionValue;
                        }
                    }

                    var totalBasicQtyNeeded = item.Quantity * conversionValue;

                    var batches = lockedBatches
                        .Where(b => b.ProductId == item.ProductId)
                        .OrderBy(b => b.ExpirationDate)
                        .ToList();

                    var totalAvailableQty = batches.Sum(b => b.CurrentQuantity);
                    if (totalAvailableQty < totalBasicQtyNeeded)
                    {
                        _logger.LogWarning("Không đủ tồn kho '{ProductName}': yêu cầu {Req}, khả dụng {Avail}", product.Name, totalBasicQtyNeeded, totalAvailableQty);
                        throw new InsufficientStockException(
                            $"Sản phẩm '{product.Name}' không đủ tồn kho. Yêu cầu: {totalBasicQtyNeeded} {product.Unit}, tồn thực tế: {totalAvailableQty} {product.Unit}."
                        );
                    }

                    var remainingNeeded = totalBasicQtyNeeded;
                    foreach (var batch in batches)
                    {
                        if (remainingNeeded <= 0) break;

                        var deductQty = Math.Min(remainingNeeded, batch.CurrentQuantity);
                        batch.CurrentQuantity -= deductQty;

                        _logger.LogInformation("Khấu trừ FIFO '{ProductName}' Lô:{BatchNumber} -{Qty}{Unit}", product.Name, batch.BatchNumber, deductQty, product.Unit);

                        // Determine if we can record using the selected unit (if no split or fully covers)
                        string recordedUnit = product.Unit;
                        int recordedConv = 1;
                        int recordedQty = deductQty;
                        decimal recordedUnitPrice = product.SellingPrice;
                        decimal recordedCostPrice = product.ImportPrice;

                        if (deductQty == totalBasicQtyNeeded)
                        {
                            recordedUnit = soldUnit;
                            recordedConv = conversionValue;
                            recordedQty = item.Quantity;
                            recordedUnitPrice = unitPrice;
                            recordedCostPrice = costPrice;
                        }
                        else
                        {
                            recordedUnit = product.Unit;
                            recordedConv = 1;
                            recordedQty = deductQty;
                            recordedUnitPrice = product.SellingPrice;
                            recordedCostPrice = product.ImportPrice;
                        }

                        remainingNeeded -= deductQty;

                        var subtotal = recordedQty * recordedUnitPrice;
                        totalBeforeDiscount += subtotal;

                        orderDetails.Add(new OrderDetail
                        {
                            Id = Guid.NewGuid(),
                            ProductId = product.Id,
                            BatchId = batch.Id,
                            Quantity = recordedQty,
                            UnitPrice = recordedUnitPrice,
                            CostPrice = recordedCostPrice,
                            Subtotal = subtotal,
                            SoldUnit = recordedUnit,
                            ConversionValue = recordedConv
                        });
                    }
                }

                var totalAmount = Math.Max(0, totalBeforeDiscount - request.DiscountAmount);
                var orderCode = await _orderRepository.GetNextOrderCodeAsync();

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    OrderCode = orderCode,
                    UserId = userId,
                    CustomerId = request.CustomerId,
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
                if (request.CustomerId.HasValue)
                {
                    var customer = await _customerRepository.GetByIdAsync(request.CustomerId.Value);
                    if (customer != null)
                    {
                        var pointsEarned = (int)(totalAmount / 10000);
                        customer.RewardPoints += pointsEarned;
                        _logger.LogInformation("CRM '{CustomerName}' tích +{Points} điểm thưởng.", customer.FullName, pointsEarned);
                    }
                }

                await _orderRepository.AddAsync(order);
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
                throw new PharmacyValidationException("Không tìm thấy đơn hàng.");

            return await MapToDtoAsync(order);
        }

        public async Task<OrderDto> SyncNationalAsync(Guid orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new PharmacyValidationException("Không tìm thấy đơn hàng.");

            _logger.LogInformation("Bắt đầu liên thông đơn hàng {OrderCode} lên Cổng Dược Quốc gia.", order.OrderCode);

            var syncPayload = new
            {
                NhaThuocId = "NT-000123",
                ApiKey = "SEC-KEY-999-XYZ",
                SoHoaDon = order.OrderCode,
                NgayBan = order.CreatedAt.ToString("o"),
                NguoiBan = order.User?.FullName ?? "Nhân viên hệ thống",
                KhachHang = order.Customer?.FullName ?? "Khách vãng lai",
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
                var targetUrl = "http://localhost:1033/api/national-portal-mock";
                _logger.LogInformation("Gửi request POST tới URL liên thông: {Url}", targetUrl);

                var response = await client.PostAsJsonAsync(targetUrl, syncPayload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SyncResponse>();
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

        private class SyncResponse
        {
            public bool Success { get; set; }
            public string? TransactionId { get; set; }
            public string? Message { get; set; }
        }

        public async Task<List<OrderDto>> GetRecentOrdersAsync(int limit)
        {
            var orders = await _context.Orders
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
            var query = _context.Orders
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
                var startOfDay = new DateTime(startDate.Value.Year, startDate.Value.Month, startDate.Value.Day, 0, 0, 0, 0, startDate.Value.Kind);
                var startUtc = startOfDay.ToUniversalTime();
                query = query.Where(o => o.CreatedAt >= startUtc);
            }

            if (endDate.HasValue)
            {
                // Set to end of the day (23:59:59.999) in local time/specified kind, then to UTC
                var endOfDay = new DateTime(endDate.Value.Year, endDate.Value.Month, endDate.Value.Day, 23, 59, 59, 999, endDate.Value.Kind);
                var endUtc = endOfDay.ToUniversalTime();
                query = query.Where(o => o.CreatedAt <= endUtc);
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
            var dtos = new List<OrderDto>();
            foreach (var o in orders)
            {
                dtos.Add(await MapToDtoAsync(o));
            }
            return dtos;
        }

        public async Task CancelOrderAsync(Guid orderId)
        {
            _logger.LogInformation("Bắt đầu thực hiện hủy đơn hàng ID: {OrderId}", orderId);
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Details)
                        .ThenInclude(d => d.Batch)
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    throw new PharmacyValidationException("Không tìm thấy đơn hàng cần hủy.");
                }

                if (order.Status == "Cancelled")
                {
                    throw new PharmacyValidationException("Đơn hàng này đã được hủy trước đó.");
                }

                // 1. Restore product batches stock
                foreach (var detail in order.Details)
                {
                    if (detail.Batch != null)
                    {
                        var basicQtyToRestore = detail.Quantity * detail.ConversionValue;
                        detail.Batch.CurrentQuantity += basicQtyToRestore;
                        _logger.LogInformation("Hoàn lại kho sản phẩm '{ProductId}' Lô '{BatchNumber}': +{Qty} đơn vị cơ bản", detail.ProductId, detail.Batch.BatchNumber, basicQtyToRestore);
                    }
                }

                // 2. Revert CRM reward points (10k = 1 point)
                if (order.CustomerId.HasValue && order.Customer != null)
                {
                    var pointsEarned = (int)(order.TotalAmount / 10000);
                    order.Customer.RewardPoints = Math.Max(0, order.Customer.RewardPoints - pointsEarned);
                    _logger.LogInformation("Khách hàng '{CustomerName}' bị trừ {Points} điểm tích lũy do hủy đơn hàng.", order.Customer.FullName, pointsEarned);
                }

                // 3. Mark order status as Cancelled
                order.Status = "Cancelled";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Hủy đơn hàng {OrderCode} THÀNH CÔNG và đã hoàn lại kho thuốc.", order.OrderCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi hủy đơn hàng ID: {OrderId} - Thực hiện ROLLBACK.", orderId);
                await transaction.RollbackAsync();
                throw;
            }
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
                    UnitPrice = d.UnitPrice,
                    CostPrice = d.CostPrice,
                    Subtotal = d.Subtotal,
                    SoldUnit = d.SoldUnit,
                    ConversionValue = d.ConversionValue
                }).ToList()
            };
            return Task.FromResult(dto);
        }
    }
}
