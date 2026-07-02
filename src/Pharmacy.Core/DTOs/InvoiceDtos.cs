using System;
using System.Collections.Generic;

namespace Pharmacy.Core.DTOs
{
    public class CreateInvoiceDto
    {
        public required string CustomerName { get; set; }
        public required List<CreateInvoiceItemDto> Items { get; set; } = new();
    }

    public class CreateInvoiceItemDto
    {
        public Guid MedicineId { get; set; }
        public int Quantity { get; set; }
    }

    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public required string InvoiceNumber { get; set; }
        public required string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<InvoiceItemDto> Items { get; set; } = new();
    }

    public class InvoiceItemDto
    {
        public Guid Id { get; set; }
        public Guid MedicineId { get; set; }
        public string? MedicineName { get; set; }
        public string? MedicineCode { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
