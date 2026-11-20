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
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "CASH";
    public string ItemsSummary { get; set; } = string.Empty;
    public decimal FinalAmount { get; set; }
    public string OrderStatus { get; set; } = "PAID";
    public decimal RefundedAmount { get; set; } = 0;
}

public class TopProductInHourDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public class HourlySalesStatDto
{
    public int Hour { get; set; }
    public string HourLabel { get; set; } = string.Empty;
    public string ShortHour { get; set; } = string.Empty;
    public int TotalCups { get; set; }
    public decimal TotalRevenue { get; set; }
    public int OrderCount { get; set; }
    public double HeatIntensity { get; set; }
    public string HeatColorHex { get; set; } = "#F8FAFC";
    public string TopProductsSummary { get; set; } = string.Empty;
    public List<TopProductInHourDto> TopProductsInHour { get; set; } = [];
}

public class DailyRevenueStatDto
{
    public DateTime Date { get; set; }
    public string DateFormatted { get; set; } = string.Empty;
    public string DayOfWeekName { get; set; } = string.Empty;
    public string ShortDayName { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCups { get; set; }
    public double HeightRatio { get; set; }
    public double ColumnHeight { get; set; } = 20;
    public string RevenueFormatted { get; set; } = string.Empty;
    public bool IsPeakDay { get; set; }
    public bool IsSelected { get; set; }
    public List<HourlySalesStatDto> HourlySales { get; set; } = [];
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

    public decimal MaxDailyRevenue { get; set; }
    public List<string> YAxisTickLabels { get; set; } = [];

    public List<TopProductStatDto> TopProducts { get; set; } = [];
    public List<OrderReportDto> Orders { get; set; } = [];
    public List<HourlySalesStatDto> HourlySales { get; set; } = [];
    public List<DailyRevenueStatDto> WeeklySales { get; set; } = [];
}
