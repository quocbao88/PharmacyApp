using System;

namespace Pharmacy.Core.DTOs
{
    public class CreateProductRequest
    {
        public Guid SupplierId { get; set; }
        public required string Name { get; set; }
        public string? ActiveIngredient { get; set; }
        public string? Category { get; set; }
        public string? Manufacturer { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? StorageConditions { get; set; }
        public bool PrescriptionRequired { get; set; }
        public string? Description { get; set; }
        public required string Unit { get; set; }
        public decimal ImportPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int MinStockLevel { get; set; }
        public System.Collections.Generic.List<ProductUnitConversionDto> UnitConversions { get; set; } = new();
    }

    public class ProductDto
    {
        public Guid Id { get; set; }
        public Guid SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public required string Name { get; set; }
        public string? ActiveIngredient { get; set; }
        public string? Category { get; set; }
        public string? Manufacturer { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? StorageConditions { get; set; }
        public bool PrescriptionRequired { get; set; }
        public string? Description { get; set; }
        public required string Unit { get; set; }
        public decimal ImportPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int MinStockLevel { get; set; }
        public int TotalStock { get; set; }
        public DateTime UpdatedAt { get; set; }
        public System.Collections.Generic.List<ProductUnitConversionDto> UnitConversions { get; set; } = new();
    }

    public class ProductUnitConversionDto
    {
        public Guid Id { get; set; }
        public required string UnitName { get; set; }
        public int ConversionValue { get; set; }
        public decimal SellingPrice { get; set; }
    }

    public class CreateBatchRequest
    {
        public required string BatchNumber { get; set; }
        public DateTime ExpirationDate { get; set; }
        public int Quantity { get; set; }
        public decimal? ImportPrice { get; set; }  // Giá nhập lô này (có thể khác giá gốc)
    }

    public class UpdateBatchRequest
    {
        public required string BatchNumber { get; set; }
        public DateTime ExpirationDate { get; set; }
        public int CurrentQuantity { get; set; }
        public decimal? ImportPrice { get; set; }
    }

    public class BatchDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string? ProductName { get; set; }
        public required string BatchNumber { get; set; }
        public DateTime ExpirationDate { get; set; }
        public int CurrentQuantity { get; set; }
        public decimal? ImportPrice { get; set; }
    }

    public class LowStockAlertDto
    {
        public Guid ProductId { get; set; }
        public required string ProductName { get; set; }
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public string? Unit { get; set; }
    }

    public class ExpiringBatchAlertDto
    {
        public Guid ProductId { get; set; }
        public required string ProductName { get; set; }
        public Guid BatchId { get; set; }
        public required string BatchNumber { get; set; }
        public DateTime ExpirationDate { get; set; }
        public int DaysRemaining { get; set; }
        public int CurrentQuantity { get; set; }
    }

    public class SmartAlertsResponse
    {
        public System.Collections.Generic.List<LowStockAlertDto> LowStockProducts { get; set; } = new();
        public System.Collections.Generic.List<ExpiringBatchAlertDto> ExpiringBatches { get; set; } = new();
    }

    public class ProductHistoryDto
    {
        public Guid OrderId { get; set; }
        public required string OrderCode { get; set; }
        public DateTime SaleDate { get; set; }
        public string? StaffName { get; set; }
        public string? CustomerName { get; set; }
        public string? BatchNumber { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string? SoldUnit { get; set; }
        public int? ConversionValue { get; set; }
    }
}
