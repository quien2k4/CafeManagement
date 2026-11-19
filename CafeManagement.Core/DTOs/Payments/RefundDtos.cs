namespace CafeManagement.Core.DTOs.Payments;

public class RefundItemRequest
{
    public long OrderItemId { get; set; }
    public int VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Size { get; set; } = "M";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }
}

public class RefundCreateRequest
{
    public long OrderId { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundMethod { get; set; } = "CASH"; // CASH, VIETQR, ORIGINAL
    public string RefundReason { get; set; } = string.Empty;
    public int ProcessedByUserId { get; set; }
    public string ProcessedByName { get; set; } = string.Empty;
    public bool ReturnIngredientsToInventory { get; set; } = false;
    public List<RefundItemRequest> Items { get; set; } = [];
}

public class RefundDto
{
    public long RefundId { get; set; }
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public long? PaymentId { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundMethod { get; set; } = "CASH";
    public string RefundReason { get; set; } = string.Empty;
    public string Status { get; set; } = "COMPLETED";
    public int ProcessedByUserId { get; set; }
    public string ProcessedByName { get; set; } = string.Empty;
    public DateTime RefundedAt { get; set; }
    public List<RefundItemRequest> Items { get; set; } = [];
}
