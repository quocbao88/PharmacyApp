using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Repositories
{
    public class ShiftRepository : IShiftRepository
    {
        private readonly PharmacyDbContext _context;

        public ShiftRepository(PharmacyDbContext context)
        {
            _context = context;
        }

        public async Task<Shift?> GetActiveShiftAsync()
        {
            return await _context.Shifts
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Status == "Open");
        }

        public async Task<Shift?> GetByIdAsync(Guid id)
        {
            return await _context.Shifts
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task AddAsync(Shift shift)
        {
            await _context.Shifts.AddAsync(shift);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
