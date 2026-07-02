using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Interfaces
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(Guid id);
        Task<Customer?> GetByPhoneAsync(string phone);
        Task<List<Customer>> GetAllAsync();
        Task AddAsync(Customer customer);
        Task<List<Order>> GetCustomerOrderHistoryAsync(Guid customerId);
        Task SaveChangesAsync();
    }
}
