using CafeManagement.Core.DTOs.Categories;
using CafeManagement.Core.DTOs.Products;
using CafeManagement.Core.DTOs.Toppings;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Services.Http;

public class ProductApiClient
{
    private static readonly Lazy<ProductApiClient> _instance = new(() => new ProductApiClient());
    public static ProductApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public ProductApiClient() : this(ApiClient.Instance)
    {
    }

    public ProductApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<List<ProductCategory>> GetCategoriesAsync(bool activeOnly = false)
    {
        var dtos = await _api.GetAsync<List<CategoryDto>>($"/api/categories?activeOnly={activeOnly}");
        if (dtos == null) return [];

        return dtos.Select(d => new ProductCategory
        {
            CategoryId = d.CategoryId,
            CategoryCode = d.CategoryCode,
            CategoryName = d.CategoryName,
            DisplayOrder = d.DisplayOrder,
            IsActive = d.IsActive
        }).ToList();
    }

    public async Task<CategoryDto?> CreateCategoryAsync(CategoryCreateRequest req)
    {
        return await _api.PostAsync<CategoryCreateRequest, CategoryDto>("/api/categories", req);
    }

    public async Task<CategoryDto?> UpdateCategoryAsync(int id, CategoryUpdateRequest req)
    {
        return await _api.PutAsync<CategoryUpdateRequest, CategoryDto>($"/api/categories/{id}", req);
    }

    public async Task<List<Product>> GetProductsAsync(int? categoryId = null, bool activeOnly = false)
    {
        string url = $"/api/products?activeOnly={activeOnly}";
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            url += $"&categoryId={categoryId.Value}";
        }

        var dtos = await _api.GetAsync<List<ProductDto>>(url);
        if (dtos == null) return [];

        return dtos.Select(MapDtoToProduct).ToList();
    }

    public async Task<List<Topping>> GetToppingsAsync(bool activeOnly = false)
    {
        // Khi offline, lập tức lấy từ cache mà không chờ timeout HTTP 15s
        if (!Offline.OfflineSyncManager.Instance.IsOnline)
        {
            return Offline.OfflineStorageService.Instance.GetCachedToppings();
        }

        try
        {
            var dtos = await _api.GetAsync<List<ToppingDto>>($"/api/products/toppings?activeOnly={activeOnly}");
            if (dtos != null && dtos.Count > 0)
            {
                return dtos.Select(t => new Topping
                {
                    ToppingId = t.ToppingId,
                    ToppingCode = t.ToppingCode,
                    ToppingName = t.ToppingName,
                    Price = t.Price,
                    IsActive = t.IsActive
                }).ToList();
            }
        }
        catch
        {
            // Mất mạng hoặc lỗi mạng: Tự động trả về danh sách Topping từ Cache cục bộ
        }

        return Offline.OfflineStorageService.Instance.GetCachedToppings();
    }

    public async Task<List<ToppingDto>> GetToppingDtosAsync(bool activeOnly = false)
    {
        var dtos = await _api.GetAsync<List<ToppingDto>>($"/api/products/toppings?activeOnly={activeOnly}");
        return dtos ?? [];
    }

    public async Task<ToppingDto?> CreateToppingAsync(ToppingCreateRequest req)
    {
        return await _api.PostAsync<ToppingCreateRequest, ToppingDto>("/api/products/toppings", req);
    }

    public async Task<ToppingDto?> UpdateToppingAsync(int id, ToppingUpdateRequest req)
    {
        return await _api.PutAsync<ToppingUpdateRequest, ToppingDto>($"/api/products/toppings/{id}", req);
    }

    public async Task<Product?> CreateProductAsync(ProductCreateRequest req)
    {
        var dto = await _api.PostAsync<ProductCreateRequest, ProductDto>("/api/products", req);
        return dto == null ? null : MapDtoToProduct(dto);
    }

    public async Task<Product?> UpdateProductAsync(int id, ProductUpdateRequest req)
    {
        var response = await _api.PostAsync<ProductUpdateRequest, ProductDto>($"/api/products/{id}", req);
        return response == null ? null : MapDtoToProduct(response);
    }

    public async Task<bool> ToggleProductActiveAsync(int id)
    {
        try
        {
            return await _api.PostAsync($"/api/products/{id}/toggle-active", new { });
        }
        catch
        {
            return false;
        }
    }

    private static Product MapDtoToProduct(ProductDto d)
    {
        var cat = new ProductCategory
        {
            CategoryId = d.CategoryId,
            CategoryName = d.CategoryName
        };

        var product = new Product
        {
            ProductId = d.ProductId,
            CategoryId = d.CategoryId,
            Category = cat,
            ProductCode = d.ProductCode,
            ProductName = d.ProductName,
            Description = d.Description,
            BasePrice = d.BasePrice,
            ImageUrl = d.ImageUrl,
            IsActive = d.IsActive,
            Variants = d.Variants.Select(v => new ProductVariant
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

        foreach (var v in product.Variants)
        {
            v.Product = product;
        }

        return product;
    }
}
