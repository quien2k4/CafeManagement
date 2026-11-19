using CafeManagement.Core.DTOs.Reports;

namespace CafeManagement.Api.Services.Interfaces;

public interface IReportService
{
    Task<DashboardReportDto> GetDashboardReportAsync(int? branchId = null, string dateFilter = "ALL");
}
