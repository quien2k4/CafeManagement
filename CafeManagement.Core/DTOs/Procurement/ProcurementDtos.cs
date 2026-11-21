namespace CafeManagement.Core.DTOs.Procurement;

// ==========================================
// DTOs CHO NHÀ CUNG CẤP & BẢNG GIÁ
// ==========================================

public class SupplierDto
{
    public int SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
    public string StatusBadge => IsActive ? "🟢 Đang hợp tác" : "⚪ Tạm dừng";
}

public class CreateOrUpdateSupplierRequest
{
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SupplierIngredientDto
{
    public int SupplierIngredientId { get; set; }
    public int SupplierId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUom { get; set; } = "g";
    public decimal SupplyPrice { get; set; }
    public string PackageUom { get; set; } = "Gói";
    public decimal ConversionFactor { get; set; } = 1.0m;
    public decimal MinOrderQuantity { get; set; } = 1.0m;
    public int LeadTimeDays { get; set; } = 1;
    public bool IsPreferred { get; set; }
}

public class SetSupplierIngredientRequest
{
    public int SupplierId { get; set; }
    public int IngredientId { get; set; }
    public decimal SupplyPrice { get; set; }
    public string PackageUom { get; set; } = "Gói";
    public decimal ConversionFactor { get; set; } = 1.0m;
    public decimal MinOrderQuantity { get; set; } = 1.0m;
    public int LeadTimeDays { get; set; } = 1;
    public bool IsPreferred { get; set; }
}

// ==========================================
// DTOs CHO ĐƠN ĐẶT HÀNG MUA (PO)
// ==========================================

public class PurchaseOrderDto
{
    public int PoId { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT"; // DRAFT, ORDERED, PARTIALLY_RECEIVED, COMPLETED, CANCELLED
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public int ItemCount { get; set; }

    public double ReceivedPercent { get; set; }

    public string StatusBadge => Status switch
    {
        "DRAFT" => "📝 Bản nháp",
        "ORDERED" => "📤 Đã đặt hàng",
        "PARTIALLY_RECEIVED" => "⏳ Mới nhận 1 phần",
        "COMPLETED" => "✅ Hoàn tất nhận hàng",
        "CANCELLED" => "❌ Đã hủy",
        _ => Status
    };

    public string StatusColor => Status switch
    {
        "DRAFT" => "#7F8C8D",
        "ORDERED" => "#2980B9",
        "PARTIALLY_RECEIVED" => "#D35400",
        "COMPLETED" => "#27AE60",
        "CANCELLED" => "#C0392B",
        _ => "#2C3E50"
    };

    public List<PurchaseOrderDetailDto> Details { get; set; } = [];
}

public class PurchaseOrderDetailDto
{
    public int PoDetailId { get; set; }
    public int PoId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal RemainingQuantity => Math.Max(0, OrderedQuantity - ReceivedQuantity);
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    public string Uom { get; set; } = "g";
}

public class CreatePurchaseOrderItemRequest
{
    public int IngredientId { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Uom { get; set; } = "g";
}

public class CreatePurchaseOrderRequest
{
    public int SupplierId { get; set; }
    public int BranchId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public int CreatedByUserId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string? Note { get; set; }
    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = [];
}

// ==========================================
// DTOs CHO TIẾP NHẬN HÀNG HÓA 2 BƯỚC (QC)
// ==========================================

public class GoodsReceiptDto
{
    public int ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public int PoId { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public int ReceivedByUserId { get; set; }
    public string ReceivedByName { get; set; } = string.Empty;
    public string? DeliveryPerson { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal TotalAcceptedAmount { get; set; }
    public string? Note { get; set; }
    public List<GoodsReceiptDetailDto> Details { get; set; } = [];
}

public class GoodsReceiptDetailDto
{
    public int ReceiptDetailId { get; set; }
    public int ReceiptId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal DeliveredQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal QuarantinedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? RejectionReason { get; set; }
    public decimal UnitPrice { get; set; }
    public string Uom { get; set; } = "g";
    public decimal TotalAcceptedPrice => AcceptedQuantity * UnitPrice;
}

public class CreateGoodsReceiptItemRequest
{
    public int IngredientId { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal QuarantinedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? RejectionReason { get; set; }
    public decimal UnitPrice { get; set; }
    public string Uom { get; set; } = "g";
}

public class CreateGoodsReceiptRequest
{
    public int PoId { get; set; }
    public int BranchId { get; set; }
    public int ReceivedByUserId { get; set; }
    public string? DeliveryPerson { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Note { get; set; }
    public List<CreateGoodsReceiptItemRequest> Items { get; set; } = [];
}
