using System;
using System.Collections.Generic;

namespace Pharmacy.Core.DTOs
{
    public class ReportSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit => TotalRevenue - TotalCost;
        public List<DailyReportItemDto> DailyStats { get; set; } = new();
    }

    public class DailyReportItemDto
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit => Revenue - Cost;
    }

    public class TopProductDto
    {
        public Guid ProductId { get; set; }
        public required string ProductName { get; set; }
        public required string Unit { get; set; }
        public int TotalQuantitySold { get; set; }
        public decimal TotalRevenueGenerated { get; set; }
    }
}
