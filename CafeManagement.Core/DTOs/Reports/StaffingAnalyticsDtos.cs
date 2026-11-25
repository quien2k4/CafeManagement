namespace CafeManagement.Core.DTOs.Reports;

/// <summary>
/// Thông tin ngày Lễ / Dịp đặc biệt để cảnh báo lưu lượng tăng đột biến
/// </summary>
public class HolidayInfo
{
    public DateTime Date { get; set; }
    public string DateFormatted => Date.ToString("dd/MM/yyyy");
    public string HolidayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double ExpectedSurgeMultiplier { get; set; } = 1.5; // Dự kiến tăng 150% (x1.5)
    public bool IsHoliday { get; set; } = true;
    public string HighlightColor { get; set; } = "#E53E3E"; // Màu đậm cảnh báo (Đỏ đô / Cam đậm)
}

/// <summary>
/// Lưu lượng đơn hàng và đề xuất nhân sự cho từng khung giờ (0h - 23h)
/// </summary>
public class HourlyStaffingTrafficDto
{
    public int Hour { get; set; }
    public string HourRangeLabel { get; set; } = string.Empty; // e.g. "17:00 - 18:00"
    public string ShortHourLabel => $"{Hour:D2}h";
    public double AvgOrdersPerHour { get; set; }
    public int TotalOrders { get; set; }
    public double AvgCupsPerHour { get; set; }
    public decimal AvgRevenuePerHour { get; set; }

    /// <summary>
    /// LOW (Vắng), NORMAL (Bình thường), HIGH (Đông), PEAK (Giờ cao điểm)
    /// </summary>
    public string TrafficLevel { get; set; } = "NORMAL";
    public bool IsPeak { get; set; }

    /// <summary>
    /// Màu biểu đồ nhiệt Heatmap: Xám -> Xanh -> Cam -> Đỏ rực
    /// </summary>
    public string HeatColorHex { get; set; } = "#4A5568";
    public double HeightRatio { get; set; } // 0.0 - 1.0 tỉ lệ vẽ cột
    public double BarHeight => Math.Max(12, HeightRatio * 180); // Chiều cao thanh cột UI

    /// <summary>
    /// Đề xuất số lượng nhân sự
    /// </summary>
    public int RecommendedStaffCount { get; set; } = 1;
    public string StaffBreakdown { get; set; } = "1 Nhân sự"; // Ví dụ: "1 Thu ngân + 2 Pha chế"
}

/// <summary>
/// Thẻ khuyến nghị cố vấn thông minh hỗ trợ người quản lý ra quyết định
/// </summary>
public class StaffingRecommendationItemDto
{
    public int RecommendationId { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// INCREASE_STAFF (Cần tăng), DECREASE_STAFF (Nên giảm/tối ưu), OPTIMAL (Đạt chuẩn), HOLIDAY_WARNING (Cảnh báo Lễ)
    /// </summary>
    public string RecommendationType { get; set; } = "OPTIMAL";
    public string TimeRange { get; set; } = string.Empty; // e.g. "17:00 – 20:00"
    public double AvgOrdersPerHour { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
    public int SuggestedStaff { get; set; }

    /// <summary>
    /// CRITICAL (Đỏ đậm), WARNING (Cam đậm), INFO (Xanh dương), SUCCESS (Xanh lá)
    /// </summary>
    public string Severity { get; set; } = "INFO";
    public string BadgeColor { get; set; } = "#3182CE";
    public string? HolidayNote { get; set; }
}

/// <summary>
/// Toàn bộ kết quả phân tích lưu lượng & nhân sự của Chi nhánh
/// </summary>
public class BranchStaffingAnalyticsResponse
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int DaysAnalyzed { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalOrdersAnalyzed { get; set; }
    public decimal TotalRevenueAnalyzed { get; set; }
    public double OverallAvgOrdersPerHour { get; set; }

    // Giờ cao điểm và thấp điểm
    public string PeakTimeRange { get; set; } = string.Empty;
    public double PeakAvgOrders { get; set; }
    public string OffPeakTimeRange { get; set; } = string.Empty;
    public double OffPeakAvgOrders { get; set; }

    // Thông tin các ngày lễ sắp diễn ra trong khoảng phân tích / tháng
    public List<HolidayInfo> UpcomingHolidays { get; set; } = [];

    // Danh sách 24 khung giờ
    public List<HourlyStaffingTrafficDto> HourlyTraffics { get; set; } = [];

    // Danh sách các khuyến nghị điều phối nhân sự
    public List<StaffingRecommendationItemDto> Recommendations { get; set; } = [];
}
