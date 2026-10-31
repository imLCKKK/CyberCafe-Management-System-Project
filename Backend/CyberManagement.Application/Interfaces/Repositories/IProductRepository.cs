using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(long id);
        Task<IEnumerable<Product>> GetByCategoryIdAsync(long categoryId);
        Task<IEnumerable<Product>> SearchAsync(string keyword);
        Task<Product> AddAsync(Product product);
        Task UpdateAsync(Product product);
        Task DeleteAsync(Product product);

        Task<int> GetStockQuantityAsync(long productId); // kiểm tra số lượng tồn kho của sản phẩm
    }
}
