namespace CafeManagement.Core.DTOs.Reports;

public class TopProductStatDto
{
    public int Rank { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
    public double PercentOfTotal { get; set; }
}

public class OrderReportDto
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "CASH";
    public string ItemsSummary { get; set; } = string.Empty;
    public decimal FinalAmount { get; set; }
}

public class DashboardReportDto
{
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalCups { get; set; }
    public decimal AverageOrderValue { get; set; }

    public decimal CashRevenue { get; set; }
    public int CashCount { get; set; }
    public double CashPercent { get; set; }

    public decimal QrRevenue { get; set; }
    public int QrCount { get; set; }
    public double QrPercent { get; set; }

    public List<TopProductStatDto> TopProducts { get; set; } = [];
    public List<OrderReportDto> Orders { get; set; } = [];
}
