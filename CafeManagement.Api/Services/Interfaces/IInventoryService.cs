using CafeManagement.Core.DTOs.Inventory;

namespace CafeManagement.Api.Services.Interfaces;

public interface IInventoryService
{
    Task<List<BranchInventoryDto>> GetBranchInventoryAsync(int branchId);
    Task<List<IngredientDto>> GetIngredientsAsync(bool activeOnly = true);
    Task<IngredientDto> CreateIngredientAsync(IngredientCreateRequest req);
    Task<IngredientDto> UpdateIngredientAsync(int ingredientId, IngredientUpdateRequest req);
    Task<BranchInventoryDto> ImportStockAsync(ImportStockRequest req);
    Task<BranchInventoryDto> AdjustStockAsync(AdjustStockRequest req);
    Task<bool> CancelReceiptAsync(long transactionId);
    Task<List<InventoryTransactionDto>> GetTransactionsAsync(int branchId, int limit = 500);
}
