using CafeManagement.Core.DTOs.Recipes;

namespace CafeManagement.Wpf.Services.Http;

public class RecipeApiClient
{
    private static readonly Lazy<RecipeApiClient> _instance = new(() => new RecipeApiClient());
    public static RecipeApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public RecipeApiClient() : this(ApiClient.Instance)
    {
    }

    public RecipeApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<RecipeDto?> GetCurrentRecipeByVariantIdAsync(int variantId)
    {
        return await _api.GetAsync<RecipeDto>($"/api/recipes/variants/{variantId}");
    }

    public async Task<RecipeDto?> SaveRecipeAsync(SaveRecipeRequest req)
    {
        return await _api.PostAsync<SaveRecipeRequest, RecipeDto>("/api/recipes", req);
    }
}
