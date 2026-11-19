using CafeManagement.Core.DTOs.WorkShifts;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Services.Http;

public class ShiftApiClient
{
    private static readonly Lazy<ShiftApiClient> _instance = new(() => new ShiftApiClient());
    public static ShiftApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public ShiftApiClient() : this(ApiClient.Instance)
    {
    }

    public ShiftApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<WorkShift?> GetCurrentOpenShiftAsync(int branchId)
    {
        try
        {
            var dto = await _api.GetAsync<WorkShiftDto>($"/api/workshifts/current?branchId={branchId}");
            return dto == null ? null : MapToWorkShift(dto);
        }
        catch
        {
            return null;
        }
    }

    public async Task<WorkShift?> OpenShiftAsync(OpenShiftRequest req)
    {
        var dto = await _api.PostAsync<OpenShiftRequest, WorkShiftDto>("/api/workshifts/open", req);
        return dto == null ? null : MapToWorkShift(dto);
    }

    public async Task<WorkShift?> CloseShiftAsync(CloseShiftRequest req)
    {
        var dto = await _api.PostAsync<CloseShiftRequest, WorkShiftDto>("/api/workshifts/close", req);
        return dto == null ? null : MapToWorkShift(dto);
    }

    public async Task<ZReportDto?> GetZReportAsync(long shiftId)
    {
        return await _api.GetAsync<ZReportDto>($"/api/workshifts/{shiftId}/z-report");
    }

    public async Task<List<WorkShift>> GetShiftsAsync(int? branchId = null, DateTime? from = null, DateTime? to = null)
    {
        string url = "/api/workshifts";
        var queryParams = new List<string>();
        if (branchId.HasValue && branchId.Value > 0) queryParams.Add($"branchId={branchId.Value}");
        if (from.HasValue) queryParams.Add($"from={from.Value:O}");
        if (to.HasValue) queryParams.Add($"to={to.Value:O}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        var list = await _api.GetAsync<List<WorkShiftDto>>(url);
        return list?.Select(MapToWorkShift).ToList() ?? [];
    }

    public static WorkShift MapToWorkShift(WorkShiftDto d)
    {
        return new WorkShift
        {
            ShiftId = d.ShiftId,
            ShiftCode = d.ShiftCode,
            BranchId = d.BranchId,
            CashierUserId = d.CashierUserId,
            CashierName = d.CashierName,
            ShiftName = d.ShiftName,
            StartTime = d.StartTime,
            EndTime = d.EndTime,
            OpeningCash = d.OpeningCash,
            CashSales = d.CashSales,
            VietQrSales = d.VietQrSales,
            TotalSales = d.TotalSales,
            TotalOrders = d.TotalOrders,
            ExpectedClosingCash = d.ExpectedClosingCash,
            ActualClosingCash = d.ActualClosingCash,
            DifferenceAmount = d.DifferenceAmount,
            Status = d.Status,
            Notes = d.Notes
        };
    }
}
