using CafeManagement.Core.DTOs.Procurement;

namespace CafeManagement.Api.Services.Interfaces;

public interface IProcurementService
{
    // Quản lý Nhà cung cấp
    Task<List<SupplierDto>> GetSuppliersAsync(bool activeOnly = false);
    Task<SupplierDto?> GetSupplierByIdAsync(int supplierId);
    Task<SupplierDto> CreateSupplierAsync(CreateOrUpdateSupplierRequest req);
    Task<SupplierDto?> UpdateSupplierAsync(int supplierId, CreateOrUpdateSupplierRequest req);
    Task<bool> DeleteSupplierAsync(int supplierId);

    // Bảng giá & Mặt hàng cung ứng
    Task<List<SupplierIngredientDto>> GetSupplierIngredientsAsync(int supplierId);
    Task<SupplierIngredientDto> SetSupplierIngredientAsync(SetSupplierIngredientRequest req);
    Task<bool> DeleteSupplierIngredientAsync(int supplierIngredientId);

    // Đơn đặt hàng mua (PO)
    Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(int? branchId = null, string status = "ALL");
    Task<PurchaseOrderDto?> GetPurchaseOrderByIdAsync(int poId);
    Task<PurchaseOrderDto> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest req);
    Task<bool> UpdatePoStatusAsync(int poId, string newStatus);

    // Tiếp nhận hàng hóa 2 bước & QC
    Task<List<GoodsReceiptDto>> GetGoodsReceiptsAsync(int? branchId = null, int? poId = null);
    Task<GoodsReceiptDto?> GetGoodsReceiptByIdAsync(int receiptId);
    Task<GoodsReceiptDto> ProcessGoodsReceiptAsync(CreateGoodsReceiptRequest req);
}
