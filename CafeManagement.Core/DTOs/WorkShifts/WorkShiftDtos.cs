namespace CafeManagement.Core.DTOs.WorkShifts;

public class WorkShiftDto
{
    public long ShiftId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int CashierUserId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = "Ca Sáng";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal CashSales { get; set; }
    public decimal VietQrSales { get; set; }
    public decimal TotalSales { get; set; }
    public int TotalOrders { get; set; }
    public decimal ExpectedClosingCash { get; set; }
    public decimal? ActualClosingCash { get; set; }
    public decimal? DifferenceAmount { get; set; }
    public string Status { get; set; } = "OPEN"; // OPEN, CLOSED
    public string? Notes { get; set; }
}

public class OpenShiftRequest
{
    public int BranchId { get; set; }
    public int CashierUserId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = "Ca Sáng";
    public decimal OpeningCash { get; set; }
    public string? Notes { get; set; }
}

public class CloseShiftRequest
{
    public long ShiftId { get; set; }
    public decimal ActualClosingCash { get; set; }
    public string? Notes { get; set; }
}

public class ZReportDto
{
    public long ShiftId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = "Ca Sáng";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalOrders { get; set; }
    public decimal CashSales { get; set; }
    public decimal VietQrSales { get; set; }
    public decimal TotalSales { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal CashAdded { get; set; }
    public decimal ExpectedClosingCash { get; set; }
    public decimal ActualClosingCash { get; set; }
    public decimal DifferenceAmount { get; set; }
    public string? Notes { get; set; }
}
