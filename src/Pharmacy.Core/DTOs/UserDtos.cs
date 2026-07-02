using System;

namespace Pharmacy.Core.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public required string Username { get; set; }
        public required string FullName { get; set; }
        public required string Role { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateUserRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string FullName { get; set; }
        public required string Role { get; set; }
    }

    public class UpdateUserRequest
    {
        public required string FullName { get; set; }
        public required string Role { get; set; }
        public string? Password { get; set; } // Optional password update
    }
}
