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
        Task<CategoryDTOs?> GetCategoryByIdAsync(int id);
        Task<CategoryDTOs> CreateCategoryAsync(CreateCategoryDTOs request);
        Task<bool> UpdateCategoryAsync(int Id , UpdateCategoryDTOs request);
        Task<bool> DeleteCategoryAsync(int id);
        //Task<bool> ExistsByNameAsync(string categoryName); kiểm tra trùng tên
    }
}
