namespace CafeManagement.Core.DTOs.KDS;

public class KdsItemDto
{
    public long OrderItemId { get; set; }
    public long OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Size { get; set; } = "M";
    public int Quantity { get; set; } = 1;
    public string SugarLevel { get; set; } = "100%";
    public string IceLevel { get; set; } = "100%";
    public string Note { get; set; } = string.Empty;
    public string OptionsSummary { get; set; } = string.Empty;
    public string Status { get; set; } = "QUEUED"; // QUEUED, PREPARING, READY, SERVED
    public List<string> Toppings { get; set; } = [];
}

public class KdsTicketDto
{
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string OrderType { get; set; } = "TAKE_AWAY";
    public string OrderStatus { get; set; } = "PAID"; // PAID, PREPARING, READY, COMPLETED
    public DateTime CreatedAt { get; set; }
    public List<KdsItemDto> Items { get; set; } = [];
}

public class UpdateKdsItemStatusRequest
{
    public string Status { get; set; } = "PREPARING"; // PREPARING, READY, SERVED
}
