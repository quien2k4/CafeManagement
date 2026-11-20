using CafeManagement.Core.DTOs.Reports;

namespace CafeManagement.Wpf.Services.Http;

public class ReportApiClient
{
    private static readonly Lazy<ReportApiClient> _instance = new(() => new ReportApiClient());
    public static ReportApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public ReportApiClient() : this(ApiClient.Instance)
    {
    }

    public ReportApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<DashboardReportDto?> GetDashboardReportAsync(
        int? branchId = null, 
        string dateFilter = "ALL", 
        int days = 7,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        try
        {
            string url = $"/api/reports/dashboard?dateFilter={Uri.EscapeDataString(dateFilter)}&days={days}";
            if (branchId.HasValue && branchId.Value > 0)
            {
                url += $"&branchId={branchId.Value}";
            }
            if (fromDate.HasValue)
            {
                url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
            }
            if (toDate.HasValue)
            {
                url += $"&toDate={toDate.Value:yyyy-MM-dd}";
            }
            return await _api.GetAsync<DashboardReportDto>(url);
        }
        catch
        {
            return null;
        }
    }
}
