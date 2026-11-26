using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Branches;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Branches;

public class BranchService : IBranchesService
{
    private readonly CafeDbContext _db;

    public BranchService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<List<BranchDto>> GetAllBranchesAsync(bool activeOnly = false)
    {
        var query = _db.Branches.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(b => b.IsActive);
        }
        return await query
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

    public async Task<List<BranchDto>> GetActiveBranchesAsync()
    {
        return await GetAllBranchesAsync(activeOnly: true);
    }

    public async Task<BranchDto?> GetBranchByIdAsync(int id)
    {
        var b = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == id);
        if (b == null) return null;
        return new BranchDto
        {
            BranchId = b.BranchId,
            BranchCode = b.BranchCode,
            BranchName = b.BranchName,
            Address = b.Address,
            Phone = b.Phone,
            IsActive = b.IsActive
        };
    }

    public async Task<BranchDto> CreateBranchAsync(BranchCreateRequest req)
    {
        string code = req.BranchCode.Trim().ToUpper();
        bool exists = await _db.Branches.AnyAsync(b => b.BranchCode.ToUpper() == code);
        if (exists)
        {
            throw new InvalidOperationException($"Mã chi nhánh '{code}' đã tồn tại trong hệ thống!");
        }

        var branch = new Branch
        {
            BranchCode = code,
            BranchName = req.BranchName.Trim(),
            Address = req.Address.Trim(),
            Phone = req.Phone.Trim(),
            IsActive = true
        };

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        // Tự động khởi tạo tồn kho ban đầu (BranchInventory) cho chi nhánh mới đối với tất cả nguyên vật liệu
        try
        {
            var ingredients = await _db.Ingredients.AsNoTracking().ToListAsync();
            if (ingredients.Count > 0)
            {
                var inventories = ingredients.Select(i => new BranchInventory
                {
                    BranchId = branch.BranchId,
                    IngredientId = i.IngredientId,
                    QuantityOnHand = 0,
                    ReservedQuantity = 0,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();

                _db.BranchInventories.AddRange(inventories);
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi khởi tạo kho cho chi nhánh mới: {ex.Message}");
        }

        return new BranchDto
        {
            BranchId = branch.BranchId,
            BranchCode = branch.BranchCode,
            BranchName = branch.BranchName,
            Address = branch.Address,
            Phone = branch.Phone,
            IsActive = branch.IsActive
        };
    }

    public async Task<BranchDto?> UpdateBranchAsync(int id, BranchUpdateRequest req)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
        if (branch == null) return null;

        branch.BranchName = req.BranchName.Trim();
        branch.Address = req.Address.Trim();
        branch.Phone = req.Phone.Trim();

        await _db.SaveChangesAsync();

        return new BranchDto
        {
            BranchId = branch.BranchId,
            BranchCode = branch.BranchCode,
            BranchName = branch.BranchName,
            Address = branch.Address,
            Phone = branch.Phone,
            IsActive = branch.IsActive
        };
    }

    public async Task<bool> ToggleBranchActiveAsync(int id)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
        if (branch == null) return false;

        branch.IsActive = !branch.IsActive;
        await _db.SaveChangesAsync();
        return true;
    }
}
