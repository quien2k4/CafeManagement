namespace CafeManagement.Core.DTOs.Orders;

public class OrderItemToppingRequest
{
    public int ToppingId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string Size { get; set; } = "M";
    public string Sugar { get; set; } = "100%";
    public string Ice { get; set; } = "100%";
    public string Note { get; set; } = string.Empty;
    public string OptionsSummary { get; set; } = string.Empty;
    public List<OrderItemToppingRequest> Toppings { get; set; } = [];
}

public class CreateOrderRequest
{
    public int BranchId { get; set; }
    public int CashierUserId { get; set; }
    public long? ShiftId { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerPhone { get; set; }
    public int CustomerPointsUsed { get; set; }
    public string? PromotionCode { get; set; }
    public string OrderType { get; set; } = "TAKE_AWAY"; // TAKE_AWAY, DINE_IN
    public string PaymentMethod { get; set; } = "CASH"; // CASH, VIETQR
    public decimal CustomerGivenAmount { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public List<OrderItemRequest> Items { get; set; } = [];
}

public class OrderDto
{
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int CashierUserId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PromotionCode { get; set; }
    public string OrderType { get; set; } = "TAKE_AWAY";
    public string Status { get; set; } = "PAID";
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public DateTime CreatedAt { get; set; }
    public int PointsEarned { get; set; }
    public int PointsUsed { get; set; }
}
