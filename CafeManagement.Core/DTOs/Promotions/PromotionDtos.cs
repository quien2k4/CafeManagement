namespace CafeManagement.Core.DTOs.Promotions;

public class PromotionDto
{
    public int PromotionId { get; set; }
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "PERCENT"; // PERCENT, FIXED_AMOUNT, BUY1_GET1
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal MinOrderAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;

    public string StatusBadge => !IsActive ? "⛔ Tạm ngưng" :
        DateTime.UtcNow < StartDate ? "⏳ Chưa bắt đầu" :
        DateTime.UtcNow > EndDate ? "⌛ Hết hạn" :
        (UsageLimit.HasValue && UsedCount >= UsageLimit.Value) ? "🔒 Hết lượt" :
        "🟢 Đang hoạt động";

    public string DiscountDisplay => DiscountType switch
    {
        "PERCENT" => $"Giảm {DiscountValue:N0}%" + (MaxDiscountAmount.HasValue ? $" (Tối đa {MaxDiscountAmount.Value:N0}đ)" : ""),
        "FIXED_AMOUNT" => $"Giảm {DiscountValue:N0} đ",
        "BUY1_GET1" => "Mua 1 Tặng 1",
        _ => $"{DiscountValue:N0}"
    };
}

public class PromotionCreateUpdateRequest
{
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "PERCENT";
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal MinOrderAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? UsageLimit { get; set; }
    public bool IsActive { get; set; } = true;
}
