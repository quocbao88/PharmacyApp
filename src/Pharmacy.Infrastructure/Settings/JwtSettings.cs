using System.Text;
using Microsoft.Extensions.Configuration;

namespace Pharmacy.Infrastructure.Settings
{
    public class JwtSettings
    {
        public string Key { get; private set; } = string.Empty;
        public string Issuer { get; private set; } = string.Empty;
        public string Audience { get; private set; } = string.Empty;

        public static JwtSettings Read(IConfiguration configuration)
        {
            var key = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            {
                throw new InvalidOperationException("Jwt:Key must contain at least 32 UTF-8 bytes.");
            }

            // Token issuance and validation must use the same settings and encoding.
            return new JwtSettings
            {
                Key = key,
                Issuer = configuration["Jwt:Issuer"] ?? "TTYTSaiDongApi",
                Audience = configuration["Jwt:Audience"] ?? "TTYTSaiDongClient"
            };
        }
    }
}
