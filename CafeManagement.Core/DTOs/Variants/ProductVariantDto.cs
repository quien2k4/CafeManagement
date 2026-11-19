namespace CafeManagement.Core.DTOs.Variants;

public class ProductVariantDto
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string SkuCode { get; set; } = string.Empty;
    public string Size { get; set; } = "M";
    public string Temperature { get; set; } = "ICED";
    public decimal ExtraPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
