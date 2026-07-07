using System;
using System.Collections.Generic;

namespace Pharmacy.Core.DTOs
{
    public class CreateCustomerRequest
    {
        public required string FullName { get; set; }
        public required string Phone { get; set; }
        public string? AllergyNotes { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }

    public class CustomerDto
    {
        public Guid Id { get; set; }
        public required string FullName { get; set; }
        public required string Phone { get; set; }
        public string? AllergyNotes { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int RewardPoints { get; set; }
    }

    public class CustomerHistoryDto
    {
        public CustomerDto Customer { get; set; } = null!;
        public List<CustomerOrderDto> Orders { get; set; } = new();
    }

    public class CustomerOrderDto
    {
        public Guid OrderId { get; set; }
        public required string OrderCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public List<CustomerOrderItemDto> Items { get; set; } = new();
    }

    public class CustomerOrderItemDto
    {
        public required string ProductName { get; set; }
        public required string BatchNumber { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
