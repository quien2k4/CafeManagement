using CafeManagement.Core.DTOs.Reports;

namespace CafeManagement.Core.Utils;

public static class HolidayCalendarHelper
{
    /// <summary>
    /// Danh mục các ngày lễ và sự kiện đặc biệt tại Việt Nam
    /// </summary>
    public static List<HolidayInfo> GetHolidaysForYear(int year)
    {
        var list = new List<HolidayInfo>
        {
            // Dương lịch cố định
            new() { Date = new DateTime(year, 1, 1), HolidayName = "Tết Dương Lịch", Description = "Tết Tây - Nghỉ lễ toàn quốc, nhu cầu đi cafe gặp mặt đầu năm tăng mạnh", ExpectedSurgeMultiplier = 1.8, HighlightColor = "#C53030" },
            new() { Date = new DateTime(year, 2, 14), HolidayName = "Lễ Tình Nhân (Valentine)", Description = "Ngày lễ tình nhân, lượng khách đi đôi và nhóm tăng cao từ 18:00 - 22:00", ExpectedSurgeMultiplier = 1.7, HighlightColor = "#D53F8C" },
            new() { Date = new DateTime(year, 3, 8), HolidayName = "Quốc Tế Phụ Nữ (8/3)", Description = "Nhu cầu tặng đồ uống, họp mặt gia đình & công ty tăng cao", ExpectedSurgeMultiplier = 1.6, HighlightColor = "#DD6B20" },
            new() { Date = new DateTime(year, 4, 30), HolidayName = "Giải Phóng Miền Nam (30/4)", Description = "Kỳ nghỉ lễ lớn toàn quốc 30/4 - 1/5, khách gia đình và du lịch tăng vọt", ExpectedSurgeMultiplier = 2.0, HighlightColor = "#9B2C2C" },
            new() { Date = new DateTime(year, 5, 1), HolidayName = "Quốc Tế Lao Động (1/5)", Description = "Kỳ nghỉ lễ 1/5, lượng khách đông cả ngày", ExpectedSurgeMultiplier = 1.9, HighlightColor = "#9B2C2C" },
            new() { Date = new DateTime(year, 6, 1), HolidayName = "Quốc Tế Thiếu Nhi (1/6)", Description = "Gia đình có trẻ nhỏ đi cafe cuối tuần & tối", ExpectedSurgeMultiplier = 1.4, HighlightColor = "#3182CE" },
            new() { Date = new DateTime(year, 9, 2), HolidayName = "Quốc Khánh (2/9)", Description = "Kỳ nghỉ lễ Quốc Khánh, khách đi chơi đông cả ngày", ExpectedSurgeMultiplier = 1.9, HighlightColor = "#C53030" },
            new() { Date = new DateTime(year, 10, 20), HolidayName = "Phụ Nữ Việt Nam (20/10)", Description = "Họp mặt, tiệc nhẹ và giao lưu tăng cao vào chiều tối", ExpectedSurgeMultiplier = 1.5, HighlightColor = "#D53F8C" },
            new() { Date = new DateTime(year, 11, 20), HolidayName = "Nhà Giáo Việt Nam (20/11)", Description = "Học sinh, sinh viên và phụ huynh họp lớp, tri ân thầy cô", ExpectedSurgeMultiplier = 1.4, HighlightColor = "#3182CE" },
            new() { Date = new DateTime(year, 12, 24), HolidayName = "Đêm Giáng Sinh (Noel)", Description = "Lưu lượng khách cực đông từ 18:00 đến nửa đêm", ExpectedSurgeMultiplier = 2.2, HighlightColor = "#22543D" },
            new() { Date = new DateTime(year, 12, 25), HolidayName = "Lễ Giáng Sinh", Description = "Khách đi chơi Noel và chụp ảnh cafe", ExpectedSurgeMultiplier = 1.8, HighlightColor = "#22543D" },
            new() { Date = new DateTime(year, 12, 31), HolidayName = "Đêm Giao Thừa Dương Lịch", Description = "Khách đón Countdown giao thừa cuối năm", ExpectedSurgeMultiplier = 2.0, HighlightColor = "#C53030" }
        };

        // Các dịp Lễ Âm Lịch theo năm cụ thể
        if (year == 2024)
        {
            list.Add(new() { Date = new DateTime(2024, 2, 9), HolidayName = "30 Tết Giáp Thìn", Description = "Đêm giao thừa Tết Âm Lịch", ExpectedSurgeMultiplier = 1.8, HighlightColor = "#C53030" });
            list.Add(new() { Date = new DateTime(2024, 2, 10), HolidayName = "Mùng 1 Tết Âm Lịch", Description = "Khai xuân đầu năm", ExpectedSurgeMultiplier = 2.2, HighlightColor = "#C53030" });
            list.Add(new() { Date = new DateTime(2024, 4, 18), HolidayName = "Giỗ Tổ Hùng Vương (10/3 AL)", Description = "Nghỉ lễ Giỗ Tổ", ExpectedSurgeMultiplier = 1.6, HighlightColor = "#DD6B20" });
            list.Add(new() { Date = new DateTime(2024, 9, 17), HolidayName = "Tết Trung Thu (15/8 AL)", Description = "Đêm hội trăng rằm, gia đình đi chơi đông", ExpectedSurgeMultiplier = 1.5, HighlightColor = "#DD6B20" });
        }
        else if (year == 2025)
        {
            list.Add(new() { Date = new DateTime(2025, 1, 28), HolidayName = "29 Tết Ất Tỵ", Description = "Tất niên cuối năm", ExpectedSurgeMultiplier = 1.8, HighlightColor = "#C53030" });
            list.Add(new() { Date = new DateTime(2025, 1, 29), HolidayName = "Mùng 1 Tết Âm Lịch", Description = "Tết Nguyên Đán Ất Tỵ", ExpectedSurgeMultiplier = 2.2, HighlightColor = "#C53030" });
            list.Add(new() { Date = new DateTime(2025, 4, 7), HolidayName = "Giỗ Tổ Hùng Vương (10/3 AL)", Description = "Nghỉ lễ Giỗ Tổ", ExpectedSurgeMultiplier = 1.6, HighlightColor = "#DD6B20" });
            list.Add(new() { Date = new DateTime(2025, 10, 6), HolidayName = "Tết Trung Thu (15/8 AL)", Description = "Đêm hội trăng rằm", ExpectedSurgeMultiplier = 1.5, HighlightColor = "#DD6B20" });
        }
        else if (year == 2026)
        {
            list.Add(new() { Date = new DateTime(2026, 2, 16), HolidayName = "29 Tết Bính Ngọ", Description = "Tất niên cuối năm", ExpectedSurgeMultiplier = 1.8, HighlightColor = "#C53030" });
            list.Add(new() { Date = new DateTime(2026, 2, 17), HolidayName = "Mùng 1 Tết Âm Lịch", Description = "Tết Nguyên Đán Bính Ngọ", ExpectedSurgeMultiplier = 2.2, HighlightColor = "#C53030" });
            list.Add(new() { Date = new DateTime(2026, 4, 26), HolidayName = "Giỗ Tổ Hùng Vương (10/3 AL)", Description = "Nghỉ lễ Giỗ Tổ", ExpectedSurgeMultiplier = 1.6, HighlightColor = "#DD6B20" });
            list.Add(new() { Date = new DateTime(2026, 9, 25), HolidayName = "Tết Trung Thu (15/8 AL)", Description = "Tết Trung Thu", ExpectedSurgeMultiplier = 1.5, HighlightColor = "#DD6B20" });
        }
        else if (year == 2027)
        {
            list.Add(new() { Date = new DateTime(2027, 2, 5), HolidayName = "29 Tết Đinh Mùi", Description = "Tất niên cuối năm", ExpectedSurgeMultiplier = 1.8, HighlightColor = "#C53030" });
            list.Add(new() { Date = new DateTime(2027, 2, 6), HolidayName = "Mùng 1 Tết Âm Lịch", Description = "Tết Nguyên Đán Đinh Mùi", ExpectedSurgeMultiplier = 2.2, HighlightColor = "#C53030" });
            list.Add(new() { Date = new DateTime(2027, 4, 16), HolidayName = "Giỗ Tổ Hùng Vương (10/3 AL)", Description = "Nghỉ lễ Giỗ Tổ", ExpectedSurgeMultiplier = 1.6, HighlightColor = "#DD6B20" });
            list.Add(new() { Date = new DateTime(2027, 9, 15), HolidayName = "Tết Trung Thu (15/8 AL)", Description = "Tết Trung Thu", ExpectedSurgeMultiplier = 1.5, HighlightColor = "#DD6B20" });
        }

        return list.OrderBy(h => h.Date).ToList();
    }

    /// <summary>
    /// Kiểm tra 1 ngày cụ thể có phải là ngày Lễ không
    /// </summary>
    public static HolidayInfo? CheckHoliday(DateTime date)
    {
        var targetDate = date.Date;
        var holidays = GetHolidaysForYear(targetDate.Year);
        return holidays.FirstOrDefault(h => h.Date.Date == targetDate);
    }

    /// <summary>
    /// Lấy danh sách các ngày lễ trong khoảng thời gian [from, to]
    /// </summary>
    public static List<HolidayInfo> GetHolidaysInRange(DateTime from, DateTime to)
    {
        var result = new List<HolidayInfo>();
        for (int y = from.Year; y <= to.Year; y++)
        {
            var yearHolidays = GetHolidaysForYear(y);
            result.AddRange(yearHolidays.Where(h => h.Date.Date >= from.Date && h.Date.Date <= to.Date));
        }
        return result.OrderBy(h => h.Date).ToList();
    }
}
