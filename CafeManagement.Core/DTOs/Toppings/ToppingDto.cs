namespace CafeManagement.Core.DTOs.Toppings;

public class ToppingRecipeDetailDto
{
    public int ToppingRecipeDetailId { get; set; }
    public int ToppingId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "g";
}

public class ToppingDto
{
    public int ToppingId { get; set; }
    public string ToppingCode { get; set; } = string.Empty;
    public string ToppingName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ToppingRecipeDetailDto> RecipeDetails { get; set; } = [];
}

public class ToppingRecipeDetailItem
{
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "g";
}

public class ToppingCreateRequest
{
    public string ToppingCode { get; set; } = string.Empty;
    public string ToppingName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ToppingRecipeDetailItem> RecipeDetails { get; set; } = [];
}

public class ToppingUpdateRequest
{
    public string ToppingName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ToppingRecipeDetailItem> RecipeDetails { get; set; } = [];
}

public class ToppingRecommendationDto
{
    public int ToppingId { get; set; }
    public string ToppingCode { get; set; } = string.Empty;
    public string ToppingName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Percentage { get; set; } // Ví dụ: 42.5 nghĩa là 42.5%
    public int OrderCount { get; set; }     // Số lần món chính đi kèm topping này
    public string BadgeText => $"{Percentage:0.#}% khách chọn";
    public bool IsTopPick => Percentage >= 30;
}
