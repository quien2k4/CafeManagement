namespace CafeManagement.Core.DTOs.Payments;

public class PaymentDto
{
    public long PaymentId { get; set; }
    public long OrderId { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "SUCCESS";
    public DateTime PaidAt { get; set; }
}
