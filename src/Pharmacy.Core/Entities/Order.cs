using System;
using System.Collections.Generic;

namespace Pharmacy.Core.Entities
{
    public class Order
    {
        public Guid Id { get; set; }
        public required string OrderCode { get; set; }  // Unique order code
        public Guid UserId { get; set; }                // Nhân viên tạo đơn
        public Guid? CustomerId { get; set; }
        public string? Notes { get; set; }              // Ghi chú đơn hàng
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public required string PaymentMethod { get; set; }  // Cash, Transfer

        // National Sync Status
        public string? NationalSyncStatus { get; set; } = "Pending"; // Pending, Synced, Failed, None
        public string? NationalSyncMessage { get; set; }
        public DateTime? NationalSyncedAt { get; set; }

        // Prescription Info (Bán thuốc theo đơn)
        public string? PrescriptionCode { get; set; }       // Mã đơn thuốc quốc gia
        public string? PrescribingDoctor { get; set; }      // Bác sĩ kê đơn
        public string? MedicalFacility { get; set; }       // Cơ sở y tế kê đơn
        public string? Diagnostic { get; set; }            // Chẩn đoán bệnh

        // Navigation
        public User? User { get; set; }
        public Customer? Customer { get; set; }
        public ICollection<OrderDetail> Details { get; set; } = new List<OrderDetail>();
    }
}
