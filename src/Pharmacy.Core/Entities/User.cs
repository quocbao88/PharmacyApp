using System;

namespace Pharmacy.Core.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public required string Username { get; set; }
        public required string PasswordHash { get; set; }
        public required string FullName { get; set; }
        public required string Role { get; set; } // Admin, Staff
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
