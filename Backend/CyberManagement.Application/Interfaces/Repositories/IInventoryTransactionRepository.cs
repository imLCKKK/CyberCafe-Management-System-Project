using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IInventoryTransactionRepository
    {
        Task AddAsync(InventoryTransaction transaction);

        Task<IEnumerable<InventoryTransaction>> GetByProductIdAsync(int productId);
    }
}
