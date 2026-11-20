using CafeManagement.Core.DTOs.Reports;

namespace CafeManagement.Api.Services.Interfaces;

public interface IReportService
{
    Task<DashboardReportDto> GetDashboardReportAsync(int? branchId = null, string dateFilter = "ALL", int days = 7, DateTime? fromDate = null, DateTime? toDate = null);
}
