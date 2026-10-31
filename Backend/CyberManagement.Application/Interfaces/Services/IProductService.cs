using CyberManagement.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDTOs>> GetAllAsync();
        Task<ProductDTOs?> GetByIdAsync(long id);
        Task<IEnumerable<ProductDTOs>> SearchAsync(string? keyword);

        Task<ProductDTOs> CreateAsync(CreateProductDto request);
        Task<bool> UpdateAsync(long id, UpdateProductDto request);
        Task<bool> DeleteAsync(long id);
    }
}
