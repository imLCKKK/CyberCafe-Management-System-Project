using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // GET: api/<CategoriesController>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            return Ok(categories);
        }

        // GET api/<CategoriesController>/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if(category == null)
                return NotFound();
            return Ok(category);
        }

        // POST api/<CategoriesController>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCategoryDTOs request)
        {
            var category = await _categoryService.CreateCategoryAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = category.CategoryId }, category); // Return the created category with a 201 status code
        }

        // PUT api/<CategoriesController>/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategoryAsync(int id,[FromBody] UpdateCategoryDTOs request)
        {
            var result = await _categoryService.UpdateCategoryAsync(id, request);
            if(!result)
                return NotFound();
            return NoContent();
        }

        // DELETE api/<CategoriesController>/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _categoryService.DeleteCategoryAsync(id);
            if(!result)
                return NotFound();
            return NoContent();
        }
    }
}
