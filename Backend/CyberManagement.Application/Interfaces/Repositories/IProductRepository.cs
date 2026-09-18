using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(int id);
        Task<IEnumerable<Product>> GetByCategoryIdAsync(int categoryId);
        Task<IEnumerable<Product>> SearchByNameAsync(string keyword);
        Task AddAsync(Product product);
        void Update(Product product);
        void Delete(Product product);

        Task<int> GetStockQuantityAsync(int productId); // kiểm tra số lượng tồn kho của sản phẩm
    }
}
