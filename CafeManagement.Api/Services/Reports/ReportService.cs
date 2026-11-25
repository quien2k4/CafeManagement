using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Reports;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Reports;

public class ReportService : IReportService
{
    private readonly CafeDbContext _db;

    public ReportService(CafeDbContext db)
    {
        _db = db;
    }

    private static bool _tableChecked = false;

    private async Task EnsureRefundTableExistsAsync()
    {
        if (_tableChecked) return;
        try
        {
            await _db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderRefunds')
                BEGIN
                    CREATE TABLE dbo.OrderRefunds (
                        RefundId BIGINT IDENTITY(1,1) PRIMARY KEY,
                        OrderId BIGINT NOT NULL,
                        PaymentId BIGINT NULL,
                        RefundAmount DECIMAL(18,2) NOT NULL,
                        RefundMethod VARCHAR(20) NOT NULL DEFAULT 'CASH',
                        RefundReason NVARCHAR(255) NOT NULL,
                        Status VARCHAR(20) NOT NULL DEFAULT 'COMPLETED',
                        ProcessedByUserId INT NOT NULL DEFAULT 1,
                        ProcessedByName NVARCHAR(100) NOT NULL DEFAULT '',
                        RefundedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                        CONSTRAINT FK_OrderRefunds_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId) ON DELETE CASCADE
                    );
                END
            ");
            _tableChecked = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EnsureRefundTableExistsAsync error: {ex.Message}");
        }
    }

    public async Task<DashboardReportDto> GetDashboardReportAsync(int? branchId = null, string dateFilter = "ALL", int days = 7, DateTime? fromDate = null, DateTime? toDate = null)
    {
        await EnsureRefundTableExistsAsync();

        if (days < 1) days = 7;
        if (days > 60) days = 60;

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Variant)
                    .ThenInclude(v => v!.Product)
            .Include(o => o.Payments)
            .Include(o => o.Refunds)
            .Include(o => o.Branch)
            .Where(o => o.OrderStatus == "PAID" || o.OrderStatus == "PREPARING" || o.OrderStatus == "READY" || o.OrderStatus == "SERVED" || o.OrderStatus == "PARTIALLY_REFUNDED" || o.OrderStatus == "REFUNDED");

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(o => o.BranchId == branchId.Value);
        }

        var now = DateTime.UtcNow;
        if (fromDate.HasValue && toDate.HasValue)
        {
            var f = fromDate.Value.Date;
            var t = toDate.Value.Date;
            if (f > t) (f, t) = (t, f);
            if ((t - f).TotalDays > 90) f = t.AddDays(-90);

            var startUtc = f.AddHours(-7);
            var endUtc = t.AddDays(1).AddHours(-7);
            query = query.Where(o => o.CreatedAt >= startUtc && o.CreatedAt < endUtc);
        }
        else if (dateFilter == "TODAY")
        {
            var todayStart = DateTime.UtcNow.Date;
            query = query.Where(o => o.CreatedAt >= todayStart);
        }
        else if (dateFilter == "WEEK")
        {
            var weekStart = DateTime.UtcNow.Date.AddDays(-7);
            query = query.Where(o => o.CreatedAt >= weekStart);
        }
        else if (dateFilter == "MONTH")
        {
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            query = query.Where(o => o.CreatedAt >= monthStart);
        }

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

        int totalOrders = orders.Count;
        decimal totalRevenue = orders.Sum(o => o.FinalAmount);
        int totalCups = orders.Sum(o => o.Items.Sum(i => i.Quantity));

        var allPayments = orders.SelectMany(o => o.Payments).ToList();
        var cashPayments = allPayments.Where(p => p.PaymentMethod == "CASH").ToList();
        var qrPayments = allPayments.Where(p => p.PaymentMethod == "VIETQR").ToList();

        decimal cashRevenue = cashPayments.Sum(p => p.Amount);
        int cashCount = cashPayments.Count;

        decimal qrRevenue = qrPayments.Sum(p => p.Amount);
        int qrCount = qrPayments.Count;

        // Trừ các khoản đã hoàn tiền để phản ánh doanh thu thực tế chính xác
        decimal totalRefunds = orders.SelectMany(o => o.Refunds).Where(r => r.Status == "COMPLETED").Sum(r => r.RefundAmount);
        decimal cashRefunds = orders.SelectMany(o => o.Refunds).Where(r => r.Status == "COMPLETED" && r.RefundMethod == "CASH").Sum(r => r.RefundAmount);
        decimal qrRefunds = orders.SelectMany(o => o.Refunds).Where(r => r.Status == "COMPLETED" && r.RefundMethod == "VIETQR").Sum(r => r.RefundAmount);

        decimal netRevenue = Math.Max(0, totalRevenue - totalRefunds);
        decimal netCashRevenue = Math.Max(0, cashRevenue - cashRefunds);
        decimal netQrRevenue = Math.Max(0, qrRevenue - qrRefunds);
        decimal totalNetPayment = netCashRevenue + netQrRevenue;

        decimal averageOrderValue = totalOrders > 0 ? Math.Round(netRevenue / totalOrders, 0) : 0;
        double cashPercent = totalNetPayment > 0 ? Math.Round((double)(netCashRevenue / totalNetPayment) * 100.0, 1) : 50;
        double qrPercent = totalNetPayment > 0 ? Math.Round((double)(netQrRevenue / totalNetPayment) * 100.0, 1) : 50;

        var productSales = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => i.Variant?.Product?.ProductName ?? "Món nước")
            .Select(g => new
            {
                ProductName = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.SubTotal)
            })
            .OrderByDescending(x => x.Quantity)
            .Take(10)
            .ToList();

        int rank = 1;
        var topList = productSales.Select(p => new TopProductStatDto
        {
            Rank = rank++,
            ProductName = p.ProductName,
            TotalQuantity = p.Quantity,
            TotalRevenue = p.Revenue,
            PercentOfTotal = totalRevenue > 0 ? Math.Round((double)(p.Revenue / totalRevenue) * 100.0, 1) : 0
        }).ToList();

        var cashiers = await _db.UserAccounts
            .Include(u => u.Employee)
            .ToDictionaryAsync(u => u.UserId, u => u.Employee?.FullName ?? u.Username);

        var orderReports = orders.Select(o =>
        {
            string cName = cashiers.TryGetValue(o.CashierUserId, out var cn) ? cn : $"Thu ngân #{o.CashierUserId}";
            string pMethod = o.Payments.FirstOrDefault()?.PaymentMethod ?? "CASH";
            string summary = string.Join(", ", o.Items.Select(i => $"{i.Quantity}x {i.Variant?.Product?.ProductName}"));
            decimal refunded = o.Refunds.Where(r => r.Status == "COMPLETED").Sum(r => r.RefundAmount);

            return new OrderReportDto
            {
                OrderId = o.OrderId,
                OrderNumber = o.OrderNumber,
                CreatedAt = o.CreatedAt,
                CashierName = cName,
                BranchName = o.Branch?.BranchName ?? "Chi nhánh",
                PaymentMethod = pMethod,
                ItemsSummary = summary,
                FinalAmount = o.FinalAmount,
                OrderStatus = o.OrderStatus,
                RefundedAmount = refunded
            };
        }).ToList();

        // 1. TÍNH TOÁN BIỂU ĐỒ NHIỆT KHUNG GIỜ TOÀN BỘ ĐỢT (06:00 đến 23:00)
        var hourlyStats = BuildHourlyStats(orders);

        // 2. TÍNH TOÁN BIỂU ĐỒ DOANH THU CÁC NGÀY (DAILY SALES)
        var weeklyStats = new List<DailyRevenueStatDto>();
        var localToday = DateTime.UtcNow.AddHours(7).Date;

        DateTime rangeStart;
        DateTime rangeEnd;

        if (fromDate.HasValue && toDate.HasValue)
        {
            rangeStart = fromDate.Value.Date;
            rangeEnd = toDate.Value.Date;
            if (rangeStart > rangeEnd) (rangeStart, rangeEnd) = (rangeEnd, rangeStart);
            if ((rangeEnd - rangeStart).TotalDays > 90) rangeStart = rangeEnd.AddDays(-90);
        }
        else
        {
            rangeEnd = localToday;
            rangeStart = localToday.AddDays(-(days - 1));
        }

        var startQueryUtc = rangeStart.AddHours(-7);
        var endQueryUtc = rangeEnd.AddDays(1).AddHours(-7);

        var weeklyOrdersQuery = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Variant)
                    .ThenInclude(v => v!.Product)
            .Where(o => o.OrderStatus == "PAID" || o.OrderStatus == "PREPARING" || o.OrderStatus == "READY" || o.OrderStatus == "SERVED" || o.OrderStatus == "PARTIALLY_REFUNDED" || o.OrderStatus == "REFUNDED")
            .Where(o => o.CreatedAt >= startQueryUtc && o.CreatedAt < endQueryUtc);

        if (branchId.HasValue && branchId.Value > 0)
        {
            weeklyOrdersQuery = weeklyOrdersQuery.Where(o => o.BranchId == branchId.Value);
        }

        var weeklyOrders = await weeklyOrdersQuery.ToListAsync();

        for (var targetDate = rangeStart; targetDate <= rangeEnd; targetDate = targetDate.AddDays(1))
        {
            var dayOrders = weeklyOrders.Where(o => o.CreatedAt.AddHours(7).Date == targetDate).ToList();

            decimal dayRev = dayOrders.Sum(o => o.FinalAmount);
            int dayOrdersCount = dayOrders.Count;
            int dayCups = dayOrders.Sum(o => o.Items.Sum(x => x.Quantity));

            string shortDay = targetDate.DayOfWeek switch
            {
                DayOfWeek.Monday => "T2",
                DayOfWeek.Tuesday => "T3",
                DayOfWeek.Wednesday => "T4",
                DayOfWeek.Thursday => "T5",
                DayOfWeek.Friday => "T6",
                DayOfWeek.Saturday => "T7",
                DayOfWeek.Sunday => "CN",
                _ => ""
            };

            string dayOfWeekName = targetDate.DayOfWeek switch
            {
                DayOfWeek.Monday => "Thứ Hai",
                DayOfWeek.Tuesday => "Thứ Ba",
                DayOfWeek.Wednesday => "Thứ Tư",
                DayOfWeek.Thursday => "Thứ Năm",
                DayOfWeek.Friday => "Thứ Sáu",
                DayOfWeek.Saturday => "Thứ Bảy",
                DayOfWeek.Sunday => "Chủ Nhật",
                _ => ""
            };

            string revFormatted;
            if (dayRev >= 1_000_000)
                revFormatted = (dayRev / 1_000_000m).ToString("0.#") + " Tr";
            else if (dayRev >= 1_000)
                revFormatted = (dayRev / 1_000m).ToString("0") + "k";
            else
                revFormatted = dayRev.ToString("N0") + "đ";

            // Tính toán biểu đồ nhiệt riêng của ngày này:
            var dayHourly = BuildHourlyStats(dayOrders);

            weeklyStats.Add(new DailyRevenueStatDto
            {
                Date = targetDate,
                DateFormatted = targetDate.ToString("dd/MM"),
                DayOfWeekName = dayOfWeekName,
                ShortDayName = shortDay,
                TotalRevenue = dayRev,
                TotalOrders = dayOrdersCount,
                TotalCups = dayCups,
                RevenueFormatted = revFormatted,
                HourlySales = dayHourly
            });
        }

        decimal maxDailyRev = weeklyStats.Count > 0 ? weeklyStats.Max(w => w.TotalRevenue) : 0;
        decimal yMax = maxDailyRev > 0 ? Math.Ceiling(maxDailyRev / 100_000m) * 100_000m : 1_000_000m;
        if (yMax <= 0) yMax = 1_000_000m;
        decimal tickStep = yMax / 4m;

        var yAxisTicks = new List<string>
        {
            FormatMoneyShort(yMax),
            FormatMoneyShort(tickStep * 3),
            FormatMoneyShort(tickStep * 2),
            FormatMoneyShort(tickStep * 1),
            "0 đ"
        };

        foreach (var w in weeklyStats)
        {
            w.HeightRatio = yMax > 0 ? (double)(w.TotalRevenue / yMax) : 0;
            // Chiều cao thân cột tối đa 125px (nằm trọn vẹn trong vùng vẽ 150px mà không bị tràn nhãn tiền)
            w.ColumnHeight = Math.Max(10, Math.Round(w.HeightRatio * 125, 1));
            w.IsPeakDay = (w.TotalRevenue == maxDailyRev && w.TotalRevenue > 0);
        }

        return new DashboardReportDto
        {
            TotalOrders = totalOrders,
            TotalRevenue = netRevenue,
            TotalCups = totalCups,
            AverageOrderValue = averageOrderValue,
            CashRevenue = netCashRevenue,
            CashCount = cashCount,
            CashPercent = cashPercent,
            QrRevenue = netQrRevenue,
            QrCount = qrCount,
            QrPercent = qrPercent,
            MaxDailyRevenue = maxDailyRev,
            YAxisTickLabels = yAxisTicks,
            TopProducts = topList,
            Orders = orderReports,
            HourlySales = hourlyStats,
            WeeklySales = weeklyStats
        };
    }

    private static List<HourlySalesStatDto> BuildHourlyStats(List<Order> sourceOrders)
    {
        var result = new List<HourlySalesStatDto>();
        int maxCupsInHour = 1;

        for (int h = 6; h <= 23; h++)
        {
            var inHour = sourceOrders.Where(o => o.CreatedAt.AddHours(7).Hour == h).ToList();
            int cups = inHour.Sum(o => o.Items.Sum(i => i.Quantity));
            if (cups > maxCupsInHour) maxCupsInHour = cups;
        }

        for (int h = 6; h <= 23; h++)
        {
            var inHour = sourceOrders.Where(o => o.CreatedAt.AddHours(7).Hour == h).ToList();
            int cups = inHour.Sum(o => o.Items.Sum(i => i.Quantity));
            decimal rev = inHour.Sum(o => o.FinalAmount);
            int count = inHour.Count;

            double intensity = maxCupsInHour > 0 ? (double)cups / maxCupsInHour : 0;
            string colorHex = "#F8FAFC";
            if (cups > 0)
            {
                if (intensity <= 0.25) colorHex = "#FEF3C7";      // Vàng nhạt
                else if (intensity <= 0.50) colorHex = "#FED7AA"; // Cam nhạt
                else if (intensity <= 0.75) colorHex = "#FB923C"; // Cam vừa
                else colorHex = "#EA580C";                       // Đỏ cam rực
            }

            var topInHour = inHour
                .SelectMany(o => o.Items)
                .GroupBy(i => i.Variant?.Product?.ProductName ?? "Món")
                .Select(g => new TopProductInHourDto
                {
                    ProductName = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.SubTotal)
                })
                .OrderByDescending(x => x.Quantity)
                .Take(3)
                .ToList();

            string topSummary = topInHour.Count > 0
                ? string.Join(", ", topInHour.Select(tp => $"{tp.ProductName} ({tp.Quantity} ly)"))
                : "Chưa có món bán";

            result.Add(new HourlySalesStatDto
            {
                Hour = h,
                HourLabel = $"{h:00}:00 - {h + 1:00}:00",
                ShortHour = $"{h}h",
                TotalCups = cups,
                TotalRevenue = rev,
                OrderCount = count,
                HeatIntensity = intensity,
                HeatColorHex = colorHex,
                TopProductsInHour = topInHour,
                TopProductsSummary = topSummary
            });
        }

        return result;
    }

    public async Task<BranchStaffingAnalyticsResponse> GetStaffingAnalyticsAsync(int? branchId = null, int days = 14, string dayType = "ALL", DateTime? fromDate = null, DateTime? toDate = null)
    {
        await EnsureRefundTableExistsAsync();

        if (days < 1) days = 14;
        if (days > 90) days = 90;

        DateTime end = toDate ?? DateTime.UtcNow;
        DateTime start = fromDate ?? end.AddDays(-days);

        int daysAnalyzed = Math.Max(1, (int)(end.Date - start.Date).TotalDays + 1);

        // Lấy tên chi nhánh
        string branchName = "Tất cả chi nhánh";
        if (branchId.HasValue && branchId.Value > 0)
        {
            var b = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == branchId.Value);
            if (b != null) branchName = b.BranchName;
        }

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CreatedAt >= start && o.CreatedAt <= end && o.OrderStatus != "CANCELLED");

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(o => o.BranchId == branchId.Value);
        }

        var orders = await query.ToListAsync();

        // Lọc theo Ngày thường (T2-T6) hoặc Cuối tuần (T7, CN) nếu có chọn
        if (dayType.Equals("WEEKDAY", StringComparison.OrdinalIgnoreCase))
        {
            orders = orders.Where(o => o.CreatedAt.DayOfWeek != DayOfWeek.Saturday && o.CreatedAt.DayOfWeek != DayOfWeek.Sunday).ToList();
        }
        else if (dayType.Equals("WEEKEND", StringComparison.OrdinalIgnoreCase))
        {
            orders = orders.Where(o => o.CreatedAt.DayOfWeek == DayOfWeek.Saturday || o.CreatedAt.DayOfWeek == DayOfWeek.Sunday).ToList();
        }

        int totalOrders = orders.Count;
        decimal totalRevenue = orders.Sum(o => o.FinalAmount);

        // Gom nhóm theo 24 giờ
        var hourlyList = new List<HourlyStaffingTrafficDto>();
        double maxOrdersFound = 0;

        for (int h = 0; h < 24; h++)
        {
            var inHour = orders.Where(o => o.CreatedAt.Hour == h).ToList();
            int count = inHour.Count;
            double avgOrders = Math.Round((double)count / daysAnalyzed, 1);
            int cups = inHour.SelectMany(o => o.Items).Sum(i => i.Quantity);
            double avgCups = Math.Round((double)cups / daysAnalyzed, 1);
            decimal rev = inHour.Sum(o => o.FinalAmount);
            decimal avgRev = daysAnalyzed > 0 ? Math.Round(rev / daysAnalyzed, 0) : 0;

            if (avgOrders > maxOrdersFound) maxOrdersFound = avgOrders;

            // Xác định đề xuất nhân sự
            int staffCount;
            string breakdown;
            if (avgOrders <= 0)
            {
                staffCount = 0;
                breakdown = "Đóng cửa / Chưa hoạt động";
            }
            else if (avgOrders <= 10)
            {
                staffCount = 1;
                breakdown = "1 Nhân sự (Kiêm nhiệm Thu ngân & Pha chế)";
            }
            else if (avgOrders <= 22)
            {
                staffCount = 2;
                breakdown = "2 Nhân sự (1 Thu ngân, 1 Pha chế)";
            }
            else if (avgOrders <= 35)
            {
                staffCount = 3;
                breakdown = "3 Nhân sự (1 Thu ngân, 2 Pha chế)";
            }
            else if (avgOrders <= 50)
            {
                staffCount = 4;
                breakdown = "4 Nhân sự (1 Thu ngân, 2 Pha chế, 1 Tiếp thực)";
            }
            else
            {
                staffCount = 5;
                breakdown = "5 Nhân sự (1 Thu ngân, 3 Pha chế, 1 Phục vụ bàn)";
            }

            hourlyList.Add(new HourlyStaffingTrafficDto
            {
                Hour = h,
                HourRangeLabel = $"{h:00}:00 - {h + 1:00}:00",
                TotalOrders = count,
                AvgOrdersPerHour = avgOrders,
                AvgCupsPerHour = avgCups,
                AvgRevenuePerHour = avgRev,
                RecommendedStaffCount = staffCount,
                StaffBreakdown = breakdown
            });
        }

        // Tính toán tỉ lệ chiều cao cột & phân loại mức độ đông khách
        double overallAvg = hourlyList.Where(x => x.Hour >= 7 && x.Hour <= 22).Select(x => x.AvgOrdersPerHour).DefaultIfEmpty(0).Average();
        double peakThreshold = Math.Max(12.0, overallAvg * 1.35);

        foreach (var item in hourlyList)
        {
            item.HeightRatio = maxOrdersFound > 0 ? (item.AvgOrdersPerHour / maxOrdersFound) : 0;

            if (item.AvgOrdersPerHour >= peakThreshold && item.AvgOrdersPerHour >= 8.0)
            {
                item.TrafficLevel = "PEAK";
                item.IsPeak = true;
                item.HeatColorHex = "#E53E3E"; // Đỏ rực
            }
            else if (item.AvgOrdersPerHour >= 12.0)
            {
                item.TrafficLevel = "HIGH";
                item.HeatColorHex = "#DD6B20"; // Cam
            }
            else if (item.AvgOrdersPerHour >= 4.0)
            {
                item.TrafficLevel = "NORMAL";
                item.HeatColorHex = "#3182CE"; // Xanh
            }
            else
            {
                item.TrafficLevel = "LOW";
                item.HeatColorHex = "#718096"; // Xám
            }
        }

        // Tìm giờ cao điểm nhất và thấp điểm nhất trong khung giờ mở cửa (7h - 22h)
        var operatingHours = hourlyList.Where(x => x.Hour >= 7 && x.Hour <= 22).ToList();
        var peakItem = operatingHours.OrderByDescending(x => x.AvgOrdersPerHour).FirstOrDefault();
        var offPeakItem = operatingHours.OrderBy(x => x.AvgOrdersPerHour).FirstOrDefault();

        // Danh sách ngày lễ sắp tới trong 30 ngày
        var upcomingHolidays = CafeManagement.Core.Utils.HolidayCalendarHelper.GetHolidaysInRange(DateTime.Today, DateTime.Today.AddDays(30));

        // Sinh danh sách khuyến nghị cố vấn thông minh
        var recommendations = new List<StaffingRecommendationItemDto>();
        int recId = 1;

        // 1. Khuyến nghị TĂNG nhân sự nếu có giờ cao điểm
        var peakHours = hourlyList.Where(x => (x.TrafficLevel == "PEAK" || x.TrafficLevel == "HIGH") && x.Hour >= 7 && x.Hour <= 22).ToList();
        if (peakHours.Any())
        {
            // Nhóm các giờ cao điểm liên tiếp
            int startPeak = peakHours.First().Hour;
            int endPeak = peakHours.First().Hour;
            double maxPeakAvg = peakHours.First().AvgOrdersPerHour;
            int maxStaffReq = peakHours.First().RecommendedStaffCount;
            string bestBreakdown = peakHours.First().StaffBreakdown;

            for (int i = 1; i < peakHours.Count; i++)
            {
                if (peakHours[i].Hour == endPeak + 1)
                {
                    endPeak = peakHours[i].Hour;
                    if (peakHours[i].AvgOrdersPerHour > maxPeakAvg)
                    {
                        maxPeakAvg = peakHours[i].AvgOrdersPerHour;
                        maxStaffReq = peakHours[i].RecommendedStaffCount;
                        bestBreakdown = peakHours[i].StaffBreakdown;
                    }
                }
                else
                {
                    // Đóng cụm trước
                    recommendations.Add(new StaffingRecommendationItemDto
                    {
                        RecommendationId = recId++,
                        BranchId = branchId ?? 0,
                        BranchName = branchName,
                        RecommendationType = "INCREASE_STAFF",
                        TimeRange = $"{startPeak:00}:00 – {endPeak + 1:00}:00",
                        AvgOrdersPerHour = maxPeakAvg,
                        SuggestedStaff = maxStaffReq,
                        Title = $"🔥 Tăng cường nhân sự tại {branchName} khung {startPeak:00}:00 – {endPeak + 1:00}:00",
                        ActionDescription = $"Lưu lượng đạt trung bình {maxPeakAvg:0.0} đơn/giờ (Cao điểm). Khuyến nghị: Bố trí tối thiểu {maxStaffReq} nhân sự ({bestBreakdown}) để đảm bảo tốc độ ra món dưới 5 phút, tránh khách chờ dồn ứ.",
                        Severity = "CRITICAL",
                        BadgeColor = "#E53E3E"
                    });

                    startPeak = peakHours[i].Hour;
                    endPeak = peakHours[i].Hour;
                    maxPeakAvg = peakHours[i].AvgOrdersPerHour;
                    maxStaffReq = peakHours[i].RecommendedStaffCount;
                    bestBreakdown = peakHours[i].StaffBreakdown;
                }
            }

            // Thêm cụm cuối
            recommendations.Add(new StaffingRecommendationItemDto
            {
                RecommendationId = recId++,
                BranchId = branchId ?? 0,
                BranchName = branchName,
                RecommendationType = "INCREASE_STAFF",
                TimeRange = $"{startPeak:00}:00 – {endPeak + 1:00}:00",
                AvgOrdersPerHour = maxPeakAvg,
                SuggestedStaff = maxStaffReq,
                Title = $"🔥 Tăng cường nhân sự tại {branchName} khung {startPeak:00}:00 – {endPeak + 1:00}:00",
                ActionDescription = $"Lưu lượng đạt trung bình {maxPeakAvg:0.0} đơn/giờ (Cao điểm). Khuyến nghị: Bố trí tối thiểu {maxStaffReq} nhân sự ({bestBreakdown}) để phục vụ kịp thời, hạn chế khách đợi lâu.",
                Severity = "CRITICAL",
                BadgeColor = "#E53E3E"
            });
        }
        else
        {
            recommendations.Add(new StaffingRecommendationItemDto
            {
                RecommendationId = recId++,
                BranchId = branchId ?? 0,
                BranchName = branchName,
                RecommendationType = "OPTIMAL",
                TimeRange = "Cả ngày",
                AvgOrdersPerHour = overallAvg,
                SuggestedStaff = 1,
                Title = $"✅ Lưu lượng ổn định tại {branchName}",
                ActionDescription = "Chưa ghi nhận thời điểm quá tải đột biến. Lực lượng 1 - 2 nhân viên/ca hiện tại đang đáp ứng tốt nhu cầu phục vụ.",
                Severity = "SUCCESS",
                BadgeColor = "#38A169"
            });
        }

        // 2. Khuyến nghị TỐI ƯU CHI PHÍ vào khung giờ thấp điểm
        var lowHours = hourlyList.Where(x => x.TrafficLevel == "LOW" && x.Hour >= 9 && x.Hour <= 21).ToList();
        if (lowHours.Any())
        {
            var firstLow = lowHours.First();
            recommendations.Add(new StaffingRecommendationItemDto
            {
                RecommendationId = recId++,
                BranchId = branchId ?? 0,
                BranchName = branchName,
                RecommendationType = "DECREASE_STAFF",
                TimeRange = $"{firstLow.Hour:00}:00 – {firstLow.Hour + 2:00}:00",
                AvgOrdersPerHour = firstLow.AvgOrdersPerHour,
                SuggestedStaff = 1,
                Title = $"💡 Tối ưu nhân sự khung giờ thấp điểm ({firstLow.Hour:00}:00 – {firstLow.Hour + 2:00}:00)",
                ActionDescription = $"Lưu lượng khung giờ này chỉ đạt ~{firstLow.AvgOrdersPerHour:0.0} đơn/giờ. Đề xuất: Duy trì 1 nhân sự trực ca, có thể sắp xếp cho nhân viên nghỉ ca gãy hoặc vệ sinh/chuẩn bị nguyên liệu.",
                Severity = "INFO",
                BadgeColor = "#718096"
            });
        }

        // 3. Khuyến nghị CẢNH BÁO DỊP LỄ (Đổi màu đậm trên lịch)
        foreach (var hol in upcomingHolidays.Take(2))
        {
            recommendations.Add(new StaffingRecommendationItemDto
            {
                RecommendationId = recId++,
                BranchId = branchId ?? 0,
                BranchName = branchName,
                RecommendationType = "HOLIDAY_SURGE_PREP",
                TimeRange = hol.DateFormatted,
                AvgOrdersPerHour = overallAvg * hol.ExpectedSurgeMultiplier,
                SuggestedStaff = 3,
                Title = $"🎉 Cảnh báo Dịp Lễ: {hol.HolidayName} ({hol.DateFormatted})",
                ActionDescription = $"{hol.Description}. Dự kiến lượng đơn tăng thêm +{Math.Round((hol.ExpectedSurgeMultiplier - 1) * 100)}%. Đề xuất quản lý: Chủ động huy động nhân sự dự phòng Part-time hoặc tăng ca thưởng lễ trước 3 - 5 ngày.",
                Severity = "WARNING",
                BadgeColor = hol.HighlightColor,
                HolidayNote = "Hệ thống đã tô màu đậm trên lịch để bạn dễ nhận biết khi xếp ca."
            });
        }

        return new BranchStaffingAnalyticsResponse
        {
            BranchId = branchId ?? 0,
            BranchName = branchName,
            DaysAnalyzed = daysAnalyzed,
            FromDate = start,
            ToDate = end,
            TotalOrdersAnalyzed = totalOrders,
            TotalRevenueAnalyzed = totalRevenue,
            OverallAvgOrdersPerHour = Math.Round(overallAvg, 1),
            PeakTimeRange = peakItem != null ? peakItem.HourRangeLabel : "Chưa có",
            PeakAvgOrders = peakItem?.AvgOrdersPerHour ?? 0,
            OffPeakTimeRange = offPeakItem != null ? offPeakItem.HourRangeLabel : "Chưa có",
            OffPeakAvgOrders = offPeakItem?.AvgOrdersPerHour ?? 0,
            UpcomingHolidays = upcomingHolidays,
            HourlyTraffics = hourlyList,
            Recommendations = recommendations
        };
    }

    private static string FormatMoneyShort(decimal m)
    {
        if (m >= 1_000_000)
            return (m / 1_000_000m).ToString("0.#") + " Tr";
        if (m >= 1_000)
            return (m / 1_000m).ToString("0") + "k";
        return m.ToString("N0") + " đ";
    }
}
