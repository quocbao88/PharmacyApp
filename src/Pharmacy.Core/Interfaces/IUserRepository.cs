using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByUsernameAsync(string username);
        Task<List<User>> GetAllAsync(string? search = null);
        Task AddAsync(User user);
        void Update(User user);
        void Delete(User user);
        Task SaveChangesAsync();
    }
}

