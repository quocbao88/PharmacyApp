namespace Pharmacy.Core.Entities
{
    public class StockMovement
    {
        public Guid Id { get; set; }
        public Guid? RequestId { get; set; }
        public string? RequestHash { get; set; }
        public Guid BatchId { get; set; }
        public ProductBatch? Batch { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public required string Type { get; set; }
        public int Quantity { get; set; }
        public decimal StockValue { get; set; }
        public decimal CostAmount { get; set; }
        public decimal RevenueAmount { get; set; }
        public Guid? OrderDetailId { get; set; }
        public OrderDetail? OrderDetail { get; set; }
        public Guid? UserId { get; set; }
        public required string Reference { get; set; }
        public required string Reason { get; set; }
    }
}
