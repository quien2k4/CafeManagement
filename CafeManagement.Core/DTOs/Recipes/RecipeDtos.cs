namespace CafeManagement.Core.DTOs.Recipes;

public class RecipeDetailDto
{
    public int RecipeDetailId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "g";
}

public class RecipeDto
{
    public int RecipeId { get; set; }
    public int VariantId { get; set; }
    public int VersionNumber { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public bool IsCurrent { get; set; }
    public string? Note { get; set; }
    public List<RecipeDetailDto> Details { get; set; } = [];
}

public class SaveRecipeDetailItem
{
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "g";
}

public class SaveRecipeRequest
{
    public int VariantId { get; set; }
    public string? Note { get; set; }
    public List<SaveRecipeDetailItem> Details { get; set; } = [];
}
