using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardReport([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var report = await _reportService.GetRevenueAndProfitReportAsync(start, end);
            return Ok(report);
        }

        [HttpGet("top-selling")]
        public async Task<IActionResult> GetTopSellingProducts(
            [FromQuery] DateTime start, 
            [FromQuery] DateTime end, 
            [FromQuery] int limit = 10)
        {
            var topProducts = await _shiftServiceGetTop(start, end, limit);
            return Ok(topProducts);
        }

        private async Task<System.Collections.Generic.List<Pharmacy.Core.DTOs.TopProductDto>> _shiftServiceGetTop(DateTime start, DateTime end, int limit)
        {
            return await _reportService.GetTopSellingProductsAsync(start, end, limit);
        }
    }
}
