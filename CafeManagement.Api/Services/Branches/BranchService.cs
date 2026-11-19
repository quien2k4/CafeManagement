using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Branches;

namespace CafeManagement.Api.Services.Branches;

public class BranchService : IBranchesService
{
    private readonly CafeDbContext _db;

    public BranchService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<List<BranchDto>> GetActiveBranchesAsync()
    {
        return await _db.Branches
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.BranchId)
            .Select(b => new BranchDto
            {
                BranchId = b.BranchId,
                BranchCode = b.BranchCode,
                BranchName = b.BranchName,
                Address = b.Address,
                Phone = b.Phone,
                IsActive = b.IsActive
            })
            .ToListAsync();
    }
}
