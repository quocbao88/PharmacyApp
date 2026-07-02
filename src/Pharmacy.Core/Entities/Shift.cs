using System;

namespace Pharmacy.Core.Entities
{
    public class Shift
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public decimal StartingCash { get; set; } // Tiền đầu ca
        public decimal? EndingCash { get; set; } // Tiền chốt ca thực tế
        public decimal ActualRevenue { get; set; } = 0; // Doanh thu ghi nhận trên hệ thống trong ca
        public required string Status { get; set; } = "Open"; // Open, Closed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}
