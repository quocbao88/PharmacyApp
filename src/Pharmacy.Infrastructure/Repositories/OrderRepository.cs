using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;
using Pharmacy.Core.DTOs;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly PharmacyDbContext _context;

        public OrderRepository(PharmacyDbContext context)
        {
            _context = context;
        }

        public async Task<Order?> GetByIdAsync(Guid id)
        {
            return await _context.Orders
                .Include(o => o.Returns).ThenInclude(x => x.Details)
                .Include(o => o.User)
                .Include(o => o.Customer)
                .Include(o => o.Details)
                    .ThenInclude(d => d.Product)
                .Include(o => o.Details)
                    .ThenInclude(d => d.Batch)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task AddAsync(Order order)
        {
            await _context.Orders.AddAsync(order);
        }

        public async Task<string> GetNextOrderCodeAsync()
        {
            var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"HD-{todayStr}-";

            // Serialize the daily counter until checkout commits, including sales of different products.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({734201}, {int.Parse(todayStr)})");

            var count = await _context.Orders
                .Where(o => o.OrderCode.StartsWith(prefix))
                .CountAsync();

            return $"{prefix}{(count + 1):D4}";
        }

        public async Task<List<Order>> GetOrdersByUserIdAsync(Guid userId)
        {
            return await _context.Orders
                .Include(o => o.Details)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Order>> GetOrdersInPeriodAsync(DateTime start, DateTime end)
        {
            return await _context.Orders
                .Include(o => o.Details)
                .AsNoTracking()
                .Where(o => o.CreatedAt >= start && o.CreatedAt <= end && o.Status != "Cancelled")
                .ToListAsync();
        }

        public async Task<List<TopProductDto>> GetTopSellingProductsAsync(DateTime start, DateTime end, int limit)
        {
            return await _context.OrderDetails
                .Include(d => d.Product)
                .Include(d => d.Order)
                .AsNoTracking()
                .Where(d => d.Order!.CreatedAt >= start && d.Order.CreatedAt <= end && d.Order.Status != "Cancelled")
                .GroupBy(d => new { d.ProductId, d.Product!.Name, d.Product.Unit })
                .Select(g => new TopProductDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    Unit = g.Key.Unit,
                    TotalQuantitySold = g.Sum(x => (x.Quantity - x.ReturnedQuantity) * x.ConversionValue),
                    TotalRevenueGenerated = g.Sum(x => (x.Quantity - x.ReturnedQuantity) * x.UnitPrice)
                })
                .OrderByDescending(x => x.TotalQuantitySold)
                .Take(limit)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
