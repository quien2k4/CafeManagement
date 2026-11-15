using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeManagement.Wpf.ViewModels;

public class TopProductItemViewModel
{
    public int Rank { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
    public double PercentOfTotal { get; set; }

    public string RankBadge => Rank switch
    {
        1 => "🥇 1",
        2 => "🥈 2",
        3 => "🥉 3",
        _ => $"#{Rank}"
    };
}

public class OrderReportItemViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "CASH";
    public string PaymentMethodDisplay => PaymentMethod == "VIETQR" ? "VietQR (Chuyển khoản)" : "Tiền mặt";
    public string ItemsSummary { get; set; } = string.Empty;
    public decimal FinalAmount { get; set; }
    public string StatusText => "Đã thanh toán";
}
