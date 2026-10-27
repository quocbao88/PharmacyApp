using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers
{
    [ApiController]
    [Route("api/backup")]
    [Authorize(Roles = "Admin")]
    public class BackupController : ControllerBase
    {
        private readonly IDatabaseBackupService _backupService;
        private readonly ILogger<BackupController> _logger;

        public BackupController(IDatabaseBackupService backupService, ILogger<BackupController> logger)
        {
            _backupService = backupService;
            _logger = logger;
        }

        [HttpPost("export")]
        public async Task<IActionResult> Export(CancellationToken cancellationToken)
        {
            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            _logger.LogInformation("Database backup requested by administrator {UserId}", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            var stream = await _backupService.ExportAsync(cancellationToken);
            return File(stream, "application/octet-stream", $"pharmacy-public-{DateTime.UtcNow:yyyyMMdd-HHmmss}.dump");
        }
    }
}
