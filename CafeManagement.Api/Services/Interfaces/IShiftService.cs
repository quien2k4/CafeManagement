using CafeManagement.Core.DTOs.WorkShifts;

namespace CafeManagement.Api.Services.Interfaces;

public interface IShiftService
{
    Task<WorkShiftDto?> GetCurrentOpenShiftAsync(int branchId);
    Task<WorkShiftDto> OpenShiftAsync(OpenShiftRequest req);
    Task<WorkShiftDto> CloseShiftAsync(CloseShiftRequest req);
    Task<ZReportDto> GetZReportAsync(long shiftId);
    Task<List<WorkShiftDto>> GetShiftsAsync(int? branchId = null, DateTime? from = null, DateTime? to = null);
}
