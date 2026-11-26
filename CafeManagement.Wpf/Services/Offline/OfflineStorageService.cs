using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CafeManagement.Core.Models;
using CafeManagement.Core.DTOs.Sync;
using CafeManagement.Core.DTOs.Inventory;

namespace CafeManagement.Wpf.Services.Offline;

public class MenuCacheData
{
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public List<ProductCategory> Categories { get; set; } = [];
    public List<Product> Products { get; set; } = [];
    public List<Topping> Toppings { get; set; } = [];
    public List<BranchInventoryDto> Inventories { get; set; } = [];
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
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
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

    // ==================== BỘ NHỚ ĐỆM MENU & NGUYÊN LIỆU (CACHE) ====================

    public void SaveMenuCache(
        List<ProductCategory> categories, 
        List<Product> products, 
        List<Topping>? toppings = null,
        List<BranchInventoryDto>? inventories = null)
    {
        lock (_lock)
        {
            try
            {
                // Nếu không truyền toppings hoặc inventories mới thì giữ lại bản đang có trong cache
                var current = GetMenuCache();

                var data = new MenuCacheData
                {
                    CachedAt = DateTime.UtcNow,
                    Categories = categories,
                    Products = products,
                    Toppings = toppings ?? current?.Toppings ?? [],
                    Inventories = inventories ?? current?.Inventories ?? []
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

    public List<Topping> GetCachedToppings()
    {
        var cache = GetMenuCache();
        return cache?.Toppings ?? [];
    }

    public List<BranchInventoryDto> GetCachedInventories()
    {
        var cache = GetMenuCache();
        return cache?.Inventories ?? [];
    }

    public void SaveCachedInventories(List<BranchInventoryDto> inventories)
    {
        lock (_lock)
        {
            try
            {
                var current = GetMenuCache() ?? new MenuCacheData();
                current.CachedAt = DateTime.UtcNow;
                current.Inventories = inventories;
                string json = JsonSerializer.Serialize(current, _jsonOptions);
                File.WriteAllText(_menuCacheFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi lưu cache nguyên liệu: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Nhập bổ sung số lượng nguyên liệu trong bộ nhớ đệm khi đang Offline
    /// </summary>
    public bool UpdateCachedStock(int ingredientId, decimal quantityToAdd)
    {
        lock (_lock)
        {
            try
            {
                var current = GetMenuCache();
                if (current == null || current.Inventories == null) return false;

                var item = current.Inventories.FirstOrDefault(i => i.IngredientId == ingredientId);
                if (item != null)
                {
                    item.QuantityOnHand += quantityToAdd;
                    current.CachedAt = DateTime.UtcNow;
                    string json = JsonSerializer.Serialize(current, _jsonOptions);
                    File.WriteAllText(_menuCacheFile, json);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi nhập nguyên liệu trong bộ nhớ đệm: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Đặt lại trực tiếp số lượng tồn kho nguyên liệu trong bộ nhớ đệm
    /// </summary>
    public bool SetCachedStock(int ingredientId, decimal newQuantity)
    {
        lock (_lock)
        {
            try
            {
                var current = GetMenuCache();
                if (current == null || current.Inventories == null) return false;

                var item = current.Inventories.FirstOrDefault(i => i.IngredientId == ingredientId);
                if (item != null)
                {
                    item.QuantityOnHand = Math.Max(0, newQuantity);
                    current.CachedAt = DateTime.UtcNow;
                    string json = JsonSerializer.Serialize(current, _jsonOptions);
                    File.WriteAllText(_menuCacheFile, json);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi gán tồn kho bộ nhớ đệm: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Thêm hoặc cập nhật một nguyên liệu mới vào bộ nhớ đệm
    /// </summary>
    public bool AddOrUpdateCachedIngredient(BranchInventoryDto item)
    {
        lock (_lock)
        {
            try
            {
                var current = GetMenuCache() ?? new MenuCacheData();
                current.CachedAt = DateTime.UtcNow;
                if (current.Inventories == null) current.Inventories = [];

                var existing = current.Inventories.FirstOrDefault(i =>
                    (item.IngredientId > 0 && i.IngredientId == item.IngredientId) ||
                    (!string.IsNullOrEmpty(item.IngredientCode) && i.IngredientCode.Equals(item.IngredientCode, StringComparison.OrdinalIgnoreCase)));

                if (existing != null)
                {
                    existing.QuantityOnHand = item.QuantityOnHand;
                    existing.IngredientName = item.IngredientName;
                    existing.BaseUom = item.BaseUom;
                    existing.MinStockAlert = item.MinStockAlert;
                }
                else
                {
                    if (item.IngredientId <= 0)
                    {
                        int maxId = current.Inventories.Count > 0 ? current.Inventories.Max(i => i.IngredientId) : 1000;
                        item.IngredientId = maxId + 1;
                    }
                    current.Inventories.Add(item);
                }

                string json = JsonSerializer.Serialize(current, _jsonOptions);
                File.WriteAllText(_menuCacheFile, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi cập nhật nguyên liệu cache: {ex.Message}");
                return false;
            }
        }
    }
}
