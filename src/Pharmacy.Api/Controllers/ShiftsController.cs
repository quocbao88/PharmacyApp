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
    public class ShiftsController : ControllerBase
    {
        private readonly IShiftService _shiftService;

        public ShiftsController(IShiftService shiftService)
        {
            _shiftService = shiftService;
        }

        [HttpPost("open")]
        public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequest request)
        {
            var userId = GetUserId();
            var shiftDto = await _shiftService.OpenShiftAsync(userId, request.StartingCash);
            return Ok(shiftDto);
        }

        [HttpPost("close")]
        public async Task<IActionResult> CloseShift([FromBody] CloseShiftRequest request)
        {
            var shiftDto = await _shiftService.CloseShiftAsync(request.EndingCash);
            return Ok(shiftDto);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveShift()
        {
            var shiftDto = await _shiftService.GetActiveShiftAsync();
            if (shiftDto == null)
            {
                return NotFound(new { error = "Không có ca làm việc nào đang mở." });
            }
            return Ok(shiftDto);
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Không thể định danh tài khoản người dùng.");
            }
            return userId;
        }
    }
}
