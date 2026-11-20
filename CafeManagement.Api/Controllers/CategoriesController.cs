using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Categories;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories([FromQuery] bool activeOnly = false)
    {
        var list = await _categoryService.GetCategoriesAsync(activeOnly);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetCategoryById(int id)
    {
        var category = await _categoryService.GetCategoryByIdAsync(id);
        if (category == null) return NotFound(new { message = "Không tìm thấy danh mục!" });
        return Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CategoryCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CategoryName))
        {
            return BadRequest(new { message = "Tên danh mục không được để trống!" });
        }
        if (string.IsNullOrWhiteSpace(req.CategoryCode))
        {
            return BadRequest(new { message = "Mã danh mục không được để trống!" });
        }

        try
        {
            var created = await _categoryService.CreateCategoryAsync(req);
            return CreatedAtAction(nameof(GetCategoryById), new { id = created.CategoryId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, [FromBody] CategoryUpdateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CategoryName))
        {
            return BadRequest(new { message = "Tên danh mục không được để trống!" });
        }

        var updated = await _categoryService.UpdateCategoryAsync(id, req);
        if (updated == null)
        {
            return NotFound(new { message = "Không tìm thấy danh mục để cập nhật!" });
        }
        return Ok(updated);
    }
}
