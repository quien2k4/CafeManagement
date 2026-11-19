using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Products;
using CafeManagement.Core.DTOs.Toppings;
using CafeManagement.Core.DTOs.Variants;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Products;

public class ProductService : IProductService
{
    private readonly CafeDbContext _db;

    public ProductService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProductDto>> GetProductsAsync(int? categoryId = null, bool activeOnly = false)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var list = await query
            .OrderByDescending(p => p.ProductId)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        return product == null ? null : MapToDto(product);
    }

    public async Task<ProductDto> CreateProductAsync(ProductCreateRequest req)
    {
        var product = new Product
        {
            CategoryId = req.CategoryId,
            ProductCode = req.ProductCode,
            ProductName = req.ProductName,
            Description = req.Description,
            BasePrice = req.BasePrice,
            ImageUrl = req.ImageUrl,
            IsActive = req.IsActive,
            Variants =
            [
                new ProductVariant
                {
                    SkuCode = $"{req.ProductCode}-M",
                    Size = "M",
                    Temperature = "ICED",
                    ExtraPrice = 0,
                    IsActive = true
                },
                new ProductVariant
                {
                    SkuCode = $"{req.ProductCode}-L",
                    Size = "L",
                    Temperature = "ICED",
                    ExtraPrice = req.ExtraPriceSizeL,
                    IsActive = true
                }
            ]
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // Nạp Category name để trả về đầy đủ
        await _db.Entry(product).Reference(p => p.Category).LoadAsync();
        return MapToDto(product);
    }

    public async Task<ProductDto?> UpdateProductAsync(int id, ProductUpdateRequest req)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null) return null;

        product.CategoryId = req.CategoryId;
        product.ProductName = req.ProductName;
        product.Description = req.Description;
        product.BasePrice = req.BasePrice;
        product.ImageUrl = req.ImageUrl;
        product.IsActive = req.IsActive;

        // Cập nhật giá thêm của Size L
        var variantL = product.Variants.FirstOrDefault(v => v.Size == "L");
        if (variantL != null)
        {
            variantL.ExtraPrice = req.ExtraPriceSizeL;
        }
        else
        {
            product.Variants.Add(new ProductVariant
            {
                SkuCode = $"{product.ProductCode}-L",
                Size = "L",
                Temperature = "ICED",
                ExtraPrice = req.ExtraPriceSizeL,
                IsActive = true
            });
        }

        await _db.SaveChangesAsync();
        await _db.Entry(product).Reference(p => p.Category).LoadAsync();

        return MapToDto(product);
    }

    public async Task<bool> ToggleProductActiveAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return false;

        product.IsActive = !product.IsActive;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ToppingDto>> GetToppingsAsync(bool activeOnly = false)
    {
        var query = _db.Toppings.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query
            .OrderBy(t => t.ToppingId)
            .Select(t => new ToppingDto
            {
                ToppingId = t.ToppingId,
                ToppingCode = t.ToppingCode,
                ToppingName = t.ToppingName,
                Price = t.Price,
                IsActive = t.IsActive
            })
            .ToListAsync();
    }

    private static ProductDto MapToDto(Product p)
    {
        return new ProductDto
        {
            ProductId = p.ProductId,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.CategoryName ?? string.Empty,
            ProductCode = p.ProductCode,
            ProductName = p.ProductName,
            Description = p.Description,
            BasePrice = p.BasePrice,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive,
            Variants = p.Variants.Select(v => new ProductVariantDto
            {
                VariantId = v.VariantId,
                ProductId = v.ProductId,
                SkuCode = v.SkuCode,
                Size = v.Size,
                Temperature = v.Temperature,
                ExtraPrice = v.ExtraPrice,
                IsActive = v.IsActive
            }).ToList()
        };
    }
}
