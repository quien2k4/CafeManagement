using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Recipes;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Recipes;

public class RecipeService : IRecipeService
{
    private readonly CafeDbContext _db;

    public RecipeService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<RecipeDto?> GetCurrentRecipeByVariantIdAsync(int variantId)
    {
        var recipeVersion = await _db.RecipeVersions
            .AsNoTracking()
            .Include(rv => rv.Details)
                .ThenInclude(d => d.Ingredient)
            .Where(rv => rv.Recipe!.VariantId == variantId && rv.IsCurrent)
            .FirstOrDefaultAsync();

        if (recipeVersion == null) return null;

        return new RecipeDto
        {
            RecipeId = recipeVersion.RecipeId,
            VariantId = variantId,
            VersionNumber = recipeVersion.VersionNumber,
            EffectiveFrom = recipeVersion.EffectiveFrom,
            IsCurrent = recipeVersion.IsCurrent,
            Note = recipeVersion.Note,
            Details = recipeVersion.Details.Select(d => new RecipeDetailDto
            {
                RecipeDetailId = d.RecipeDetailId,
                IngredientId = d.IngredientId,
                IngredientCode = d.Ingredient?.IngredientCode ?? string.Empty,
                IngredientName = d.Ingredient?.IngredientName ?? string.Empty,
                Quantity = d.Quantity,
                Uom = d.Uom
            }).ToList()
        };
    }

    public async Task<RecipeDto> SaveRecipeAsync(SaveRecipeRequest req)
    {
        var variant = await _db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.VariantId == req.VariantId)
            ?? throw new InvalidOperationException($"Không tìm thấy biến thể món ID={req.VariantId}!");

        var recipe = await _db.Recipes
            .Include(r => r.Versions)
                .ThenInclude(v => v.Details)
            .FirstOrDefaultAsync(r => r.VariantId == req.VariantId);

        if (recipe == null)
        {
            recipe = new Recipe
            {
                VariantId = req.VariantId,
                Description = $"Công thức cho {variant.Product?.ProductName ?? "Sản phẩm"} ({variant.Size})"
            };
            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync();
        }

        // Đánh dấu các version cũ là không current
        foreach (var v in recipe.Versions)
        {
            v.IsCurrent = false;
        }

        int newVerNum = recipe.Versions.Count + 1;
        var newVersion = new RecipeVersion
        {
            RecipeId = recipe.RecipeId,
            VersionNumber = newVerNum,
            EffectiveFrom = DateTime.UtcNow,
            IsCurrent = true,
            Note = req.Note ?? $"Cập nhật định mức ngày {DateTime.Now:dd/MM/yyyy HH:mm}"
        };

        foreach (var item in req.Details)
        {
            newVersion.Details.Add(new RecipeDetail
            {
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                Uom = item.Uom
            });
        }

        _db.RecipeVersions.Add(newVersion);
        await _db.SaveChangesAsync();

        return (await GetCurrentRecipeByVariantIdAsync(req.VariantId))!;
    }
}
