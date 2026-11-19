using CafeManagement.Core.DTOs.Recipes;

namespace CafeManagement.Api.Services.Interfaces;

public interface IRecipeService
{
    Task<RecipeDto?> GetCurrentRecipeByVariantIdAsync(int variantId);
    Task<RecipeDto> SaveRecipeAsync(SaveRecipeRequest req);
}
