using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Sync;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;

    public SyncController(ISyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpGet("health")]
    public async Task<ActionResult<HealthCheckResponse>> CheckHealth()
    {
        var res = await _syncService.CheckHealthAsync();
        return Ok(res);
    }

    [HttpPost("orders")]
    public async Task<ActionResult<SyncBatchResponse>> SyncOrders([FromBody] SyncBatchRequest batch)
    {
        var res = await _syncService.SyncBatchOrdersAsync(batch);
        return Ok(res);
    }

    [HttpPost("resolve-conflict")]
    public async Task<ActionResult<SyncItemResult>> ResolveConflict([FromBody] ResolveConflictRequest req)
    {
        var res = await _syncService.ResolveConflictAsync(req);
        return Ok(res);
    }
}
