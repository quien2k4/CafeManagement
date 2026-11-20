using CommunityToolkit.Mvvm.ComponentModel;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.ViewModels;

public partial class ProductManagementItemViewModel : ObservableObject
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBadge))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    private bool _isActive = true;

    public string VariantsDisplay { get; set; } = string.Empty;

    public string StatusBadge => IsActive ? "✅ Đang kinh doanh" : "⛔ Tạm ngưng bán";
    public string StatusColor => IsActive ? "#27AE60" : "#E74C3C";

    public Product Product { get; set; } = null!;
}

public class RecipeDetailItemViewModel
{
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "g";

    public string DisplayText => $"{IngredientName}: {Quantity:N1} {Uom}";
}
