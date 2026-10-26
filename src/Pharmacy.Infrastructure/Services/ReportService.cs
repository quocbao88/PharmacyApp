using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Interfaces;
using Pharmacy.Core.Exceptions;

namespace Pharmacy.Infrastructure.Services
{
    public class ReportService : IReportService
    {
        private readonly IOrderRepository _orderRepository;

        public ReportService(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public async Task<ReportSummaryDto> GetRevenueAndProfitReportAsync(DateTime start, DateTime end)
        {
            CheckDates(start, end);

            // Normalize dates to include full range and specify UTC kind for PostgreSQL compatibility
            var normalizedStart = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
            var normalizedEnd = DateTime.SpecifyKind(end.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var orders = await _orderRepository.GetOrdersInPeriodAsync(normalizedStart, normalizedEnd);

            decimal totalRevenue = 0;
            decimal totalCost = 0;
            var dailyMap = new Dictionary<DateTime, (decimal Revenue, decimal Cost)>();

            foreach (var order in orders)
            {
                var orderDate = DateTime.SpecifyKind(order.CreatedAt.Date, DateTimeKind.Utc);
                
                decimal orderRevenue = order.TotalAmount;
                // Calculate total cost for this order (Sum of d.Quantity * d.CostPrice)
                decimal orderCost = order.Details.Sum(d => d.Quantity * d.CostPrice);

                totalRevenue += orderRevenue;
                totalCost += orderCost;

                if (dailyMap.ContainsKey(orderDate))
                {
                    var existing = dailyMap[orderDate];
                    dailyMap[orderDate] = (existing.Revenue + orderRevenue, existing.Cost + orderCost);
                }
                else
                {
                    dailyMap[orderDate] = (orderRevenue, orderCost);
                }
            }

            // Fill missing days with zero stats (user-friendly chart layout)
            var dailyStatsList = new List<DailyReportItemDto>();
            for (var date = normalizedStart.Date; date <= normalizedEnd.Date; date = date.AddDays(1))
            {
                var dateUtc = DateTime.SpecifyKind(date, DateTimeKind.Utc);
                decimal revenue = 0;
                decimal cost = 0;

                if (dailyMap.TryGetValue(dateUtc, out var stats))
                {
                    revenue = stats.Revenue;
                    cost = stats.Cost;
                }

                dailyStatsList.Add(new DailyReportItemDto
                {
                    Date = dateUtc,
                    Revenue = revenue,
                    Cost = cost
                });
            }

            return new ReportSummaryDto
            {
                TotalRevenue = totalRevenue,
                TotalCost = totalCost,
                DailyStats = dailyStatsList.OrderBy(d => d.Date).ToList()
            };
        }

        public async Task<List<TopProductDto>> GetTopSellingProductsAsync(DateTime start, DateTime end, int limit)
        {
            CheckDates(start, end);
            if (limit <= 0)
            {
                throw new PharmacyValidationException("Số lượng sản phẩm phải lớn hơn 0.");
            }

            var normalizedStart = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
            var normalizedEnd = DateTime.SpecifyKind(end.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            return await _orderRepository.GetTopSellingProductsAsync(normalizedStart, normalizedEnd, limit);
        }

        private static void CheckDates(DateTime start, DateTime end)
        {
            if (start == default || end == default || start.Date > end.Date || end.Date == DateTime.MaxValue.Date)
            {
                throw new PharmacyValidationException("Khoảng ngày báo cáo không hợp lệ.");
            }
        }
    }
}
