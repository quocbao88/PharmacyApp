using System;

namespace Pharmacy.Core.Entities
{
    public class ProductUnitConversion
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public required string UnitName { get; set; }        // Tên đơn vị quy đổi (e.g. Hộp, Vỉ)
        public int ConversionValue { get; set; }             // Hệ số quy đổi ra đơn vị cơ bản (e.g. Hộp = 30 Viên)
        public decimal SellingPrice { get; set; }            // Giá bán cho đơn vị quy đổi này

        // Navigation
        public Product? Product { get; set; }
    }
}
