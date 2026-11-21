using CafeManagement.Core.DTOs.Inventory;

namespace CafeManagement.Wpf.Services.Http;

public class StockTransferApiClient
{
    private static readonly Lazy<StockTransferApiClient> _instance = new(() => new StockTransferApiClient());
    public static StockTransferApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public StockTransferApiClient() : this(ApiClient.Instance)
    {
    }

    public StockTransferApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<List<StockTransferDto>> GetTransfersAsync(int? branchId = null, string status = "ALL")
    {
        try
        {
            string url = $"/api/stocktransfers?status={Uri.EscapeDataString(status)}";
            if (branchId.HasValue && branchId.Value > 0)
            {
                url += $"&branchId={branchId.Value}";
            }
            var list = await _api.GetAsync<List<StockTransferDto>>(url);
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<StockTransferDto?> GetTransferByIdAsync(int transferId)
    {
        try
        {
            return await _api.GetAsync<StockTransferDto>($"/api/stocktransfers/{transferId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<StockTransferDto?> CreateTransferRequestAsync(CreateStockTransferRequest req)
    {
        return await _api.PostAsync<CreateStockTransferRequest, StockTransferDto>("/api/stocktransfers/request", req);
    }

    public async Task<StockTransferDto?> ApproveTransferAsync(ApproveStockTransferRequest req)
    {
        return await _api.PostAsync<ApproveStockTransferRequest, StockTransferDto>("/api/stocktransfers/approve", req);
    }

    public async Task<StockTransferDto?> DispatchTransferAsync(DispatchStockTransferRequest req)
    {
        return await _api.PostAsync<DispatchStockTransferRequest, StockTransferDto>("/api/stocktransfers/dispatch", req);
    }

    public async Task<StockTransferDto?> ReceiveAndReconcileAsync(ReceiveAndReconcileTransferRequest req)
    {
        return await _api.PostAsync<ReceiveAndReconcileTransferRequest, StockTransferDto>("/api/stocktransfers/receive", req);
    }

    public async Task<bool> CancelTransferAsync(int transferId, string? reason)
    {
        try
        {
            string url = $"/api/stocktransfers/{transferId}/cancel";
            if (!string.IsNullOrEmpty(reason))
            {
                url += $"?reason={Uri.EscapeDataString(reason)}";
            }
            return await _api.PostAsync(url, new { });
        }
        catch
        {
            return false;
        }
    }
}
