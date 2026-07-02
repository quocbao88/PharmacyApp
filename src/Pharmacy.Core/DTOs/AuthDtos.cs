using System;

namespace Pharmacy.Core.DTOs
{
    public class LoginRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
    }

    public class LoginResponse
    {
        public required string Token { get; set; }
        public required string Username { get; set; }
        public required string FullName { get; set; }
        public required string Role { get; set; }
    }

    public class OpenShiftRequest
    {
        public decimal StartingCash { get; set; }
    }

    public class CloseShiftRequest
    {
        public decimal EndingCash { get; set; }
    }

    public class ShiftDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? UserFullName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal StartingCash { get; set; }
        public decimal? EndingCash { get; set; }
        public decimal ActualRevenue { get; set; }
        public required string Status { get; set; }
    }
}
