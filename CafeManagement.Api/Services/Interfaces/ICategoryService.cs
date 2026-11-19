using CafeManagement.Core.DTOs.Categories;

namespace CafeManagement.Api.Services.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetCategoriesAsync(bool activeOnly = false);
    Task<CategoryDto?> GetCategoryByIdAsync(int id);
}
