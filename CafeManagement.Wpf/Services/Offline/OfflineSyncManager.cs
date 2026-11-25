using System.Windows.Threading;
using CafeManagement.Core.DTOs.Orders;
using CafeManagement.Core.DTOs.Sync;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.Services.Offline;

public class OfflineSyncManager
{
    private static readonly Lazy<OfflineSyncManager> _instance = new(() => new OfflineSyncManager());
    public static OfflineSyncManager Instance => _instance.Value;

    private readonly OfflineStorageService _storage;
    private readonly SyncApiClient _api;
    private readonly DispatcherTimer _heartbeatTimer;
    private bool _isSyncing = false;

    public bool IsOnline { get; private set; } = true;
    public int PendingSyncCount => _storage.GetPendingCount();

    public event Action<bool>? ConnectionStatusChanged;
    public event Action<int>? PendingCountChanged;
    public event Action<SyncConflictDetailDto>? ConflictDetected;

    public OfflineSyncManager()
    {
        _storage = OfflineStorageService.Instance;
        _api = SyncApiClient.Instance;

        _heartbeatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(8)
        };
        _heartbeatTimer.Tick += async (s, e) => await CheckConnectionAsync();
    }

    public void StartMonitoring()
    {
        _heartbeatTimer.Start();
        _ = CheckConnectionAsync();
    }

    public void StopMonitoring()
    {
        _heartbeatTimer.Stop();
    }

    public async Task<bool> CheckConnectionAsync()
    {
        bool onlineNow = await _api.CheckHealthAsync();
        bool statusChanged = (onlineNow != IsOnline);
        IsOnline = onlineNow;

        if (statusChanged)
        {
            ConnectionStatusChanged?.Invoke(IsOnline);
        }

        // Nếu vừa có mạng lại và còn đơn chờ -> Kích hoạt tự động đồng bộ ngay
        if (IsOnline && PendingSyncCount > 0 && !_isSyncing)
        {
            await TriggerSyncAsync();
        }

        PendingCountChanged?.Invoke(PendingSyncCount);
        return IsOnline;
    }

    /// <summary>
    /// Lưu đơn hàng khi mất mạng hoặc khi gửi API thất bại
    /// </summary>
    public OfflineOrderRecord CreateOfflineOrder(CreateOrderRequest req)
    {
        string localId = Guid.NewGuid().ToString("N");
        string orderNumber = $"OFF-B{req.BranchId}-{DateTime.Now:yyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

        var record = new OfflineOrderRecord
        {
            LocalId = localId,
            OrderNumber = orderNumber,
            CreatedAt = DateTime.UtcNow,
            OrderRequest = req,
            SyncStatus = "PENDING"
        };

        _storage.SavePendingOrder(record);
        PendingCountChanged?.Invoke(PendingSyncCount);

        // Thử đồng bộ nếu mạng thông
        if (IsOnline && !_isSyncing)
        {
            _ = TriggerSyncAsync();
        }

        return record;
    }

    /// <summary>
    /// Kích hoạt tiến trình đồng bộ các đơn đang chờ
    /// </summary>
    public async Task TriggerSyncAsync()
    {
        if (_isSyncing) return;
        _isSyncing = true;

        try
        {
            var pendingOrders = _storage.GetPendingOrders()
                .Where(o => o.SyncStatus == "PENDING" || o.SyncStatus == "FAILED")
                .ToList();

            if (pendingOrders.Count == 0) return;

            var batch = new SyncBatchRequest
            {
                BranchId = pendingOrders.First().OrderRequest.BranchId,
                Orders = pendingOrders
            };

            var response = await _api.SyncBatchAsync(batch);
            if (response != null)
            {
                foreach (var res in response.Results)
                {
                    if (res.Status == "SUCCESS")
                    {
                        _storage.RemoveOrder(res.LocalId);
                    }
                    else if (res.Status == "CONFLICT" && res.ConflictDetail != null)
                    {
                        _storage.UpdateOrderSyncStatus(res.LocalId, "CONFLICT", res.Message, res.ServerOrderId, res.ServerOrderNumber);
                        ConflictDetected?.Invoke(res.ConflictDetail);
                    }
                    else if (res.Status == "ERROR")
                    {
                        _storage.UpdateOrderSyncStatus(res.LocalId, "FAILED", res.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi tiến trình đồng bộ: {ex.Message}");
        }
        finally
        {
            _isSyncing = false;
            PendingCountChanged?.Invoke(PendingSyncCount);
        }
    }

    /// <summary>
    /// Giải quyết xung đột dữ liệu (Use Local / Use Server)
    /// </summary>
    public async Task<bool> ResolveConflictAsync(string localId, long? serverOrderId, string choice)
    {
        var localRecord = _storage.GetPendingOrders().FirstOrDefault(o => o.LocalId == localId);

        var req = new ResolveConflictRequest
        {
            LocalId = localId,
            ServerOrderId = serverOrderId,
            ResolutionChoice = choice,
            LocalOrderData = localRecord?.OrderRequest
        };

        var result = await _api.ResolveConflictAsync(req);
        if (result != null && result.Status == "SUCCESS")
        {
            _storage.RemoveOrder(localId);
            PendingCountChanged?.Invoke(PendingSyncCount);
            return true;
        }

        return false;
    }
}
