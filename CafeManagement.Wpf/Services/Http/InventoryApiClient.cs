using CafeManagement.Core.DTOs.Inventory;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Services.Http;

public class InventoryApiClient
{
    private static readonly Lazy<InventoryApiClient> _instance = new(() => new InventoryApiClient());
    public static InventoryApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public InventoryApiClient() : this(ApiClient.Instance)
    {
    }

    public InventoryApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<List<BranchInventoryDto>> GetBranchInventoryAsync(int branchId)
    {
        try
        {
            var list = await _api.GetAsync<List<BranchInventoryDto>>($"/api/inventory/stocks?branchId={branchId}");
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<Ingredient>> GetIngredientsAsync(bool activeOnly = true)
    {
        try
        {
            var dtos = await _api.GetAsync<List<IngredientDto>>($"/api/inventory/ingredients?activeOnly={activeOnly}");
            return dtos?.Select(d => new Ingredient
            {
                IngredientId = d.IngredientId,
                IngredientCode = d.IngredientCode,
                IngredientName = d.IngredientName,
                BaseUom = d.BaseUom,
                MinStockAlert = d.MinStockAlert,
                IsActive = d.IsActive
            }).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<IngredientDto?> CreateIngredientAsync(IngredientCreateRequest req)
    {
        return await _api.PostAsync<IngredientCreateRequest, IngredientDto>("/api/inventory/ingredients", req);
    }

    public async Task<IngredientDto?> UpdateIngredientAsync(int id, IngredientUpdateRequest req)
    {
        return await _api.PutAsync<IngredientUpdateRequest, IngredientDto>($"/api/inventory/ingredients/{id}", req);
    }

    public async Task<BranchInventoryDto?> ImportStockAsync(ImportStockRequest req)
    {
        return await _api.PostAsync<ImportStockRequest, BranchInventoryDto>("/api/inventory/import", req);
    }

    public async Task<BranchInventoryDto?> AdjustStockAsync(AdjustStockRequest req)
    {
        return await _api.PostAsync<AdjustStockRequest, BranchInventoryDto>("/api/inventory/adjust", req);
    }

    public async Task<bool> CancelReceiptAsync(long transactionId)
    {
        try
        {
            return await _api.PostAsync($"/api/inventory/transactions/{transactionId}/cancel", new { });
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<InventoryTransactionDto>> GetTransactionsAsync(int branchId, int limit = 500)
    {
        try
        {
            var list = await _api.GetAsync<List<InventoryTransactionDto>>($"/api/inventory/transactions?branchId={branchId}&limit={limit}");
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }
}
