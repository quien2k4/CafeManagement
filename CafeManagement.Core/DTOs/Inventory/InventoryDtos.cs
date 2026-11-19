namespace CafeManagement.Core.DTOs.Inventory;

public class IngredientDto
{
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUom { get; set; } = "g";
    public decimal MinStockAlert { get; set; } = 500;
    public bool IsActive { get; set; } = true;
}

public class IngredientCreateRequest
{
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUom { get; set; } = "g";
    public decimal MinStockAlert { get; set; } = 500;
    public decimal InitialStock { get; set; } = 0;
    public int? CurrentBranchId { get; set; }
    public string? CreatorName { get; set; }
}

public class IngredientUpdateRequest
{
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUom { get; set; } = "g";
    public decimal MinStockAlert { get; set; } = 500;
}

public class BranchInventoryDto
{
    public int BranchInventoryId { get; set; }
    public int BranchId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUom { get; set; } = "g";
    public decimal QuantityOnHand { get; set; }
    public decimal MinStockAlert { get; set; }
}

public class ImportStockRequest
{
    public int BranchId { get; set; }
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string Note { get; set; } = string.Empty;
}

public class AdjustStockRequest
{
    public int BranchId { get; set; }
    public int IngredientId { get; set; }
    public decimal NewQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
}

public class InventoryTransactionDto
{
    public long TransactionId { get; set; }
    public int BranchId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUom { get; set; } = "g";
    public string TransactionType { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal BalanceAfter { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
