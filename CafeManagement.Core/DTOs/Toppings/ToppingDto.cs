namespace CafeManagement.Core.DTOs.Toppings;

public class ToppingDto
{
    public int ToppingId { get; set; }
    public string ToppingCode { get; set; } = string.Empty;
    public string ToppingName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
