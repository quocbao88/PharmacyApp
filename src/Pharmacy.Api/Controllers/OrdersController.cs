using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        public async Task<IActionResult> Checkout([FromBody] CheckoutOrderRequest request)
        {
            var userId = GetUserId();
            var orderDto = await _orderService.CheckoutAsync(userId, request);
            return Ok(orderDto);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var orderDto = await _orderService.GetOrderByIdAsync(id);
            return Ok(orderDto);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrders([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int? limit)
        {
            if (limit.HasValue)
            {
                var orders = await _orderService.GetRecentOrdersAsync(limit.Value);
                return Ok(orders);
            }
            else
            {
                var orders = await _orderService.GetOrdersAsync(startDate, endDate);
                return Ok(orders);
            }
        }

        [HttpPost("{id}/sync-national")]
        public async Task<IActionResult> SyncNational(Guid id)
        {
            var orderDto = await _orderService.SyncNationalAsync(id);
            return Ok(orderDto);
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                throw new UnauthorizedAccessException("Không thể định danh tài khoản người dùng.");
            return userId;
        }
    }
}
