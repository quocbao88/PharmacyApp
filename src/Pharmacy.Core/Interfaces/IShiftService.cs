using System;
using System.Threading.Tasks;
using Pharmacy.Core.DTOs;

namespace Pharmacy.Core.Interfaces
{
    public interface IShiftService
    {
        Task<ShiftDto> OpenShiftAsync(Guid userId, decimal startingCash);
        Task<ShiftDto> CloseShiftAsync(decimal endingCash);
        Task<ShiftDto?> GetActiveShiftAsync();
    }
}
