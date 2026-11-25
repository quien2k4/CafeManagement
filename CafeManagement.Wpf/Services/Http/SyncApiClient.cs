using CafeManagement.Core.DTOs.Sync;

namespace CafeManagement.Wpf.Services.Http;

public class SyncApiClient
{
    private static readonly Lazy<SyncApiClient> _instance = new(() => new SyncApiClient());
    public static SyncApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public SyncApiClient() : this(ApiClient.Instance)
    {
    }

    public SyncApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var res = await _api.GetAsync<HealthCheckResponse>("/api/sync/health");
            return res != null && res.Status == "ONLINE";
        }
        catch
        {
            return false;
        }
    }

    public async Task<SyncBatchResponse?> SyncBatchAsync(SyncBatchRequest batch)
    {
        try
        {
            return await _api.PostAsync<SyncBatchRequest, SyncBatchResponse>("/api/sync/orders", batch);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SyncBatchAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<SyncItemResult?> ResolveConflictAsync(ResolveConflictRequest req)
    {
        try
        {
            return await _api.PostAsync<ResolveConflictRequest, SyncItemResult>("/api/sync/resolve-conflict", req);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ResolveConflictAsync error: {ex.Message}");
            return null;
        }
    }
}
