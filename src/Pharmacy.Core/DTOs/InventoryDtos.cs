using System.ComponentModel.DataAnnotations;

namespace Pharmacy.Core.DTOs
{
    public class StockAdjustmentRequest
    {
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }
        public int ExpectedQuantity { get; set; }
        [Required, StringLength(500, MinimumLength = 3)]
        public required string Reason { get; set; }
    }

    public class StockReportDto
    {
        public DateTime? CoverageStart { get; set; }
        public List<StockReportItemDto> Items { get; set; } = new();
    }

    public class StockReportItemDto
    {
        public Guid ProductId { get; set; }
        public Guid BatchId { get; set; }
        public required string Name { get; set; }
        public required string Unit { get; set; }
        public required string BatchNumber { get; set; }
        public DateTime ExpirationDate { get; set; }
        public decimal ImportPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int OpeningQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public int SoldQuantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public int AdjustedQuantity { get; set; }
        public int ClosingQuantity { get; set; }
        public decimal ReceiptAmount { get; set; }
        public decimal CostAmount { get; set; }
        public decimal RevenueAmount { get; set; }
        public decimal ClosingValue { get; set; }
    }

    public class StockMovementDto
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public required string Type { get; set; }
        public int Quantity { get; set; }
        public required string Reference { get; set; }
        public required string Reason { get; set; }
    }

    public class CancelOrderRequest
    {
        [Required, StringLength(500, MinimumLength = 3)]
        public required string Reason { get; set; }
    }

    public class ReturnOrderRequest : CancelOrderRequest
    {
        public Guid RequestId { get; set; }
        [Required, MinLength(1)]
        public List<ReturnOrderItem> Items { get; set; } = new();
    }

    public class ReturnOrderItem
    {
        public Guid OrderDetailId { get; set; }
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
        public bool Restock { get; set; } = true;
    }

    public class OrderReturnDto
    {
        public DateTime CreatedAt { get; set; }
        public required string Reason { get; set; }
        public decimal RefundAmount { get; set; }
        public List<OrderReturnItemDto> Details { get; set; } = new();
    }

    public class OrderReturnItemDto
    {
        public Guid OrderDetailId { get; set; }
        public int Quantity { get; set; }
        public bool Restock { get; set; }
        public decimal RefundAmount { get; set; }
    }
}
