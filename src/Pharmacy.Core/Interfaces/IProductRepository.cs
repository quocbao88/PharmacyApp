using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pharmacy.Core.Entities;
using Pharmacy.Core.DTOs;

namespace Pharmacy.Core.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid id);
        Task<List<Product>> GetAllAsync(string? search = null);
        Task AddAsync(Product product);
        void Update(Product product);
        void Delete(Product product);
        
        // Batch operations
        Task AddBatchAsync(ProductBatch batch);
        Task<List<ProductBatch>> GetBatchesByProductIdAsync(Guid productId);
        Task<ProductBatch?> GetBatchByIdAsync(Guid id);
        void UpdateBatch(ProductBatch batch);
        void DeleteBatch(ProductBatch batch);
        
        // Locking batches to prevent deadlocks and resolve concurrency
        Task<List<ProductBatch>> GetBatchesForUpdateAsync(IEnumerable<Guid> productIds);
        
        // Smart alerts
        Task<List<Product>> GetLowStockProductsAsync();
        Task<List<ProductBatch>> GetExpiringBatchesAsync(int daysThreshold);

        // Product sale history
        Task<List<ProductHistoryDto>> GetProductHistoryAsync(Guid productId);

        Task<Guid> GetValidSupplierIdAsync(Guid supplierId);

        void DeleteUnitConversions(IEnumerable<ProductUnitConversion> conversions);

        // Product modification audit logs
        Task AddAuditLogAsync(ProductAuditLog log);
        Task<List<ProductAuditLog>> GetAuditLogsByProductIdAsync(Guid productId);

        Task SaveChangesAsync();
    }
}
