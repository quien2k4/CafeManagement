using CafeManagement.Core.DTOs.Procurement;

namespace CafeManagement.Wpf.Services.Http;

public class ProcurementApiClient
{
    private static readonly Lazy<ProcurementApiClient> _instance = new(() => new ProcurementApiClient());
    public static ProcurementApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public ProcurementApiClient() : this(ApiClient.Instance)
    {
    }

    public ProcurementApiClient(ApiClient api)
    {
        _api = api;
    }

    #region 1. Nhà Cung Cấp
    public async Task<List<SupplierDto>> GetSuppliersAsync(bool activeOnly = false)
    {
        try
        {
            var list = await _api.GetAsync<List<SupplierDto>>($"/api/procurement/suppliers?activeOnly={activeOnly}");
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(int id)
    {
        try
        {
            return await _api.GetAsync<SupplierDto>($"/api/procurement/suppliers/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<SupplierDto?> CreateSupplierAsync(CreateOrUpdateSupplierRequest req)
    {
        return await _api.PostAsync<CreateOrUpdateSupplierRequest, SupplierDto>("/api/procurement/suppliers", req);
    }

    public async Task<SupplierDto?> UpdateSupplierAsync(int id, CreateOrUpdateSupplierRequest req)
    {
        return await _api.PutAsync<CreateOrUpdateSupplierRequest, SupplierDto>($"/api/procurement/suppliers/{id}", req);
    }

    public async Task<bool> DeleteSupplierAsync(int id)
    {
        try
        {
            return await _api.DeleteAsync($"/api/procurement/suppliers/{id}");
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region 2. Bảng Giá & Mặt Hàng Cung Ứng
    public async Task<List<SupplierIngredientDto>> GetSupplierIngredientsAsync(int supplierId)
    {
        try
        {
            var list = await _api.GetAsync<List<SupplierIngredientDto>>($"/api/procurement/suppliers/{supplierId}/ingredients");
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<SupplierIngredientDto?> SetSupplierIngredientAsync(SetSupplierIngredientRequest req)
    {
        return await _api.PostAsync<SetSupplierIngredientRequest, SupplierIngredientDto>("/api/procurement/suppliers/ingredients", req);
    }

    public async Task<bool> DeleteSupplierIngredientAsync(int id)
    {
        try
        {
            return await _api.DeleteAsync($"/api/procurement/suppliers/ingredients/{id}");
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region 3. Đơn Đặt Hàng Mua (PO)
    public async Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(int? branchId = null, string status = "ALL")
    {
        try
        {
            string url = $"/api/procurement/purchase-orders?status={Uri.EscapeDataString(status)}";
            if (branchId.HasValue && branchId.Value > 0)
            {
                url += $"&branchId={branchId.Value}";
            }
            var list = await _api.GetAsync<List<PurchaseOrderDto>>(url);
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<PurchaseOrderDto?> GetPurchaseOrderByIdAsync(int id)
    {
        try
        {
            return await _api.GetAsync<PurchaseOrderDto>($"/api/procurement/purchase-orders/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<PurchaseOrderDto?> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest req)
    {
        return await _api.PostAsync<CreatePurchaseOrderRequest, PurchaseOrderDto>("/api/procurement/purchase-orders", req);
    }

    public async Task<bool> UpdatePoStatusAsync(int id, string newStatus)
    {
        try
        {
            return await _api.PutAsync($"/api/procurement/purchase-orders/{id}/status?newStatus={Uri.EscapeDataString(newStatus)}", new { });
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region 4. Tiếp Nhận Hàng Hóa & QC
    public async Task<List<GoodsReceiptDto>> GetGoodsReceiptsAsync(int? branchId = null, int? poId = null)
    {
        try
        {
            string url = "/api/procurement/goods-receipts?";
            if (branchId.HasValue && branchId.Value > 0)
            {
                url += $"branchId={branchId.Value}&";
            }
            if (poId.HasValue && poId.Value > 0)
            {
                url += $"poId={poId.Value}&";
            }
            var list = await _api.GetAsync<List<GoodsReceiptDto>>(url.TrimEnd('&', '?'));
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<GoodsReceiptDto?> GetGoodsReceiptByIdAsync(int id)
    {
        try
        {
            return await _api.GetAsync<GoodsReceiptDto>($"/api/procurement/goods-receipts/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<GoodsReceiptDto?> ProcessGoodsReceiptAsync(CreateGoodsReceiptRequest req)
    {
        return await _api.PostAsync<CreateGoodsReceiptRequest, GoodsReceiptDto>("/api/procurement/goods-receipts", req);
    }
    #endregion
}
