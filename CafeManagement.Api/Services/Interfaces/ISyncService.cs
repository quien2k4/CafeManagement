using CafeManagement.Core.DTOs.Sync;

namespace CafeManagement.Api.Services.Interfaces;

public interface ISyncService
{
    Task<HealthCheckResponse> CheckHealthAsync();
    Task<SyncBatchResponse> SyncBatchOrdersAsync(SyncBatchRequest batch);
    Task<SyncItemResult> ResolveConflictAsync(ResolveConflictRequest req);
}
