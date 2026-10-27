using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventory;

        public InventoryController(IInventoryService inventory)
        {
            _inventory = inventory;
        }

        [HttpGet("report")]
        public async Task<IActionResult> Report(DateTime start, DateTime end)
        {
            return Ok(await _inventory.GetReportAsync(start, end));
        }

        [HttpGet("batches/{id}/history")]
        public async Task<IActionResult> History(Guid id)
        {
            return Ok(await _inventory.GetHistoryAsync(id));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("batches/{id}/adjust")]
        public async Task<IActionResult> Adjust(Guid id, StockAdjustmentRequest request)
        {
            return Ok(await _inventory.AdjustAsync(id, request, GetUserId()));
        }

        private Guid GetUserId()
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            {
                throw new UnauthorizedAccessException();
            }
            return id;
        }
    }
}
