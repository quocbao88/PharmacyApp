namespace Pharmacy.Core.Entities
{
    public class OrderReturn
    {
        public Guid Id { get; set; }
        public Guid RequestId { get; set; }
        public required string RequestHash { get; set; }
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public required string Reason { get; set; }
        public decimal RefundAmount { get; set; }
        public int RevokedPoints { get; set; }
        public List<OrderReturnDetail> Details { get; set; } = new();
    }

    public class OrderReturnDetail
    {
        public Guid Id { get; set; }
        public Guid OrderReturnId { get; set; }
        public OrderReturn? OrderReturn { get; set; }
        public Guid OrderDetailId { get; set; }
        public OrderDetail? OrderDetail { get; set; }
        public int Quantity { get; set; }
        public bool Restock { get; set; }
        public decimal RefundAmount { get; set; }
    }
}
