using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        public CategoryService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<CategoryDTOs> CreateCategoryAsync(CreateCategoryDTOs request)
        {
            if (string.IsNullOrWhiteSpace(request.CategoryName))
            {
                throw new ArgumentException("Category name cannot be null or empty.");
            }
            var category = new Category
            {
                CategoryName = request.CategoryName
            };

            var createdCategory = await _categoryRepository.AddAsync(category);
            
            var categoryDTO = new CategoryDTOs
            {
                CategoryId = createdCategory.CategoryId,
                CategoryName = createdCategory.CategoryName
            };
            return categoryDTO;
        }

        //delete tạm thời, sau này sẽ thêm việc xóa category có ảnh hưởng đến product hay không? 
        //Khi xóa phải kiểm tra số lượng product
        public async Task<bool> DeleteCategoryAsync(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null) return false;

            await _categoryRepository.DeleteAsync(category);
            return true;
        }

        public async Task<IEnumerable<CategoryDTOs>> GetAllCategoriesAsync()
        {
            var categories = await _categoryRepository.GetAllAsync();
            
            var categoryDTOs = categories.Select(category => new CategoryDTOs
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName
            });
            return categoryDTOs;

        }

        public async Task<CategoryDTOs?> GetCategoryByIdAsync(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null) return null;

            var categoryDTO = new CategoryDTOs
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName
            };
            return categoryDTO;
        }

        public async Task<bool> UpdateCategoryAsync(int Id, UpdateCategoryDTOs request)
        {
            if(string.IsNullOrWhiteSpace(request.CategoryName))
            {
                throw new ArgumentException("Category name cannot be null or empty.");
            }

            var category = await _categoryRepository.GetByIdAsync(Id);
            if(category == null) return false;
            category.CategoryName = request.CategoryName;

            await _categoryRepository.UpdateAsync(category);
            return true;
        }
    }
}
