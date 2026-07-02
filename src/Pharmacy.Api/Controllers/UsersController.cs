using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;

        public UsersController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search)
        {
            var users = await _userRepository.GetAllAsync(search);
            return Ok(users.Select(MapToDto));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound(new { error = "Không tìm thấy người dùng." });
            return Ok(MapToDto(user));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            var existingUser = await _userRepository.GetByUsernameAsync(request.Username);
            if (existingUser != null)
            {
                return BadRequest(new { error = "Tên đăng nhập đã tồn tại trong hệ thống." });
            }

            if (request.Role != "Admin" && request.Role != "Staff")
            {
                return BadRequest(new { error = "Vai trò không hợp lệ. Phải là 'Admin' hoặc 'Staff'." });
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                PasswordHash = HashPassword(request.Password),
                FullName = request.FullName,
                Role = request.Role,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, MapToDto(user));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound(new { error = "Không tìm thấy người dùng." });

            if (request.Role != "Admin" && request.Role != "Staff")
            {
                return BadRequest(new { error = "Vai trò không hợp lệ. Phải là 'Admin' hoặc 'Staff'." });
            }

            user.FullName = request.FullName;
            user.Role = request.Role;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.PasswordHash = HashPassword(request.Password);
            }

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return NotFound(new { error = "Không tìm thấy người dùng." });

            var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(currentUserIdClaim, out var currentUserId) && currentUserId == id)
            {
                return BadRequest(new { error = "Bạn không thể tự xóa tài khoản của chính mình." });
            }

            _userRepository.Delete(user);
            await _userRepository.SaveChangesAsync();

            return NoContent();
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private static UserDto MapToDto(User u) => new()
        {
            Id = u.Id,
            Username = u.Username,
            FullName = u.FullName,
            Role = u.Role,
            CreatedAt = u.CreatedAt
        };
    }
}
