using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        public ProductService(IProductRepository productRepository, ICategoryRepository categoryRepository)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
        }

        public async Task<ProductDTOs> CreateAsync(CreateProductDto request)
        {
            if(string.IsNullOrWhiteSpace(request.ProductName))
                throw new ArgumentException("ProductName cannot be null or empty.");

            if (request.Price <= 0)
                throw new ArgumentException("Price must be greater than 0.");
            if (request.Stock < 0)
                throw new ArgumentException("Stock must be non-negative.");
            if (request.CategoryId <= 0)
                throw new ArgumentException("CategoryId must be a positive integer.");

            //Check if the category exists
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
            if (category == null)
                throw new ArgumentException("Invalid CategoryId.");

            var product = new Product
            {
                CategoryId = request.CategoryId,
                ProductName = request.ProductName,
                Price = request.Price,
                Stock = request.Stock
            };

            var createdProduct = await _productRepository.AddAsync(product);
            var productDto = new ProductDTOs
            {
                ProductId = createdProduct.ProductId,
                CategoryId = createdProduct.CategoryId,
                ProductName = createdProduct.ProductName,
                Price = createdProduct.Price,
                Stock = createdProduct.Stock
            };
            return productDto;


        }

        // TODO: Cân nhắc Soft Delete(chuyển trạng thái) thay vì xóa Product trực tiếp,
        // vì Product có thể đã được tham chiếu bởi OrderDetail của các đơn hàng cũ.
        public async Task<bool> DeleteAsync(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return false;

            await _productRepository.DeleteAsync(product);
            return true;    
        }

        public async Task<IEnumerable<ProductDTOs>> GetAllAsync()
        {
            var products = await _productRepository.GetAllAsync();

            var productDTOs = products.Select(product => new ProductDTOs
            {
                ProductId = product.ProductId,
                CategoryId = product.CategoryId,
                ProductName = product.ProductName,
                Price = product.Price,
                Stock = product.Stock
            });
            return productDTOs;
        }

        public async Task<ProductDTOs?> GetByIdAsync(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return null;
            var productDto = new ProductDTOs
            {
                ProductId = product.ProductId,
                CategoryId = product.CategoryId,
                ProductName = product.ProductName,
                Price = product.Price,
                Stock = product.Stock
            };
            return productDto;
        }

        public async Task<IEnumerable<ProductDTOs>> SearchAsync(string keyword)
        {
            if(string.IsNullOrWhiteSpace(keyword)) return await GetAllAsync();
            keyword = keyword.Trim();

            var products = await _productRepository.SearchAsync(keyword);
            var productDTOs= products.Select(product => new ProductDTOs
            {
                ProductId = product.ProductId,
                CategoryId = product.CategoryId,
                ProductName = product.ProductName,
                Price = product.Price,
                Stock = product.Stock
            });
            return productDTOs;

        }

        public async Task<bool> UpdateAsync(int id, UpdateProductDto request)
        {
            if(string.IsNullOrWhiteSpace(request.ProductName))
                throw new ArgumentException("ProductName cannot be null or empty.");
            if(request.Price <= 0)
                throw new ArgumentException("Price must be greater than 0.");
            if (request.Stock < 0)
                throw new ArgumentException("Stock must be non-negative.");

            //check if the product exists
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return false;

            // Validate CategoryId
            if (request.CategoryId <= 0)
                throw new ArgumentException("CategoryId must be a positive integer.");
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
            if (category == null)
                throw new ArgumentException("Invalid CategoryId.");


            product.CategoryId = request.CategoryId;
            product.ProductName = request.ProductName;
            product.Price = request.Price;
            product.Stock = request.Stock;

            await _productRepository.UpdateAsync(product);
            return true;
        }
    }
}
