using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.Extensions.Logging;

namespace Pharmacy.Api.Controllers
{
    [ApiController]
    [Route("api/national-portal-mock")]
    public class NationalPortalMockController : ControllerBase
    {
        private readonly ILogger<NationalPortalMockController> _logger;

        public NationalPortalMockController(ILogger<NationalPortalMockController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public IActionResult SyncOrder([FromBody] object payload)
        {
            _logger.LogInformation("Cổng Dược Quốc gia nhận payload liên thông: {Payload}", payload?.ToString());
            
            return Ok(new
            {
                Success = true,
                TransactionId = "QD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                Message = "Đồng bộ hóa đơn lên Cổng Dược Quốc gia thành công."
            });
        }
    }
}
