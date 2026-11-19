using CafeManagement.Core.DTOs.Payments;

namespace CafeManagement.Wpf.Services.Http;

public class RefundApiClient
{
    private static readonly Lazy<RefundApiClient> _instance = new(() => new RefundApiClient());
    public static RefundApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public RefundApiClient() : this(ApiClient.Instance)
    {
    }

    public RefundApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<RefundDto?> CreateRefundAsync(RefundCreateRequest req)
    {
        return await _api.PostAsync<RefundCreateRequest, RefundDto>("/api/refunds", req);
    }

    public async Task<List<RefundDto>> GetRefundsByOrderIdAsync(long orderId)
    {
        try
        {
            var list = await _api.GetAsync<List<RefundDto>>($"/api/refunds/orders/{orderId}");
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }
}
