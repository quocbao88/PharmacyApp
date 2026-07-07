using System;
using System.Collections.Generic;

namespace Pharmacy.Core.DTOs
{
    public class CheckoutOrderRequest
    {
        public Guid? CustomerId { get; set; }
        public string? Notes { get; set; }
        public decimal DiscountAmount { get; set; }
        public required string PaymentMethod { get; set; } // Cash, Transfer
        public List<CheckoutItemDto> Items { get; set; } = new();

        // Prescription Info (Bán thuốc theo đơn)
        public string? PrescriptionCode { get; set; }       // Mã đơn thuốc quốc gia
        public string? PrescribingDoctor { get; set; }      // Bác sĩ kê đơn
        public string? MedicalFacility { get; set; }       // Cơ sở y tế kê đơn
        public string? Diagnostic { get; set; }            // Chẩn đoán bệnh
    }

    public class CheckoutItemDto
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public string? SoldUnit { get; set; }               // Đơn vị bán lựa chọn (e.g. Hộp, Vỉ, Viên)
    }

    public class OrderDto
    {
        public Guid Id { get; set; }
        public required string OrderCode { get; set; }
        public Guid UserId { get; set; }
        public string? StaffName { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public required string PaymentMethod { get; set; }
        public string Status { get; set; } = null!;
        public List<OrderDetailDto> Details { get; set; } = new();

        // National Sync Info
        public string? NationalSyncStatus { get; set; }
        public string? NationalSyncMessage { get; set; }
        public DateTime? NationalSyncedAt { get; set; }

        // Prescription Info
        public string? PrescriptionCode { get; set; }
        public string? PrescribingDoctor { get; set; }
        public string? MedicalFacility { get; set; }
        public string? Diagnostic { get; set; }
    }

    public class OrderDetailDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public required string ProductName { get; set; }
        public required string Unit { get; set; }
        public Guid BatchId { get; set; }
        public required string BatchNumber { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal Subtotal { get; set; }

        // Unit conversion details
        public string? SoldUnit { get; set; }
        public int ConversionValue { get; set; }
    }

    public class OrderListDto
    {
        public Guid Id { get; set; }
        public required string OrderCode { get; set; }
        public string? StaffName { get; set; }
        public string? CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public required string PaymentMethod { get; set; }
        public int ItemCount { get; set; }

        // National Sync Info
        public string? NationalSyncStatus { get; set; }
        public string Status { get; set; } = null!;
    }
}
