using CafeManagement.Core.DTOs.KDS;

namespace CafeManagement.Wpf.Services.Http;

public class KdsApiClient
{
    private static readonly Lazy<KdsApiClient> _instance = new(() => new KdsApiClient());
    public static KdsApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public KdsApiClient() : this(ApiClient.Instance)
    {
    }

    public KdsApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<List<KdsTicketDto>> GetActiveTicketsAsync(int branchId)
    {
        try
        {
            var tickets = await _api.GetAsync<List<KdsTicketDto>>($"/api/kds/tickets?branchId={branchId}");
            return tickets ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> UpdateItemStatusAsync(long orderItemId, string status)
    {
        try
        {
            return await _api.PostAsync($"/api/kds/items/{orderItemId}/status", new UpdateKdsItemStatusRequest { Status = status });
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CompleteOrderAsync(long orderId)
    {
        try
        {
            return await _api.PostAsync($"/api/kds/orders/{orderId}/complete", new { });
        }
        catch
        {
            return false;
        }
    }
}
