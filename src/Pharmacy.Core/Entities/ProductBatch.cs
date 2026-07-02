using System;

namespace Pharmacy.Core.Entities
{
    public class ProductBatch
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public required string BatchNumber { get; set; } // Số lô
        public DateTime ExpirationDate { get; set; } // Hạn sử dụng
        public int CurrentQuantity { get; set; } // Số lượng tồn trong lô
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Product? Product { get; set; }
    }
}
