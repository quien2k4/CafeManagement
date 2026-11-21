using CafeManagement.Core.DTOs.Inventory;

namespace CafeManagement.Api.Services.Interfaces;

public interface IStockTransferService
{
    Task<List<StockTransferDto>> GetTransfersAsync(int? branchId = null, string status = "ALL");
    Task<StockTransferDto?> GetTransferByIdAsync(int transferId);
    Task<StockTransferDto> CreateTransferRequestAsync(CreateStockTransferRequest req, int userId);
    Task<StockTransferDto> ApproveTransferRequestAsync(ApproveStockTransferRequest req, int userId);
    Task<StockTransferDto> DispatchTransferAsync(DispatchStockTransferRequest req, int userId);
    Task<StockTransferDto> ReceiveAndReconcileAsync(ReceiveAndReconcileTransferRequest req, int userId);
    Task<bool> CancelTransferAsync(int transferId, int userId, string reason);
}
