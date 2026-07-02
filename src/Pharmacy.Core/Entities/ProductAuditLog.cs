using System;

namespace Pharmacy.Core.Entities
{
    public class ProductAuditLog
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string? ProductName { get; set; }
        public required string Action { get; set; } // Thêm mới, Cập nhật, Xóa, Nhập lô
        public required string ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string? Details { get; set; }
    }
}
