namespace CafeManagement.Core.DTOs.Customers;

public class CustomerDto
{
    public int CustomerId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MembershipTier { get; set; } = "BRONZE"; // BRONZE, SILVER, GOLD, DIAMOND
    public int TotalPoints { get; set; }
    public int LifetimePoints { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public string TierBadge => MembershipTier switch
    {
        "DIAMOND" => "💎 Kim Cương",
        "GOLD" => "🥇 Vàng",
        "SILVER" => "🥈 Bạc",
        _ => "🥉 Đồng"
    };

    public string TierColor => MembershipTier switch
    {
        "DIAMOND" => "#8E44AD",
        "GOLD" => "#D35400",
        "SILVER" => "#7F8C8D",
        _ => "#A0522D"
    };
}

public class CustomerCreateRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class AdjustCustomerPointsRequest
{
    public int CustomerId { get; set; }
    public int PointAdjustment { get; set; }
    public string Reason { get; set; } = string.Empty;
}
