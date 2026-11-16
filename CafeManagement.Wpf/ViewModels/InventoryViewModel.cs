using CommunityToolkit.Mvvm.ComponentModel;
using CafeManagement.Wpf.Models;

namespace CafeManagement.Wpf.ViewModels;

public partial class InventoryItemViewModel : ObservableObject
{
    public int BranchInventoryId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUom { get; set; } = "g";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLowStock))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StockPercent))]
    private decimal _quantityOnHand;

    [ObservableProperty]
    private decimal _minStockAlert;

    public bool IsLowStock => QuantityOnHand <= MinStockAlert;
    public string StatusText => IsLowStock ? "⚠️ Sắp hết hàng" : "✅ Còn đủ hàng";

    // Thanh đo phần trăm tồn kho (ước tính trên mức an toàn x3)
    public double StockPercent
    {
        get
        {
            if (MinStockAlert <= 0) return 100.0;
            double target = (double)(MinStockAlert * 3);
            double pct = ((double)QuantityOnHand / target) * 100.0;
            return Math.Min(100.0, Math.Max(0.0, pct));
        }
    }
}

public class InventoryTransactionItemViewModel
{
    public long TransactionId { get; set; }
    public int BranchId { get; set; }
    public int IngredientId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal BalanceAfter { get; set; }
    public string BaseUom { get; set; } = "g";
    public string ReferenceId { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public string ChangeDisplay => QuantityChange > 0 
        ? $"+{QuantityChange:N1} {BaseUom}" 
        : $"{QuantityChange:N1} {BaseUom}";

    public bool IsIncrease => QuantityChange > 0;
    public bool CanCancel => TransactionType == "GOODS_RECEIPT" && QuantityChange > 0;
    public bool IsCancelled => TransactionType == "RECEIPT_CANCELLED";

    public string TransactionTypeBadge => TransactionType switch
    {
        "GOODS_RECEIPT" => "📥 Nhập kho",
        "SALE_CONSUMPTION" => "☕ Bán hàng",
        "STOCK_ADJUSTMENT" => "⚖️ Điều chỉnh",
        "INITIAL_STOCK" => "🌱 Tồn ban đầu",
        "RECEIPT_CANCELLED" => "❌ Đã hủy",
        _ => TransactionType
    };

    public string TransactionColor => TransactionType switch
    {
        "GOODS_RECEIPT" => "#27AE60",
        "SALE_CONSUMPTION" => "#C0392B",
        "STOCK_ADJUSTMENT" => "#D35400",
        "INITIAL_STOCK" => "#2980B9",
        "RECEIPT_CANCELLED" => "#7F8C8D",
        _ => "#2C3E50"
    };

    public string TransactionBackground => TransactionType switch
    {
        "GOODS_RECEIPT" => "#EAFAF1",
        "SALE_CONSUMPTION" => "#FDEDEC",
        "STOCK_ADJUSTMENT" => "#FEF5E7",
        "INITIAL_STOCK" => "#EBF5FB",
        "RECEIPT_CANCELLED" => "#F2F4F4",
        _ => "#F8F9FA"
    };
}
