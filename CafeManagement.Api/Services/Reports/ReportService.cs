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

    private static string FormatMoneyShort(decimal m)
    {
        if (m >= 1_000_000)
            return (m / 1_000_000m).ToString("0.#") + " Tr";
        if (m >= 1_000)
            return (m / 1_000m).ToString("0") + "k";
        return m.ToString("N0") + " đ";
    }
}
