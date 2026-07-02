using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductRepository _productRepository;

        public ProductsController(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? category)
        {
            var products = await _productRepository.GetAllAsync(search);
            if (!string.IsNullOrWhiteSpace(category))
                products = products.Where(p => p.Category == category).ToList();

            return Ok(products.Select(MapToDto));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { error = "Không tìm thấy sản phẩm." });
            return Ok(MapToDto(product));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
        {
            var supplierId = await _productRepository.GetValidSupplierIdAsync(request.SupplierId);
            var product = new Product
            {
                Id = Guid.NewGuid(),
                SupplierId = supplierId,
                Name = request.Name,
                ActiveIngredient = request.ActiveIngredient,
                Category = request.Category,
                Manufacturer = request.Manufacturer,
                DosageForm = request.DosageForm,
                Strength = request.Strength,
                StorageConditions = request.StorageConditions,
                PrescriptionRequired = request.PrescriptionRequired,
                Description = request.Description,
                Unit = request.Unit,
                ImportPrice = request.ImportPrice,
                SellingPrice = request.SellingPrice,
                MinStockLevel = request.MinStockLevel,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (request.UnitConversions != null && request.UnitConversions.Any())
            {
                foreach (var uc in request.UnitConversions)
                {
                    product.UnitConversions.Add(new ProductUnitConversion
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        UnitName = uc.UnitName,
                        ConversionValue = uc.ConversionValue,
                        SellingPrice = uc.SellingPrice
                    });
                }
            }

            await _productRepository.AddAsync(product);
            
            // Log audit
            var userFullName = User.Identity?.Name ?? "Hệ thống";
            var auditLog = new ProductAuditLog
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                Action = "Thêm mới",
                ChangedBy = userFullName,
                ChangedAt = DateTime.UtcNow,
                Details = $"Thêm mới thuốc: {product.Name}, nhóm: {product.Category}, dạng: {product.DosageForm}, giá nhập: {product.ImportPrice:N0}đ, giá bán: {product.SellingPrice:N0}đ, đơn vị: {product.Unit}."
            };
            await _productRepository.AddAuditLogAsync(auditLog);

            await _productRepository.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = product.Id }, MapToDto(product));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateProductRequest request)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { error = "Không tìm thấy sản phẩm." });

            var detailsList = new List<string>();
            if (product.Name != request.Name) detailsList.Add($"Tên: '{product.Name}' -> '{request.Name}'");
            if (product.Category != request.Category) detailsList.Add($"Nhóm: '{product.Category}' -> '{request.Category}'");
            if (product.DosageForm != request.DosageForm) detailsList.Add($"Dạng bào chế: '{product.DosageForm}' -> '{request.DosageForm}'");
            if (product.Strength != request.Strength) detailsList.Add($"Hàm lượng: '{product.Strength}' -> '{request.Strength}'");
            if (product.Unit != request.Unit) detailsList.Add($"Đơn vị: '{product.Unit}' -> '{request.Unit}'");
            if (product.ImportPrice != request.ImportPrice) detailsList.Add($"Giá nhập: {product.ImportPrice:N0}đ -> {request.ImportPrice:N0}đ");
            if (product.SellingPrice != request.SellingPrice) detailsList.Add($"Giá bán: {product.SellingPrice:N0}đ -> {request.SellingPrice:N0}đ");
            if (product.MinStockLevel != request.MinStockLevel) detailsList.Add($"Tồn tối thiểu: {product.MinStockLevel} -> {request.MinStockLevel}");
            
            var details = detailsList.Count > 0 ? string.Join(", ", detailsList) : "Cập nhật thông tin chi tiết (không đổi thuộc tính chính).";

            var supplierId = await _productRepository.GetValidSupplierIdAsync(request.SupplierId);
            product.SupplierId = supplierId;
            product.Name = request.Name;
            product.ActiveIngredient = request.ActiveIngredient;
            product.Category = request.Category;
            product.Manufacturer = request.Manufacturer;
            product.DosageForm = request.DosageForm;
            product.Strength = request.Strength;
            product.StorageConditions = request.StorageConditions;
            product.PrescriptionRequired = request.PrescriptionRequired;
            product.Description = request.Description;
            product.Unit = request.Unit;
            product.ImportPrice = request.ImportPrice;
            product.SellingPrice = request.SellingPrice;
            product.MinStockLevel = request.MinStockLevel;
            product.UpdatedAt = DateTime.UtcNow;

            // Update Unit Conversions
            if (product.UnitConversions != null)
            {
                var existingConversions = product.UnitConversions.ToList();
                if (existingConversions.Any())
                {
                    _productRepository.DeleteUnitConversions(existingConversions);
                    foreach (var uc in existingConversions)
                    {
                        product.UnitConversions.Remove(uc);
                    }
                }
            }

            if (request.UnitConversions != null)
            {
                foreach (var uc in request.UnitConversions)
                {
                    product.UnitConversions.Add(new ProductUnitConversion
                    {
                        Id = Guid.Empty,
                        ProductId = product.Id,
                        UnitName = uc.UnitName,
                        ConversionValue = uc.ConversionValue,
                        SellingPrice = uc.SellingPrice
                    });
                }
            }

            // Log audit
            var userFullName = User.Identity?.Name ?? "Hệ thống";
            var auditLog = new ProductAuditLog
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                Action = "Cập nhật",
                ChangedBy = userFullName,
                ChangedAt = DateTime.UtcNow,
                Details = details
            };
            await _productRepository.AddAuditLogAsync(auditLog);

            await _productRepository.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { error = "Không tìm thấy sản phẩm." });

            _productRepository.Delete(product);

            // Log audit
            var userFullName = User.Identity?.Name ?? "Hệ thống";
            var auditLog = new ProductAuditLog
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                Action = "Xóa",
                ChangedBy = userFullName,
                ChangedAt = DateTime.UtcNow,
                Details = $"Xóa thuốc: {product.Name} khỏi danh mục."
            };
            await _productRepository.AddAuditLogAsync(auditLog);

            await _productRepository.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/batches")]
        public async Task<IActionResult> AddBatch(Guid id, [FromBody] CreateBatchRequest request)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { error = "Không tìm thấy sản phẩm." });

            var batch = new ProductBatch
            {
                Id = Guid.NewGuid(),
                ProductId = id,
                BatchNumber = request.BatchNumber,
                ExpirationDate = request.ExpirationDate.ToUniversalTime(),
                CurrentQuantity = request.Quantity,
                CreatedAt = DateTime.UtcNow
            };

            await _productRepository.AddBatchAsync(batch);

            // Log audit
            var userFullName = User.Identity?.Name ?? "Hệ thống";
            var auditLog = new ProductAuditLog
            {
                Id = Guid.NewGuid(),
                ProductId = id,
                ProductName = product.Name,
                Action = "Nhập lô",
                ChangedBy = userFullName,
                ChangedAt = DateTime.UtcNow,
                Details = $"Nhập lô mới: {request.BatchNumber}, Số lượng: {request.Quantity}, Hạn dùng: {request.ExpirationDate:dd/MM/yyyy}."
            };
            await _productRepository.AddAuditLogAsync(auditLog);

            await _productRepository.SaveChangesAsync();

            return Ok(new BatchDto
            {
                Id = batch.Id,
                ProductId = batch.ProductId,
                ProductName = product.Name,
                BatchNumber = batch.BatchNumber,
                ExpirationDate = batch.ExpirationDate,
                CurrentQuantity = batch.CurrentQuantity
            });
        }

        [HttpGet("batches/{productId}")]
        public async Task<IActionResult> GetBatches(Guid productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null) return NotFound(new { error = "Không tìm thấy sản phẩm." });

            var batches = product.Batches.Select(b => new BatchDto
            {
                Id = b.Id,
                ProductId = b.ProductId,
                ProductName = product.Name,
                BatchNumber = b.BatchNumber,
                ExpirationDate = b.ExpirationDate,
                CurrentQuantity = b.CurrentQuantity
            }).OrderBy(b => b.ExpirationDate).ToList();

            return Ok(batches);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("batches/{batchId}")]
        public async Task<IActionResult> UpdateBatch(Guid batchId, [FromBody] UpdateBatchRequest request)
        {
            var batch = await _productRepository.GetBatchByIdAsync(batchId);
            if (batch == null) return NotFound(new { error = "Không tìm thấy lô hàng." });

            var oldNumber = batch.BatchNumber;
            var oldQty = batch.CurrentQuantity;
            var oldExp = batch.ExpirationDate;

            batch.BatchNumber = request.BatchNumber;
            batch.ExpirationDate = request.ExpirationDate.ToUniversalTime();
            batch.CurrentQuantity = request.CurrentQuantity;

            _productRepository.UpdateBatch(batch);

            // Log audit
            var userFullName = User.Identity?.Name ?? "Hệ thống";
            var auditLog = new ProductAuditLog
            {
                Id = Guid.NewGuid(),
                ProductId = batch.ProductId,
                ProductName = batch.Product?.Name ?? "Sản phẩm",
                Action = "Cập nhật lô",
                ChangedBy = userFullName,
                ChangedAt = DateTime.UtcNow,
                Details = $"Cập nhật lô {oldNumber} -> {request.BatchNumber}. Số lượng: {oldQty} -> {request.CurrentQuantity}. Hạn dùng: {oldExp:dd/MM/yyyy} -> {request.ExpirationDate:dd/MM/yyyy}."
            };
            await _productRepository.AddAuditLogAsync(auditLog);

            await _productRepository.SaveChangesAsync();

            return Ok(new BatchDto
            {
                Id = batch.Id,
                ProductId = batch.ProductId,
                ProductName = batch.Product?.Name,
                BatchNumber = batch.BatchNumber,
                ExpirationDate = batch.ExpirationDate,
                CurrentQuantity = batch.CurrentQuantity
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("batches/{batchId}")]
        public async Task<IActionResult> DeleteBatch(Guid batchId)
        {
            var batch = await _productRepository.GetBatchByIdAsync(batchId);
            if (batch == null) return NotFound(new { error = "Không tìm thấy lô hàng." });

            _productRepository.DeleteBatch(batch);

            // Log audit
            var userFullName = User.Identity?.Name ?? "Hệ thống";
            var auditLog = new ProductAuditLog
            {
                Id = Guid.NewGuid(),
                ProductId = batch.ProductId,
                ProductName = batch.Product?.Name ?? "Sản phẩm",
                Action = "Xóa lô",
                ChangedBy = userFullName,
                ChangedAt = DateTime.UtcNow,
                Details = $"Xóa lô: {batch.BatchNumber}, Số lượng còn lại khi xóa: {batch.CurrentQuantity}."
            };
            await _productRepository.AddAuditLogAsync(auditLog);

            await _productRepository.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetProductHistory(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { error = "Không tìm thấy sản phẩm." });

            var history = await _productRepository.GetProductHistoryAsync(id);
            return Ok(history);
        }

        [HttpGet("{id}/audit-logs")]
        public async Task<IActionResult> GetAuditLogs(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { error = "Không tìm thấy sản phẩm." });

            var logs = await _productRepository.GetAuditLogsByProductIdAsync(id);
            return Ok(logs);
        }

        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
        {
            var lowStockProducts = await _productRepository.GetLowStockProductsAsync();
            var expiringBatches = await _productRepository.GetExpiringBatchesAsync(90);

            return Ok(new SmartAlertsResponse
            {
                LowStockProducts = lowStockProducts.Select(p => new LowStockAlertDto
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    CurrentStock = p.Batches.Sum(b => b.CurrentQuantity),
                    MinStockLevel = p.MinStockLevel,
                    Unit = p.Unit
                }).ToList(),
                ExpiringBatches = expiringBatches.Select(b => new ExpiringBatchAlertDto
                {
                    ProductId = b.ProductId,
                    ProductName = b.Product?.Name ?? "Sản phẩm",
                    BatchId = b.Id,
                    BatchNumber = b.BatchNumber,
                    ExpirationDate = b.ExpirationDate,
                    DaysRemaining = (b.ExpirationDate - DateTime.UtcNow).Days,
                    CurrentQuantity = b.CurrentQuantity
                }).ToList()
            });
        }

        private static ProductDto MapToDto(Product p) => new()
        {
            Id = p.Id,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier?.Name,
            Name = p.Name,
            ActiveIngredient = p.ActiveIngredient,
            Category = p.Category,
            Manufacturer = p.Manufacturer,
            DosageForm = p.DosageForm,
            Strength = p.Strength,
            StorageConditions = p.StorageConditions,
            PrescriptionRequired = p.PrescriptionRequired,
            Description = p.Description,
            Unit = p.Unit,
            ImportPrice = p.ImportPrice,
            SellingPrice = p.SellingPrice,
            MinStockLevel = p.MinStockLevel,
            TotalStock = p.Batches.Sum(b => b.CurrentQuantity),
            UpdatedAt = p.UpdatedAt,
            UnitConversions = p.UnitConversions.Select(uc => new ProductUnitConversionDto
            {
                Id = uc.Id,
                UnitName = uc.UnitName,
                ConversionValue = uc.ConversionValue,
                SellingPrice = uc.SellingPrice
            }).ToList()
        };
    }
}
