using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pharmacy.Core.Entities;
using Pharmacy.Core.DTOs;

namespace Pharmacy.Core.Interfaces
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(Guid id);
        Task AddAsync(Order order);
        Task<string> GetNextOrderCodeAsync();
        Task<List<Order>> GetOrdersByUserIdAsync(Guid userId);

        // Report/Analytics Queries
        Task<List<Order>> GetOrdersInPeriodAsync(DateTime start, DateTime end);
        Task<List<TopProductDto>> GetTopSellingProductsAsync(DateTime start, DateTime end, int limit);
        Task SaveChangesAsync();
    }
}
