using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeManagement.Wpf.ViewModels.Reports;

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
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "CASH";
    public string PaymentMethodDisplay => PaymentMethod == "VIETQR" ? "VietQR (Chuyển khoản)" : "Tiền mặt";
    public string ItemsSummary { get; set; } = string.Empty;
    public decimal FinalAmount { get; set; }
    public string OrderStatus { get; set; } = "PAID";
    public decimal RefundedAmount { get; set; } = 0;

    public decimal RemainingRefundableAmount => Math.Max(0, FinalAmount - RefundedAmount);

    public string StatusText => OrderStatus switch
    {
        "REFUNDED" => "Đã hoàn tiền",
        "PARTIALLY_REFUNDED" => $"Hoàn 1 phần ({RefundedAmount:N0} đ)",
        "CANCELLED" => "Đã hủy đơn",
        _ => "Đã thanh toán"
    };

    public string StatusColor => OrderStatus switch
    {
        "REFUNDED" => "#E74C3C",
        "PARTIALLY_REFUNDED" => "#E67E22",
        "CANCELLED" => "#95A5A6",
        _ => "#27AE60"
    };

    public string StatusBadgeBackground => OrderStatus switch
    {
        "REFUNDED" => "#FDEDEC",
        "PARTIALLY_REFUNDED" => "#FDF2E9",
        "CANCELLED" => "#F2F4F4",
        _ => "#EAFAF1"
    };

    public bool CanRefund => OrderStatus != "REFUNDED" && OrderStatus != "CANCELLED" && RemainingRefundableAmount > 0;
}
