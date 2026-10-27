using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pharmacy.Core.Exceptions;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Infrastructure.Services
{
    public sealed class DatabaseBackupService : IDatabaseBackupService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseBackupService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public DatabaseBackupService(IConfiguration configuration, ILogger<DatabaseBackupService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        #region Main logic

        public async Task<Stream> ExportAsync(CancellationToken cancellationToken)
        {
            if (!await _gate.WaitAsync(0, cancellationToken))
            {
                throw new PharmacyValidationException("Đang tạo một bản sao lưu. Vui lòng chờ và thử lại.");
            }

            var path = Path.Combine(Path.GetTempPath(), "pharmacy-backup-" + Guid.NewGuid().ToString("N") + ".dump");
            try
            {
                // Read server-owned settings only; clients cannot choose hosts, files or commands.
                var settings = ReadSettings();
                using var process = new Process { StartInfo = PrepareProcess(settings, path) };
                try
                {
                    process.Start();
                }
                catch (Win32Exception)
                {
                    throw new PharmacyValidationException("Máy chủ chưa cài pg_dump. Liên hệ quản trị để cấu hình công cụ sao lưu.");
                }

                // Drain diagnostic output without logging connection details or database contents.
                var errors = process.StandardError.ReadToEndAsync();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(3));
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    await process.WaitForExitAsync(CancellationToken.None);
                    await errors;
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new PharmacyValidationException("Sao lưu quá thời gian chờ. Vui lòng kiểm tra kết nối cơ sở dữ liệu và thử lại.");
                }
                await errors;
                if (process.ExitCode != 0 || !File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    _logger.LogWarning("Database backup failed with pg_dump exit code {ExitCode}", process.ExitCode);
                    throw new PharmacyValidationException("Không tạo được bản sao lưu. Kiểm tra kết nối backup dạng Direct/Session, quyền đọc và phiên bản pg_dump.");
                }

                // The response owns this stream; disposal also removes the private temporary file.
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None,
                    65536, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                path = string.Empty;
                _logger.LogInformation("Application database backup generated successfully");
                return stream;
            }
            finally
            {
                if (path.Length > 0 && File.Exists(path))
                {
                    File.Delete(path);
                }
                _gate.Release();
            }
        }

        #endregion

        #region Helpers

        private NpgsqlConnectionStringBuilder ReadSettings()
        {
            var connection = _configuration.GetConnectionString("BackupConnection");
            if (string.IsNullOrWhiteSpace(connection))
            {
                connection = _configuration.GetConnectionString("DefaultConnection");
            }
            var settings = new NpgsqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Database)
                || string.IsNullOrWhiteSpace(settings.Username))
            {
                throw new PharmacyValidationException("Chưa cấu hình kết nối sao lưu cơ sở dữ liệu.");
            }
            if (settings.Host.Contains("pooler.supabase.com", StringComparison.OrdinalIgnoreCase) && settings.Port == 6543)
            {
                throw new PharmacyValidationException("Cần cấu hình ConnectionStrings__BackupConnection bằng kết nối Supabase Session pooler cổng 5432 hoặc Direct; không dùng Transaction pooler cổng 6543.");
            }
            return settings;
        }

        private ProcessStartInfo PrepareProcess(NpgsqlConnectionStringBuilder settings, string path)
        {
            var executable = _configuration["Backup:PgDumpPath"];
            if (string.IsNullOrWhiteSpace(executable))
            {
                executable = "pg_dump";
            }
            var start = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "--format=custom", "--schema=public", "--no-owner", "--no-acl", "--no-password", "--lock-wait-timeout=10s", "--file", path })
            {
                start.ArgumentList.Add(argument);
            }
            // Keep credentials out of process arguments, logs and the downloaded archive.
            start.Environment["PGHOST"] = settings.Host;
            start.Environment["PGPORT"] = settings.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            start.Environment["PGDATABASE"] = settings.Database;
            start.Environment["PGUSER"] = settings.Username;
            start.Environment["PGPASSWORD"] = settings.Password ?? string.Empty;
            start.Environment["PGCONNECT_TIMEOUT"] = "15";
            start.Environment["PGSSLMODE"] = settings.SslMode.ToString().ToLowerInvariant();
            if (settings.SslMode == SslMode.VerifyCA)
            {
                start.Environment["PGSSLMODE"] = "verify-ca";
            }
            else if (settings.SslMode == SslMode.VerifyFull)
            {
                start.Environment["PGSSLMODE"] = "verify-full";
            }
            if (!string.IsNullOrWhiteSpace(settings.RootCertificate))
            {
                start.Environment["PGSSLROOTCERT"] = settings.RootCertificate;
            }
            return start;
        }

        #endregion
    }
}
