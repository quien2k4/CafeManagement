using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache? _cache;

    public ProductService(CafeDbContext db, IMemoryCache? cache = null)
    {
        _db = db;
        _cache = cache;
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
        var query = _db.Toppings
            .Include(t => t.RecipeDetails)
                .ThenInclude(rd => rd.Ingredient)
            .AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(t => t.IsActive);
        }

        var list = await query.OrderBy(t => t.ToppingId).ToListAsync();
        return list.Select(MapToppingToDto).ToList();
    }

    public async Task<ToppingDto?> GetToppingByIdAsync(int id)
    {
        var t = await _db.Toppings
            .Include(t => t.RecipeDetails)
                .ThenInclude(rd => rd.Ingredient)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ToppingId == id);

        return t == null ? null : MapToppingToDto(t);
    }

    public async Task<ToppingDto> CreateToppingAsync(ToppingCreateRequest req)
    {
        var code = req.ToppingCode.Trim().ToUpper();
        var exists = await _db.Toppings.AnyAsync(t => t.ToppingCode == code);
        if (exists)
        {
            throw new InvalidOperationException($"Mã Topping '{code}' đã tồn tại!");
        }

        var topping = new Topping
        {
            ToppingCode = code,
            ToppingName = req.ToppingName.Trim(),
            Price = req.Price,
            IsActive = req.IsActive,
            RecipeDetails = req.RecipeDetails.Select(d => new ToppingRecipeDetail
            {
                IngredientId = d.IngredientId,
                Quantity = d.Quantity,
                Uom = string.IsNullOrWhiteSpace(d.Uom) ? "g" : d.Uom.Trim()
            }).ToList()
        };

        _db.Toppings.Add(topping);
        await _db.SaveChangesAsync();

        return await GetToppingByIdAsync(topping.ToppingId) ?? MapToppingToDto(topping);
    }

    public async Task<ToppingDto?> UpdateToppingAsync(int id, ToppingUpdateRequest req)
    {
        var topping = await _db.Toppings
            .Include(t => t.RecipeDetails)
            .FirstOrDefaultAsync(t => t.ToppingId == id);

        if (topping == null) return null;

        topping.ToppingName = req.ToppingName.Trim();
        topping.Price = req.Price;
        topping.IsActive = req.IsActive;

        _db.ToppingRecipeDetails.RemoveRange(topping.RecipeDetails);
        topping.RecipeDetails = req.RecipeDetails.Select(d => new ToppingRecipeDetail
        {
            ToppingId = id,
            IngredientId = d.IngredientId,
            Quantity = d.Quantity,
            Uom = string.IsNullOrWhiteSpace(d.Uom) ? "g" : d.Uom.Trim()
        }).ToList();

        await _db.SaveChangesAsync();
        return await GetToppingByIdAsync(id);
    }

    private static ToppingDto MapToppingToDto(Topping t)
    {
        return new ToppingDto
        {
            ToppingId = t.ToppingId,
            ToppingCode = t.ToppingCode,
            ToppingName = t.ToppingName,
            Price = t.Price,
            IsActive = t.IsActive,
            RecipeDetails = t.RecipeDetails.Select(rd => new ToppingRecipeDetailDto
            {
                ToppingRecipeDetailId = rd.ToppingRecipeDetailId,
                ToppingId = rd.ToppingId,
                IngredientId = rd.IngredientId,
                IngredientCode = rd.Ingredient?.IngredientCode ?? string.Empty,
                IngredientName = rd.Ingredient?.IngredientName ?? string.Empty,
                Quantity = rd.Quantity,
                Uom = rd.Uom
            }).ToList()
        };
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

    public async Task<List<ToppingRecommendationDto>> GetRecommendedToppingsAsync(int productId, int limit = 3)
    {
        string cacheKey = $"ai_rec_toppings_{productId}_{limit}";
        if (_cache != null && _cache.TryGetValue(cacheKey, out List<ToppingRecommendationDto>? cached) && cached != null)
        {
            return cached;
        }

        var activeToppings = await _db.Toppings
            .AsNoTracking()
            .Where(t => t.IsActive)
            .ToDictionaryAsync(t => t.ToppingId);

        if (activeToppings.Count == 0) return [];

        var variantIds = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == productId)
            .Select(v => v.VariantId)
            .ToListAsync();

        var result = new List<ToppingRecommendationDto>();

        if (variantIds.Count > 0)
        {
            var productOrderItems = await _db.OrderItems
                .AsNoTracking()
                .Where(oi => variantIds.Contains(oi.VariantId) && oi.Order != null && oi.Order.OrderStatus != "CANCELLED")
                .Select(oi => oi.OrderItemId)
                .ToListAsync();

            int totalOrders = productOrderItems.Count;

            if (totalOrders > 0)
            {
                var toppingStats = await _db.OrderItemToppings
                    .AsNoTracking()
                    .Where(oit => productOrderItems.Contains(oit.OrderItemId))
                    .GroupBy(oit => oit.ToppingId)
                    .Select(g => new
                    {
                        ToppingId = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                foreach (var ts in toppingStats)
                {
                    if (activeToppings.TryGetValue(ts.ToppingId, out var topping))
                    {
                        decimal percentage = Math.Round(((decimal)ts.Count / totalOrders) * 100m, 1);
                        result.Add(new ToppingRecommendationDto
                        {
                            ToppingId = topping.ToppingId,
                            ToppingCode = topping.ToppingCode,
                            ToppingName = topping.ToppingName,
                            Price = topping.Price,
                            Percentage = percentage,
                            OrderCount = ts.Count
                        });
                    }
                    if (result.Count >= limit) break;
                }
            }
        }

        // Cold-start fallback: nếu chưa đủ kết quả hoặc món mới chưa có dữ liệu
        if (result.Count < limit)
        {
            var fallbackToppings = await _db.OrderItemToppings
                .AsNoTracking()
                .Where(oit => oit.OrderItem != null && oit.OrderItem.Order != null && oit.OrderItem.Order.OrderStatus != "CANCELLED")
                .GroupBy(oit => oit.ToppingId)
                .Select(g => new { ToppingId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(limit * 2)
                .ToListAsync();

            int totalGlobalOrders = Math.Max(1, await _db.OrderItems.CountAsync());

            foreach (var ft in fallbackToppings)
            {
                if (activeToppings.TryGetValue(ft.ToppingId, out var topping) && !result.Any(r => r.ToppingId == topping.ToppingId))
                {
                    decimal percentage = Math.Round(((decimal)ft.Count / totalGlobalOrders) * 100m, 1);
                    if (percentage < 15) percentage = 25.0m;
                    result.Add(new ToppingRecommendationDto
                    {
                        ToppingId = topping.ToppingId,
                        ToppingCode = topping.ToppingCode,
                        ToppingName = topping.ToppingName,
                        Price = topping.Price,
                        Percentage = percentage,
                        OrderCount = ft.Count
                    });
                }
                if (result.Count >= limit) break;
            }
        }

        // Demo fallback nếu toàn hệ thống chưa có dữ liệu đơn hàng
        if (result.Count == 0)
        {
            var samplePercentages = new[] { 42.0m, 31.0m, 18.0m };
            int idx = 0;
            foreach (var t in activeToppings.Values.Take(limit))
            {
                result.Add(new ToppingRecommendationDto
                {
                    ToppingId = t.ToppingId,
                    ToppingCode = t.ToppingCode,
                    ToppingName = t.ToppingName,
                    Price = t.Price,
                    Percentage = idx < samplePercentages.Length ? samplePercentages[idx] : 20.0m,
                    OrderCount = 0
                });
                idx++;
            }
        }

        _cache?.Set(cacheKey, result, TimeSpan.FromMinutes(30));
        return result;
    }
}
