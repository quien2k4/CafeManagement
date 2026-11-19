using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.WorkShifts;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.WorkShifts;

public class ShiftService : IShiftService
{
    private readonly CafeDbContext _db;

    public ShiftService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<WorkShiftDto?> GetCurrentOpenShiftAsync(int branchId)
    {
        var shift = await _db.WorkShifts
            .Include(s => s.Branch)
            .Where(s => s.BranchId == branchId && s.Status == "OPEN")
            .OrderByDescending(s => s.ShiftId)
            .FirstOrDefaultAsync();

        return shift == null ? null : MapToDto(shift);
    }

    public async Task<WorkShiftDto> OpenShiftAsync(OpenShiftRequest req)
    {
        var existing = await _db.WorkShifts
            .Where(s => s.BranchId == req.BranchId && s.Status == "OPEN")
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            throw new InvalidOperationException($"Chi nhánh đang có ca làm việc '{existing.ShiftName}' (#{existing.ShiftCode}) chưa kết thúc!");
        }

        var shift = new WorkShift
        {
            ShiftCode = $"CA{DateTime.UtcNow:yyMMddHHmm}-{Random.Shared.Next(100, 999)}",
            BranchId = req.BranchId,
            CashierUserId = req.CashierUserId,
            CashierName = req.CashierName,
            ShiftName = string.IsNullOrWhiteSpace(req.ShiftName) ? "Ca Sáng" : req.ShiftName,
            StartTime = DateTime.UtcNow,
            OpeningCash = req.OpeningCash,
            CashSales = 0,
            VietQrSales = 0,
            TotalSales = 0,
            TotalOrders = 0,
            ExpectedClosingCash = req.OpeningCash,
            Status = "OPEN",
            Notes = req.Notes
        };

        _db.WorkShifts.Add(shift);
        await _db.SaveChangesAsync();

        return MapToDto(shift);
    }

    public async Task<WorkShiftDto> CloseShiftAsync(CloseShiftRequest req)
    {
        var shift = await _db.WorkShifts
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.ShiftId == req.ShiftId);

        if (shift == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy ca làm việc có ID {req.ShiftId}");
        }

        if (shift.Status != "OPEN")
        {
            throw new InvalidOperationException("Ca làm việc này đã được kết ca trước đó!");
        }

        shift.EndTime = DateTime.UtcNow;
        shift.ActualClosingCash = req.ActualClosingCash;
        decimal expected = shift.OpeningCash + shift.CashSales;
        shift.ExpectedClosingCash = expected;
        shift.DifferenceAmount = req.ActualClosingCash - expected;

        if (!string.IsNullOrWhiteSpace(req.Notes))
        {
            shift.Notes = string.IsNullOrWhiteSpace(shift.Notes)
                ? req.Notes
                : $"{shift.Notes}\n[Đóng ca]: {req.Notes}";
        }

        shift.Status = "CLOSED";
        await _db.SaveChangesAsync();

        return MapToDto(shift);
    }

    public async Task<ZReportDto> GetZReportAsync(long shiftId)
    {
        var shift = await _db.WorkShifts
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.ShiftId == shiftId);

        if (shift == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy ca làm việc ID {shiftId}");
        }

        return new ZReportDto
        {
            ShiftId = shift.ShiftId,
            ShiftCode = shift.ShiftCode,
            BranchName = shift.Branch?.BranchName ?? "Chi nhánh",
            CashierName = shift.CashierName,
            ShiftName = shift.ShiftName,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime ?? DateTime.UtcNow,
            TotalOrders = shift.TotalOrders,
            CashSales = shift.CashSales,
            VietQrSales = shift.VietQrSales,
            TotalSales = shift.TotalSales,
            OpeningCash = shift.OpeningCash,
            CashAdded = shift.CashSales,
            ExpectedClosingCash = shift.ExpectedClosingCash,
            ActualClosingCash = shift.ActualClosingCash ?? shift.ExpectedClosingCash,
            DifferenceAmount = shift.DifferenceAmount ?? 0,
            Notes = shift.Notes
        };
    }

    public async Task<List<WorkShiftDto>> GetShiftsAsync(int? branchId = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _db.WorkShifts
            .Include(s => s.Branch)
            .AsNoTracking()
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(s => s.StartTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(s => s.StartTime <= to.Value);
        }

        var list = await query.OrderByDescending(s => s.ShiftId).ToListAsync();
        return list.Select(MapToDto).ToList();
    }

    private static WorkShiftDto MapToDto(WorkShift s)
    {
        return new WorkShiftDto
        {
            ShiftId = s.ShiftId,
            ShiftCode = s.ShiftCode,
            BranchId = s.BranchId,
            BranchName = s.Branch?.BranchName ?? string.Empty,
            CashierUserId = s.CashierUserId,
            CashierName = s.CashierName,
            ShiftName = s.ShiftName,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            OpeningCash = s.OpeningCash,
            CashSales = s.CashSales,
            VietQrSales = s.VietQrSales,
            TotalSales = s.TotalSales,
            TotalOrders = s.TotalOrders,
            ExpectedClosingCash = s.ExpectedClosingCash,
            ActualClosingCash = s.ActualClosingCash,
            DifferenceAmount = s.DifferenceAmount,
            Status = s.Status,
            Notes = s.Notes
        };
    }
}
