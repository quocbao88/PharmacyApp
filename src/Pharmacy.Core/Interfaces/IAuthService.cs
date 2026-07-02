using System.Threading.Tasks;
using Pharmacy.Core.DTOs;

namespace Pharmacy.Core.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task RegisterAsync(string username, string password, string fullName, string role);
    }
}
