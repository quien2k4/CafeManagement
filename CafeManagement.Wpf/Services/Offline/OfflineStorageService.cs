using System.IO;
using System.Text.Json;
using CafeManagement.Core.Models;
using CafeManagement.Core.DTOs.Sync;

namespace CafeManagement.Wpf.Services.Offline;

public class MenuCacheData
{
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public List<ProductCategory> Categories { get; set; } = [];
    public List<Product> Products { get; set; } = [];
}

public class OfflineStorageService
{
    private static readonly Lazy<OfflineStorageService> _instance = new(() => new OfflineStorageService());
    public static OfflineStorageService Instance => _instance.Value;

    private readonly string _storageDir;
    private readonly string _pendingOrdersFile;
    private readonly string _menuCacheFile;
    private readonly object _lock = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public OfflineStorageService()
    {
        _storageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local_data");
        if (!Directory.Exists(_storageDir))
        {
            Directory.CreateDirectory(_storageDir);
        }

        _pendingOrdersFile = Path.Combine(_storageDir, "pending_orders.json");
        _menuCacheFile = Path.Combine(_storageDir, "menu_cache.json");
    }

    // ==================== HÀNG ĐỢI ĐƠN HÀNG OFFLINE ====================

    public List<OfflineOrderRecord> GetPendingOrders()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_pendingOrdersFile)) return [];
                string json = File.ReadAllText(_pendingOrdersFile);
                return JsonSerializer.Deserialize<List<OfflineOrderRecord>>(json, _jsonOptions) ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    public int GetPendingCount()
    {
        return GetPendingOrders().Count(o => o.SyncStatus == "PENDING" || o.SyncStatus == "CONFLICT" || o.SyncStatus == "FAILED");
    }

    public void SavePendingOrder(OfflineOrderRecord order)
    {
        lock (_lock)
        {
            try
            {
                var list = GetPendingOrders();
                var existing = list.FirstOrDefault(o => o.LocalId == order.LocalId);
                if (existing != null)
                {
                    list.Remove(existing);
                }
                list.Add(order);
                string json = JsonSerializer.Serialize(list, _jsonOptions);
                File.WriteAllText(_pendingOrdersFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi lưu đơn Offline: {ex.Message}");
            }
        }
    }

    public void UpdateOrderSyncStatus(string localId, string status, string? error = null, long? serverOrderId = null, string? serverOrderNumber = null)
    {
        lock (_lock)
        {
            try
            {
                var list = GetPendingOrders();
                var item = list.FirstOrDefault(o => o.LocalId == localId);
                if (item != null)
                {
                    item.SyncStatus = status;
                    item.ErrorMessage = error;
                    item.LastSyncAttempt = DateTime.UtcNow;
                    if (serverOrderId.HasValue) item.ServerOrderId = serverOrderId;
                    if (!string.IsNullOrEmpty(serverOrderNumber)) item.ServerOrderNumber = serverOrderNumber;

                    string json = JsonSerializer.Serialize(list, _jsonOptions);
                    File.WriteAllText(_pendingOrdersFile, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi cập nhật trạng thái đơn: {ex.Message}");
            }
        }
    }

    public void RemoveOrder(string localId)
    {
        lock (_lock)
        {
            try
            {
                var list = GetPendingOrders();
                var item = list.FirstOrDefault(o => o.LocalId == localId);
                if (item != null)
                {
                    list.Remove(item);
                    string json = JsonSerializer.Serialize(list, _jsonOptions);
                    File.WriteAllText(_pendingOrdersFile, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi xóa đơn: {ex.Message}");
            }
        }
    }

    // ==================== BỘ NHỚ ĐỆM MENU (CACHE) ====================

    public void SaveMenuCache(List<ProductCategory> categories, List<Product> products)
    {
        lock (_lock)
        {
            try
            {
                var data = new MenuCacheData
                {
                    CachedAt = DateTime.UtcNow,
                    Categories = categories,
                    Products = products
                };
                string json = JsonSerializer.Serialize(data, _jsonOptions);
                File.WriteAllText(_menuCacheFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi lưu cache menu: {ex.Message}");
            }
        }
    }

    public MenuCacheData? GetMenuCache()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_menuCacheFile)) return null;
                string json = File.ReadAllText(_menuCacheFile);
                return JsonSerializer.Deserialize<MenuCacheData>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }
}
