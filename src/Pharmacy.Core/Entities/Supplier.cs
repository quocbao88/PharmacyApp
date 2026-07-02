using System;
using System.Collections.Generic;

namespace Pharmacy.Core.Entities
{
    public class Supplier
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
