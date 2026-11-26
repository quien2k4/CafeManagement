using CafeManagement.Core.DTOs.Products;
using CafeManagement.Core.DTOs.Toppings;

namespace CafeManagement.Api.Services.Interfaces;

public interface IProductService
{
    Task<List<ProductDto>> GetProductsAsync(int? categoryId = null, bool activeOnly = false);
    Task<ProductDto?> GetProductByIdAsync(int id);
    Task<ProductDto> CreateProductAsync(ProductCreateRequest req);
    Task<ProductDto?> UpdateProductAsync(int id, ProductUpdateRequest req);
    Task<bool> ToggleProductActiveAsync(int id);
    Task<List<ToppingDto>> GetToppingsAsync(bool activeOnly = false);
    Task<ToppingDto?> GetToppingByIdAsync(int id);
    Task<ToppingDto> CreateToppingAsync(ToppingCreateRequest req);
    Task<ToppingDto?> UpdateToppingAsync(int id, ToppingUpdateRequest req);
    Task<List<ToppingRecommendationDto>> GetRecommendedToppingsAsync(int productId, int limit = 3);
}
