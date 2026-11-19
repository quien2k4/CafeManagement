using CafeManagement.Core.DTOs.Promotions;

namespace CafeManagement.Api.Services.Interfaces;

public interface IPromotionService
{
    Task<List<PromotionDto>> GetPromotionsAsync(bool activeOnly = false);
    Task<PromotionDto?> GetPromotionByCodeAsync(string code);
    Task<PromotionDto> CreatePromotionAsync(PromotionCreateUpdateRequest req);
    Task<PromotionDto?> UpdatePromotionAsync(int id, PromotionCreateUpdateRequest req);
    Task<bool> TogglePromotionActiveAsync(int id);
}
