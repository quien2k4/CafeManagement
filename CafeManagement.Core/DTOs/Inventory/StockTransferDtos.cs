namespace CafeManagement.Core.DTOs.Inventory;

public class StockTransferDetailDto
{
    public int DetailId { get; set; }
    public int TransferId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string IngredientCode { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal ApprovedQuantity { get; set; }
    public decimal DispatchedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal DiscrepancyQuantity { get; set; }
    public string? DiscrepancyReason { get; set; }
    public string Uom { get; set; } = "g";

    public bool HasDiscrepancy => DiscrepancyQuantity != 0;
}

public class StockTransferDto
{
    public int TransferId { get; set; }
    public string TransferCode { get; set; } = string.Empty;
    public int FromBranchId { get; set; }
    public string FromBranchName { get; set; } = string.Empty;
    public int ToBranchId { get; set; }
    public string ToBranchName { get; set; } = string.Empty;
    public string Status { get; set; } = "REQUESTED";

    public DateTime RequestedDate { get; set; }
    public int RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;

    public DateTime? ApprovedDate { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }

    public DateTime? DispatchedDate { get; set; }
    public int? DispatchedByUserId { get; set; }
    public string? DispatchedByName { get; set; }

    public DateTime? ReceivedDate { get; set; }
    public int? ReceivedByUserId { get; set; }
    public string? ReceivedByName { get; set; }

    public string? RequestNote { get; set; }
    public string? ApprovalNote { get; set; }
    public string? ReconciliationNote { get; set; }

    public List<StockTransferDetailDto> Details { get; set; } = [];

    public int TotalItemsCount => Details.Count;
    public bool HasDiscrepancy => Details.Any(d => d.DiscrepancyQuantity != 0);

    public string StatusBadgeText => Status switch
    {
        "REQUESTED" => "⏳ Chờ duyệt",
        "APPROVED" => "✔️ Đã duyệt (Chờ xuất)",
        "REJECTED" => "❌ Đã từ chối",
        "IN_TRANSIT" => "🚚 Đang vận chuyển",
        "COMPLETED" => "✅ Đã nhận đủ",
        "COMPLETED_DISCREPANCY" => "⚠️ Đã nhận (Có sai lệch)",
        "CANCELLED" => "🚫 Đã hủy",
        _ => Status
    };

    public string StatusColorHex => Status switch
    {
        "REQUESTED" => "#D97706",         // Amber
        "APPROVED" => "#2563EB",          // Blue
        "REJECTED" => "#DC2626",          // Red
        "IN_TRANSIT" => "#8B5CF6",         // Purple
        "COMPLETED" => "#16A34A",          // Green
        "COMPLETED_DISCREPANCY" => "#EA580C", // Orange-Red
        "CANCELLED" => "#64748B",          // Slate
        _ => "#475569"
    };
}

public class CreateTransferItemRequest
{
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "g";
}

public class CreateStockTransferRequest
{
    public int FromBranchId { get; set; }
    public int ToBranchId { get; set; }
    public string? RequestNote { get; set; }
    public List<CreateTransferItemRequest> Items { get; set; } = [];
}

public class ApproveTransferItemRequest
{
    public int DetailId { get; set; }
    public decimal ApprovedQuantity { get; set; }
}

public class ApproveStockTransferRequest
{
    public int TransferId { get; set; }
    public bool IsApproved { get; set; } = true;
    public string? ApprovalNote { get; set; }
    public List<ApproveTransferItemRequest>? Items { get; set; }
}

public class DispatchStockTransferRequest
{
    public int TransferId { get; set; }
    public string? DispatchedNote { get; set; }
}

public class ReconcileTransferItemRequest
{
    public int DetailId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string? DiscrepancyReason { get; set; }
}

public class ReceiveAndReconcileTransferRequest
{
    public int TransferId { get; set; }
    public string? ReconciliationNote { get; set; }
    public List<ReconcileTransferItemRequest> Items { get; set; } = [];
}
