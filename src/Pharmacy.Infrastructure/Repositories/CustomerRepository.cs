using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly PharmacyDbContext _context;

        public CustomerRepository(PharmacyDbContext context)
        {
            _context = context;
        }

        public async Task<Customer?> GetByIdAsync(Guid id)
        {
            return await _context.Customers.FindAsync(id);
        }

        public async Task<Customer?> GetByPhoneAsync(string phone)
        {
            return await _context.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
        }

        public async Task<List<Customer>> GetAllAsync()
        {
            return await _context.Customers.OrderBy(c => c.FullName).ToListAsync();
        }

        public async Task AddAsync(Customer customer)
        {
            await _context.Customers.AddAsync(customer);
        }

        public async Task<List<Order>> GetCustomerOrderHistoryAsync(Guid customerId)
        {
            return await _context.Orders
                .Include(o => o.Details)
                    .ThenInclude(d => d.Product)
                .Include(o => o.Details)
                    .ThenInclude(d => d.Batch)
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
