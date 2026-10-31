using CyberManagement.Application.DTOs;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;


namespace CyberManagement.Application.Interfaces.Services
{
    public interface ICategoryService
    {
        Task<IEnumerable<CategoryDTOs>> GetAllCategoriesAsync();
        Task<CategoryDTOs?> GetCategoryByIdAsync(long id);
        Task<CategoryDTOs> CreateCategoryAsync(CreateCategoryDTOs request);
        Task<bool> UpdateCategoryAsync(long Id , UpdateCategoryDTOs request);
        Task<bool> DeleteCategoryAsync(long id);
        //Task<bool> ExistsByNameAsync(string categoryName); kiểm tra trùng tên
    }
}
