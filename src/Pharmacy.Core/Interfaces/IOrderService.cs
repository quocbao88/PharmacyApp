using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pharmacy.Core.DTOs;

namespace Pharmacy.Core.Interfaces
{
    public interface IOrderService
    {
        Task<OrderDto> CheckoutAsync(Guid userId, CheckoutOrderRequest request);
        Task<OrderDto> GetOrderByIdAsync(Guid id);
        Task<OrderDto> SyncNationalAsync(Guid orderId);
        Task<List<OrderDto>> GetRecentOrdersAsync(int limit);
        Task<List<OrderDto>> GetOrdersAsync(DateTime? startDate, DateTime? endDate);
        Task CancelOrderAsync(Guid orderId);
    }
}
