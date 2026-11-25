using CafeManagement.Core.DTOs.Orders;

namespace CafeManagement.Core.DTOs.Sync;

/// <summary>
/// Bản ghi đơn hàng Offline được lưu cục bộ trên máy POS
/// </summary>
public class OfflineOrderRecord
{
    public string LocalId { get; set; } = Guid.NewGuid().ToString("N");
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public CreateOrderRequest OrderRequest { get; set; } = new();
    
    /// <summary>
    /// PENDING, SYNCED, CONFLICT, FAILED
    /// </summary>
    public string SyncStatus { get; set; } = "PENDING";
    public int SyncAttempts { get; set; } = 0;
    public DateTime? LastSyncAttempt { get; set; }
    public string? ErrorMessage { get; set; }

    public long? ServerOrderId { get; set; }
    public string? ServerOrderNumber { get; set; }
}

/// <summary>
/// Gói tin gửi hàng loạt đơn hàng Offline lên Server để đồng bộ
/// </summary>
public class SyncBatchRequest
{
    public int BranchId { get; set; }
    public string MachineName { get; set; } = Environment.MachineName;
    public List<OfflineOrderRecord> Orders { get; set; } = [];
}

/// <summary>
/// Chi tiết xung đột dữ liệu giữa Local POS và Server
/// </summary>
public class SyncConflictDetailDto
{
    public string LocalId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public long? ServerOrderId { get; set; }
    public string ConflictReason { get; set; } = string.Empty;

    // Dữ liệu Local
    public decimal LocalSubTotal { get; set; }
    public decimal LocalDiscount { get; set; }
    public decimal LocalFinalAmount { get; set; }
    public string LocalPaymentMethod { get; set; } = "CASH";
    public string LocalItemsSummary { get; set; } = string.Empty;
    public DateTime LocalCreatedAt { get; set; }

    // Dữ liệu Server
    public decimal ServerSubTotal { get; set; }
    public decimal ServerDiscount { get; set; }
    public decimal ServerFinalAmount { get; set; }
    public string ServerPaymentMethod { get; set; } = "CASH";
    public string ServerItemsSummary { get; set; } = string.Empty;
    public DateTime ServerCreatedAt { get; set; }
    public string ServerStatus { get; set; } = "PAID";
}

/// <summary>
/// Kết quả đồng bộ cho từng đơn hàng
/// </summary>
public class SyncItemResult
{
    public string LocalId { get; set; } = string.Empty;
    /// <summary>
    /// SUCCESS, CONFLICT, ERROR
    /// </summary>
    public string Status { get; set; } = "SUCCESS";
    public long? ServerOrderId { get; set; }
    public string? ServerOrderNumber { get; set; }
    public string? Message { get; set; }
    public SyncConflictDetailDto? ConflictDetail { get; set; }
}

/// <summary>
/// Kết quả trả về sau khi đồng bộ lô
/// </summary>
public class SyncBatchResponse
{
    public int TotalSubmitted { get; set; }
    public int SuccessCount { get; set; }
    public int ConflictCount { get; set; }
    public int ErrorCount { get; set; }
    public List<SyncItemResult> Results { get; set; } = [];
}

/// <summary>
/// Yêu cầu giải quyết xung đột từ phía người dùng (Use Local / Use Server)
/// </summary>
public class ResolveConflictRequest
{
    public string LocalId { get; set; } = string.Empty;
    public long? ServerOrderId { get; set; }
    /// <summary>
    /// USE_LOCAL (Ghi đè Server bằng dữ liệu quầy), USE_SERVER (Bỏ dữ liệu quầy, lấy dữ liệu Server)
    /// </summary>
    public string ResolutionChoice { get; set; } = "USE_LOCAL";
    public CreateOrderRequest? LocalOrderData { get; set; }
}

/// <summary>
/// Thông tin phản hồi kiểm tra kết nối mạng Server
/// </summary>
public class HealthCheckResponse
{
    public string Status { get; set; } = "ONLINE";
    public DateTime ServerTime { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "1.0.0";
}
