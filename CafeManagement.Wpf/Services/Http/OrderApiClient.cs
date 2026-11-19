using CafeManagement.Core.DTOs.Orders;

namespace CafeManagement.Wpf.Services.Http;

public class OrderApiClient
{
    private static readonly Lazy<OrderApiClient> _instance = new(() => new OrderApiClient());
    public static OrderApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public OrderApiClient() : this(ApiClient.Instance)
    {
    }

    public OrderApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<OrderDto?> CreateOrderAsync(CreateOrderRequest req)
    {
        return await _api.PostAsync<CreateOrderRequest, OrderDto>("/api/orders", req);
    }

    public async Task<List<OrderDto>> GetOrdersAsync(int? branchId = null, DateTime? from = null, DateTime? to = null)
    {
        string url = "/api/orders";
        var queryParams = new List<string>();
        if (branchId.HasValue && branchId.Value > 0) queryParams.Add($"branchId={branchId.Value}");
        if (from.HasValue) queryParams.Add($"from={from.Value:O}");
        if (to.HasValue) queryParams.Add($"to={to.Value:O}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        var list = await _api.GetAsync<List<OrderDto>>(url);
        return list ?? [];
    }
}
