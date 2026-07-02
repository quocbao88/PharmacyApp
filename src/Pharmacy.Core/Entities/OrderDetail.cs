using System;

namespace Pharmacy.Core.Entities
{
    public class OrderDetail
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public Guid BatchId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Giá bán tại thời điểm mua
        public decimal CostPrice { get; set; } // Giá nhập tại thời điểm bán (để tính lợi nhuận)
        public decimal Subtotal { get; set; } // Quantity * UnitPrice

        // Unit conversion tracking for this sale line
        public string? SoldUnit { get; set; } // e.g. Hộp, Vỉ, Viên
        public int ConversionValue { get; set; } = 1; // Default is 1

        // Navigation
        public Order? Order { get; set; }
        public Product? Product { get; set; }
        public ProductBatch? Batch { get; set; }
    }
}
