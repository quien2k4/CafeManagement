using CafeManagement.Core.DTOs.Variants;

namespace CafeManagement.Core.DTOs.Products;

public class ProductDto
{
    public int ProductId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ProductVariantDto> Variants { get; set; } = [];
}

public class ProductCreateRequest
{
    public int CategoryId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string? ImageUrl { get; set; }
    public decimal ExtraPriceSizeL { get; set; } = 6000;
    public bool IsActive { get; set; } = true;
}

public class ProductUpdateRequest
{
    public int CategoryId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string? ImageUrl { get; set; }
    public decimal ExtraPriceSizeL { get; set; } = 6000;
    public bool IsActive { get; set; } = true;
}
