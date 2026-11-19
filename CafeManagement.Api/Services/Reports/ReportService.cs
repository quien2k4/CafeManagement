using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Reports;

namespace CafeManagement.Api.Services.Reports;

public class ReportService : IReportService
{
    private readonly CafeDbContext _db;

    public ReportService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardReportDto> GetDashboardReportAsync(int? branchId = null, string dateFilter = "ALL")
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Variant)
                    .ThenInclude(v => v!.Product)
            .Include(o => o.Payments)
            .Include(o => o.Branch)
            .Where(o => o.OrderStatus == "PAID" || o.OrderStatus == "PREPARING" || o.OrderStatus == "READY" || o.OrderStatus == "SERVED");

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(o => o.BranchId == branchId.Value);
        }

        var now = DateTime.UtcNow;
        if (dateFilter == "TODAY")
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
        decimal averageOrderValue = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 0) : 0;

        var allPayments = orders.SelectMany(o => o.Payments).ToList();
        var cashPayments = allPayments.Where(p => p.PaymentMethod == "CASH").ToList();
        var qrPayments = allPayments.Where(p => p.PaymentMethod == "VIETQR").ToList();

        decimal cashRevenue = cashPayments.Sum(p => p.Amount);
        int cashCount = cashPayments.Count;

        decimal qrRevenue = qrPayments.Sum(p => p.Amount);
        int qrCount = qrPayments.Count;

        decimal totalPaymentAmount = cashRevenue + qrRevenue;
        double cashPercent = totalPaymentAmount > 0 ? Math.Round((double)(cashRevenue / totalPaymentAmount) * 100.0, 1) : 50;
        double qrPercent = totalPaymentAmount > 0 ? Math.Round((double)(qrRevenue / totalPaymentAmount) * 100.0, 1) : 50;

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

            return new OrderReportDto
            {
                OrderNumber = o.OrderNumber,
                CreatedAt = o.CreatedAt,
                CashierName = cName,
                BranchName = o.Branch?.BranchName ?? "Chi nhánh",
                PaymentMethod = pMethod,
                ItemsSummary = summary,
                FinalAmount = o.FinalAmount
            };
        }).ToList();

        return new DashboardReportDto
        {
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            TotalCups = totalCups,
            AverageOrderValue = averageOrderValue,
            CashRevenue = cashRevenue,
            CashCount = cashCount,
            CashPercent = cashPercent,
            QrRevenue = qrRevenue,
            QrCount = qrCount,
            QrPercent = qrPercent,
            TopProducts = topList,
            Orders = orderReports
        };
    }
}
