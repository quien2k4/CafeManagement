using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Categories;
using CafeManagement.Core.Models;

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

    public async Task<CategoryDto> CreateCategoryAsync(CategoryCreateRequest req)
    {
        var code = req.CategoryCode.Trim().ToUpper();
        var existing = await _db.ProductCategories.AnyAsync(c => c.CategoryCode == code);
        if (existing)
        {
            throw new InvalidOperationException($"Mã danh mục '{code}' đã tồn tại!");
        }

        var cat = new ProductCategory
        {
            CategoryCode = code,
            CategoryName = req.CategoryName.Trim(),
            DisplayOrder = req.DisplayOrder,
            IsActive = req.IsActive
        };

        _db.ProductCategories.Add(cat);
        await _db.SaveChangesAsync();

        return new CategoryDto
        {
            CategoryId = cat.CategoryId,
            CategoryCode = cat.CategoryCode,
            CategoryName = cat.CategoryName,
            DisplayOrder = cat.DisplayOrder,
            IsActive = cat.IsActive
        };
    }

    public async Task<CategoryDto?> UpdateCategoryAsync(int id, CategoryUpdateRequest req)
    {
        var cat = await _db.ProductCategories.FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat == null) return null;

        cat.CategoryName = req.CategoryName.Trim();
        cat.DisplayOrder = req.DisplayOrder;
        cat.IsActive = req.IsActive;

        await _db.SaveChangesAsync();

        return new CategoryDto
        {
            CategoryId = cat.CategoryId,
            CategoryCode = cat.CategoryCode,
            CategoryName = cat.CategoryName,
            DisplayOrder = cat.DisplayOrder,
            IsActive = cat.IsActive
        };
    }
}
