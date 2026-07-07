using System;

namespace Pharmacy.Core.Entities
{
    public class Customer
    {
        public Guid Id { get; set; }
        public required string FullName { get; set; }
        public required string Phone { get; set; } // Unique
        public string? AllergyNotes { get; set; } // Tiền sử dị ứng
        public DateTime? DateOfBirth { get; set; } // Ngày sinh
        public int RewardPoints { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
