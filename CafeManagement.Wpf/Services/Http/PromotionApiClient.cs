using CafeManagement.Core.DTOs.Promotions;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Services.Http;

public class PromotionApiClient
{
    private static readonly Lazy<PromotionApiClient> _instance = new(() => new PromotionApiClient());
    public static PromotionApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public PromotionApiClient() : this(ApiClient.Instance)
    {
    }

    public PromotionApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<List<Promotion>> GetPromotionsAsync(bool activeOnly = false)
    {
        var dtos = await _api.GetAsync<List<PromotionDto>>($"/api/promotions?activeOnly={activeOnly}");
        return dtos?.Select(MapToPromotion).ToList() ?? [];
    }

    public async Task<Promotion?> GetPromotionByCodeAsync(string code)
    {
        try
        {
            var dto = await _api.GetAsync<PromotionDto>($"/api/promotions/by-code/{Uri.EscapeDataString(code.Trim())}");
            return dto == null ? null : MapToPromotion(dto);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Promotion?> CreatePromotionAsync(PromotionCreateUpdateRequest req)
    {
        var dto = await _api.PostAsync<PromotionCreateUpdateRequest, PromotionDto>("/api/promotions", req);
        return dto == null ? null : MapToPromotion(dto);
    }

    public async Task<Promotion?> UpdatePromotionAsync(int id, PromotionCreateUpdateRequest req)
    {
        var dto = await _api.PostAsync<PromotionCreateUpdateRequest, PromotionDto>($"/api/promotions/{id}", req);
        return dto == null ? null : MapToPromotion(dto);
    }

    public async Task<bool> TogglePromotionActiveAsync(int id)
    {
        try
        {
            return await _api.PostAsync($"/api/promotions/{id}/toggle-active", new { });
        }
        catch
        {
            return false;
        }
    }

    private static Promotion MapToPromotion(PromotionDto d)
    {
        return new Promotion
        {
            PromotionId = d.PromotionId,
            PromotionCode = d.PromotionCode,
            PromotionName = d.PromotionName,
            DiscountType = d.DiscountType,
            DiscountValue = d.DiscountValue,
            MaxDiscountAmount = d.MaxDiscountAmount,
            MinOrderAmount = d.MinOrderAmount,
            StartDate = d.StartDate,
            EndDate = d.EndDate,
            UsageLimit = d.UsageLimit,
            UsedCount = d.UsedCount,
            IsActive = d.IsActive
        };
    }
}
