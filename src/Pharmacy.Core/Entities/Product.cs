using System;
using System.Collections.Generic;

namespace Pharmacy.Core.Entities
{
    public class Product
    {
        public Guid Id { get; set; }
        public Guid SupplierId { get; set; }
        public required string Name { get; set; }           // Tên thuốc
        public string? ActiveIngredient { get; set; }       // Hoạt chất
        public string? Category { get; set; }               // Nhóm thuốc (Kháng sinh, Giảm đau, Tim mạch...)
        public string? Manufacturer { get; set; }           // Hãng sản xuất
        public string? DosageForm { get; set; }             // Dạng bào chế (Viên nén, Siro, Kem bôi...)
        public string? Strength { get; set; }               // Hàm lượng / Nồng độ (500mg, 10mg/5ml...)
        public string? StorageConditions { get; set; }      // Điều kiện bảo quản
        public bool PrescriptionRequired { get; set; }      // Thuốc kê đơn
        public string? Description { get; set; }            // Mô tả, chỉ định, chống chỉ định (rich text / textarea)
        public required string Unit { get; set; }           // Đơn vị tính
        public decimal ImportPrice { get; set; }            // Giá nhập
        public decimal SellingPrice { get; set; }           // Giá bán
        public int MinStockLevel { get; set; }              // Mức cảnh báo tồn kho tối thiểu
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Supplier? Supplier { get; set; }
        public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();
        public ICollection<ProductUnitConversion> UnitConversions { get; set; } = new List<ProductUnitConversion>();
    }
}
