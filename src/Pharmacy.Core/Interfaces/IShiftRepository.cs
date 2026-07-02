using System;
using System.Threading.Tasks;
using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Interfaces
{
    public interface IShiftRepository
    {
        Task<Shift?> GetActiveShiftAsync();
        Task<Shift?> GetByIdAsync(Guid id);
        Task AddAsync(Shift shift);
        Task SaveChangesAsync();
    }
}
