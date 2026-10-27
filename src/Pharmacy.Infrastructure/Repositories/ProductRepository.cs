using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;
using Pharmacy.Core.DTOs;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly PharmacyDbContext _context;

        public ProductRepository(PharmacyDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(Guid id)
        {
            return await _context.Products
                .Include(p => p.Supplier)
                .Include(p => p.Batches)
                .Include(p => p.UnitConversions)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<Product>> GetAllAsync(string? search = null)
        {
            var query = _context.Products
                .Include(p => p.Supplier)
                .Include(p => p.Batches)
                .Include(p => p.UnitConversions)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(p => 
                    p.Name.ToLower().Contains(lowerSearch) || 
                    (p.ActiveIngredient != null && p.ActiveIngredient.ToLower().Contains(lowerSearch))
                );
            }

            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task AddAsync(Product product)
        {
            await _context.Products.AddAsync(product);
        }

        public void Update(Product product)
        {
            _context.Products.Update(product);
        }

        public void Delete(Product product)
        {
            _context.Products.Remove(product);
        }

        public async Task AddBatchAsync(ProductBatch batch)
        {
            await _context.ProductBatches.AddAsync(batch);
        }

        public async Task<List<ProductBatch>> GetBatchesByProductIdAsync(Guid productId)
        {
            return await _context.ProductBatches
                .Where(b => b.ProductId == productId)
                .OrderBy(b => b.ExpirationDate)
                .ToListAsync();
        }

        public async Task<ProductBatch?> GetBatchByIdAsync(Guid id)
        {
            return await _context.ProductBatches
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public void UpdateBatch(ProductBatch batch)
        {
            _context.ProductBatches.Update(batch);
        }

        public void DeleteBatch(ProductBatch batch)
        {
            _context.ProductBatches.Remove(batch);
        }

        /// <summary>
        /// Locks PostgreSQL batches in a consistent order for sales and stock restoration.
        /// </summary>
        public async Task<List<ProductBatch>> GetBatchesForUpdateAsync(
            IEnumerable<Guid> productIds,
            bool includeUnavailable = false)
        {
            var sortedIds = productIds.Distinct().OrderBy(id => id).ToArray();
            if (sortedIds.Length == 0)
            {
                return new List<ProductBatch>();
            }

            // Cancellation must also lock empty and expired batches before restoring stock.
            var parameters = sortedIds.Select((_, index) => $"{{{index}}}").ToArray();
            var inClause = string.Join(", ", parameters);
            var query = $"SELECT id, product_id, batch_number, expiration_date, current_quantity, unit_cost, created_at FROM product_batches WHERE product_id IN ({inClause})";
            var paramObjects = sortedIds.Cast<object>().ToList();

            if (!includeUnavailable)
            {
                query += $" AND current_quantity > 0 AND expiration_date >= {{{paramObjects.Count}}}";
                paramObjects.Add(DateTime.SpecifyKind(BusinessDate.Today, DateTimeKind.Utc));
            }

            query += " ORDER BY product_id, expiration_date, id FOR UPDATE";

            return await _context.ProductBatches
                .FromSqlRaw(query, paramObjects.ToArray())
                .ToListAsync();
        }

        public async Task<List<Product>> GetLowStockProductsAsync()
        {
            var today = DateTime.SpecifyKind(BusinessDate.Today, DateTimeKind.Utc);
            // Fetch products where the aggregate batch stock is below the minimum required stock level
            return await _context.Products
                .Include(p => p.Batches)
                .Where(p => p.Batches.Where(b => b.ExpirationDate >= today).Sum(b => b.CurrentQuantity) < p.MinStockLevel)
                .ToListAsync();
        }

        public async Task<List<ProductBatch>> GetExpiringBatchesAsync(int daysThreshold)
        {
            var limitDate = DateTime.UtcNow.AddDays(daysThreshold);
            return await _context.ProductBatches
                .Include(b => b.Product)
                .Where(b => b.ExpirationDate <= limitDate && b.CurrentQuantity > 0)
                .OrderBy(b => b.ExpirationDate)
                .ToListAsync();
        }

        public async Task<List<ProductHistoryDto>> GetProductHistoryAsync(Guid productId)
        {
            return await _context.OrderDetails
                .Include(d => d.Order)
                    .ThenInclude(o => o!.User)
                .Include(d => d.Order)
                    .ThenInclude(o => o!.Customer)
                .Include(d => d.Batch)
                .Where(d => d.ProductId == productId)
                .OrderByDescending(d => d.Order!.CreatedAt)
                .Select(d => new ProductHistoryDto
                {
                    OrderId = d.OrderId,
                    OrderCode = d.Order!.OrderCode,
                    SaleDate = d.Order.CreatedAt,
                    StaffName = d.Order.User != null ? d.Order.User.FullName : "Nhân viên",
                    CustomerName = d.Order.Customer != null ? d.Order.Customer.FullName : "Khách vãng lai",
                    BatchNumber = d.Batch != null ? d.Batch.BatchNumber : "Không rõ",
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    Subtotal = d.Subtotal,
                    SoldUnit = d.SoldUnit,
                    ConversionValue = d.ConversionValue
                })
                .ToListAsync();
        }

        public async Task<Guid> GetValidSupplierIdAsync(Guid supplierId)
        {
            var exists = await _context.Suppliers.AnyAsync(s => s.Id == supplierId);
            if (exists)
            {
                return supplierId;
            }

            var firstSupplier = await _context.Suppliers.FirstOrDefaultAsync();
            if (firstSupplier != null)
            {
                return firstSupplier.Id;
            }

            // Seed a default supplier if none exists
            var defaultSupplier = new Supplier
            {
                Id = Guid.NewGuid(),
                Name = "Tổng công ty Dược phẩm Trung ương I",
                ContactPerson = "Nguyễn Văn B",
                Phone = "0901234567",
                Address = "160 Tôn Đức Thắng, Đống Đa, Hà Nội",
                CreatedAt = DateTime.UtcNow
            };
            await _context.Suppliers.AddAsync(defaultSupplier);
            await _context.SaveChangesAsync();
            return defaultSupplier.Id;
        }

        public void DeleteUnitConversions(IEnumerable<ProductUnitConversion> conversions)
        {
            _context.ProductUnitConversions.RemoveRange(conversions);
        }

        public async Task AddAuditLogAsync(ProductAuditLog log)
        {
            await _context.ProductAuditLogs.AddAsync(log);
        }

        public async Task<List<ProductAuditLog>> GetAuditLogsByProductIdAsync(Guid productId)
        {
            return await _context.ProductAuditLogs
                .Where(log => log.ProductId == productId)
                .OrderByDescending(log => log.ChangedAt)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
