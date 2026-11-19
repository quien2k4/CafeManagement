using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Categories;

namespace CafeManagement.Api.Services.Categories;

public class CategoryService : ICategoryService
{
    private readonly CafeDbContext _db;

    public CategoryService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync(bool activeOnly = false)
    {
        var query = _db.ProductCategories.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CategoryDto
            {
                CategoryId = c.CategoryId,
                CategoryCode = c.CategoryCode,
                CategoryName = c.CategoryName,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive
            })
            .ToListAsync();
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
    {
        var c = await _db.ProductCategories.AsNoTracking().FirstOrDefaultAsync(x => x.CategoryId == id);
        if (c == null) return null;

        return new CategoryDto
        {
            CategoryId = c.CategoryId,
            CategoryCode = c.CategoryCode,
            CategoryName = c.CategoryName,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive
        };
    }
}
