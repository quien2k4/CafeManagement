using CafeManagement.Core.DTOs.Orders;

namespace CafeManagement.Api.Services.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest req);
    Task<List<OrderDto>> GetOrdersAsync(int? branchId = null, DateTime? from = null, DateTime? to = null);
    Task<OrderDto?> GetOrderByIdAsync(long id);
}
