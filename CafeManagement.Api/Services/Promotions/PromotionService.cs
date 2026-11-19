using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Promotions;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Promotions;

public class PromotionService : IPromotionService
{
    private readonly CafeDbContext _db;

    public PromotionService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<List<PromotionDto>> GetPromotionsAsync(bool activeOnly = false)
    {
        var query = _db.Promotions.AsNoTracking();
        if (activeOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now && (!p.UsageLimit.HasValue || p.UsedCount < p.UsageLimit.Value));
        }

        var list = await query.OrderByDescending(p => p.PromotionId).ToListAsync();
        return list.Select(MapToDto).ToList();
    }

    public async Task<PromotionDto?> GetPromotionByCodeAsync(string code)
    {
        string c = code.Trim().ToUpperInvariant();
        var promo = await _db.Promotions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PromotionCode.ToUpper() == c && p.IsActive);

        return promo == null ? null : MapToDto(promo);
    }

    public async Task<PromotionDto> CreatePromotionAsync(PromotionCreateUpdateRequest req)
    {
        string code = req.PromotionCode.Trim().ToUpperInvariant();
        var existing = await _db.Promotions.FirstOrDefaultAsync(p => p.PromotionCode.ToUpper() == code);
        if (existing != null)
        {
            throw new InvalidOperationException($"Mã khuyến mãi '{code}' đã tồn tại!");
        }

        var promo = new Promotion
        {
            PromotionCode = code,
            PromotionName = req.PromotionName.Trim(),
            DiscountType = req.DiscountType,
            DiscountValue = req.DiscountValue,
            MaxDiscountAmount = req.MaxDiscountAmount,
            MinOrderAmount = req.MinOrderAmount,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            UsageLimit = req.UsageLimit,
            UsedCount = 0,
            IsActive = req.IsActive
        };

        _db.Promotions.Add(promo);
        await _db.SaveChangesAsync();

        return MapToDto(promo);
    }

    public async Task<PromotionDto?> UpdatePromotionAsync(int id, PromotionCreateUpdateRequest req)
    {
        var promo = await _db.Promotions.FindAsync(id);
        if (promo == null) return null;

        promo.PromotionName = req.PromotionName.Trim();
        promo.DiscountType = req.DiscountType;
        promo.DiscountValue = req.DiscountValue;
        promo.MaxDiscountAmount = req.MaxDiscountAmount;
        promo.MinOrderAmount = req.MinOrderAmount;
        promo.StartDate = req.StartDate;
        promo.EndDate = req.EndDate;
        promo.UsageLimit = req.UsageLimit;
        promo.IsActive = req.IsActive;

        await _db.SaveChangesAsync();
        return MapToDto(promo);
    }

    public async Task<bool> TogglePromotionActiveAsync(int id)
    {
        var promo = await _db.Promotions.FindAsync(id);
        if (promo == null) return false;

        promo.IsActive = !promo.IsActive;
        await _db.SaveChangesAsync();
        return true;
    }

    private static PromotionDto MapToDto(Promotion p)
    {
        return new PromotionDto
        {
            PromotionId = p.PromotionId,
            PromotionCode = p.PromotionCode,
            PromotionName = p.PromotionName,
            DiscountType = p.DiscountType,
            DiscountValue = p.DiscountValue,
            MaxDiscountAmount = p.MaxDiscountAmount,
            MinOrderAmount = p.MinOrderAmount,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            UsageLimit = p.UsageLimit,
            UsedCount = p.UsedCount,
            IsActive = p.IsActive
        };
    }
}
