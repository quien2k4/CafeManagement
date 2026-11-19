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

    public async Task<DashboardReportDto> GetDashboardReportAsync(int? branchId = null, string dateFilter = "ALL")
    {
        await EnsureRefundTableExistsAsync();

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
            TopProducts = topList,
            Orders = orderReports
        };
    }
}
