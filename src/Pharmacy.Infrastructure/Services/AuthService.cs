using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Exceptions;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _userRepository.GetByUsernameAsync(request.Username);
            if (user == null || user.PasswordHash != HashPassword(request.Password))
            {
                throw new PharmacyValidationException("Tên đăng nhập hoặc mật khẩu không chính xác.");
            }

            var token = GenerateJwtToken(user);

            return new LoginResponse
            {
                Token = token,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role
            };
        }

        public async Task RegisterAsync(string username, string password, string fullName, string role)
        {
            var existingUser = await _userRepository.GetByUsernameAsync(username);
            if (existingUser != null)
            {
                throw new PharmacyValidationException("Tên đăng nhập đã tồn tại trong hệ thống.");
            }

            if (role != "Admin" && role != "Staff")
            {
                throw new PharmacyValidationException("Vai trò không hợp lệ. Phải là 'Admin' hoặc 'Staff'.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = HashPassword(password),
                FullName = fullName,
                Role = role
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Fallback key if configuration is missing (for developer ease)
            var secretKey = _configuration["Jwt:Key"] ?? "MedicareSuperSecretSecurityKeyThatIsAtLeast32BytesLong!";
            var issuer = _configuration["Jwt:Issuer"] ?? "MedicareApi";
            var audience = _configuration["Jwt:Audience"] ?? "MedicareClient";

            var key = Encoding.ASCII.GetBytes(secretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("username", user.Username)
                }),
                Expires = DateTime.UtcNow.AddDays(1), // Token valid for 1 day
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
