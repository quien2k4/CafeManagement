using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Inventory;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Inventory;

public class StockTransferService : IStockTransferService
{
    private readonly CafeDbContext _db;
    private static bool _tablesVerified = false;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public StockTransferService(CafeDbContext db)
    {
        _db = db;
    }

    private async Task EnsureTransferTablesExistAsync()
    {
        if (_tablesVerified) return;
        await _initLock.WaitAsync();
        try
        {
            if (_tablesVerified) return;

            await _db.Database.ExecuteSqlRawAsync(@"
                -- Tự động dọn dẹp cấu trúc bảng cũ nếu thiếu các cột mới
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransfers')
                   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.StockTransfers') AND name = 'FromBranchId')
                BEGIN
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransferDetails') DROP TABLE dbo.StockTransferDetails;
                    DROP TABLE dbo.StockTransfers;
                END

                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransferDetails')
                   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.StockTransferDetails') AND name = 'DetailId')
                BEGIN
                    DROP TABLE dbo.StockTransferDetails;
                END

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransfers')
                BEGIN
                    CREATE TABLE dbo.StockTransfers (
                        TransferId INT IDENTITY(1,1) PRIMARY KEY,
                        TransferCode NVARCHAR(30) NOT NULL UNIQUE,
                        FromBranchId INT NOT NULL,
                        ToBranchId INT NOT NULL,
                        Status NVARCHAR(30) NOT NULL DEFAULT 'REQUESTED',
                        RequestedDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
                        RequestedByUserId INT NOT NULL DEFAULT 1,
                        ApprovedDate DATETIME NULL,
                        ApprovedByUserId INT NULL,
                        DispatchedDate DATETIME NULL,
                        DispatchedByUserId INT NULL,
                        ReceivedDate DATETIME NULL,
                        ReceivedByUserId INT NULL,
                        RequestNote NVARCHAR(255) NULL,
                        ApprovalNote NVARCHAR(255) NULL,
                        ReconciliationNote NVARCHAR(500) NULL,
                        CONSTRAINT FK_StockTransfers_FromBranch FOREIGN KEY (FromBranchId) REFERENCES dbo.Branches(BranchId),
                        CONSTRAINT FK_StockTransfers_ToBranch FOREIGN KEY (ToBranchId) REFERENCES dbo.Branches(BranchId)
                    );
                END

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransferDetails')
                BEGIN
                    CREATE TABLE dbo.StockTransferDetails (
                        DetailId INT IDENTITY(1,1) PRIMARY KEY,
                        TransferId INT NOT NULL,
                        IngredientId INT NOT NULL,
                        RequestedQuantity DECIMAL(12,3) NOT NULL,
                        ApprovedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                        DispatchedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                        ReceivedQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                        DiscrepancyQuantity DECIMAL(12,3) NOT NULL DEFAULT 0,
                        DiscrepancyReason NVARCHAR(255) NULL,
                        Uom NVARCHAR(20) NOT NULL DEFAULT 'g',
                        CONSTRAINT FK_STD_Transfers FOREIGN KEY (TransferId) REFERENCES dbo.StockTransfers(TransferId) ON DELETE CASCADE,
                        CONSTRAINT FK_STD_Ingredients FOREIGN KEY (IngredientId) REFERENCES dbo.Ingredients(IngredientId)
                    );
                END
            ");

            _tablesVerified = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StockTransferService] Lỗi khởi tạo bảng điều chuyển: {ex.Message}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<StockTransferDto>> GetTransfersAsync(int? branchId = null, string status = "ALL")
    {
        await EnsureTransferTablesExistAsync();

        var query = _db.StockTransfers
            .AsNoTracking()
            .Include(t => t.FromBranch)
            .Include(t => t.ToBranch)
            .Include(t => t.Details)
                .ThenInclude(d => d.Ingredient)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(t => t.FromBranchId == branchId.Value || t.ToBranchId == branchId.Value);
        }

        if (!string.IsNullOrEmpty(status) && status != "ALL")
        {
            query = query.Where(t => t.Status == status);
        }

        var transfers = await query.OrderByDescending(t => t.RequestedDate).ToListAsync();

        var userIds = transfers.Select(t => t.RequestedByUserId)
            .Concat(transfers.Where(t => t.ApprovedByUserId.HasValue).Select(t => t.ApprovedByUserId!.Value))
            .Concat(transfers.Where(t => t.DispatchedByUserId.HasValue).Select(t => t.DispatchedByUserId!.Value))
            .Concat(transfers.Where(t => t.ReceivedByUserId.HasValue).Select(t => t.ReceivedByUserId!.Value))
            .Distinct()
            .ToList();

        var users = await _db.UserAccounts
            .AsNoTracking()
            .Include(u => u.Employee)
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Employee?.FullName ?? u.Username);

        return transfers.Select(t => MapToDto(t, users)).ToList();
    }

    public async Task<StockTransferDto?> GetTransferByIdAsync(int transferId)
    {
        await EnsureTransferTablesExistAsync();

        var t = await _db.StockTransfers
            .AsNoTracking()
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .Include(x => x.Details)
                .ThenInclude(d => d.Ingredient)
            .FirstOrDefaultAsync(x => x.TransferId == transferId);

        if (t == null) return null;

        var userIds = new List<int> { t.RequestedByUserId };
        if (t.ApprovedByUserId.HasValue) userIds.Add(t.ApprovedByUserId.Value);
        if (t.DispatchedByUserId.HasValue) userIds.Add(t.DispatchedByUserId.Value);
        if (t.ReceivedByUserId.HasValue) userIds.Add(t.ReceivedByUserId.Value);

        var users = await _db.UserAccounts
            .AsNoTracking()
            .Include(u => u.Employee)
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Employee?.FullName ?? u.Username);

        return MapToDto(t, users);
    }

    public async Task<StockTransferDto> CreateTransferRequestAsync(CreateStockTransferRequest req, int userId)
    {
        await EnsureTransferTablesExistAsync();

        if (req.FromBranchId == req.ToBranchId)
        {
            throw new InvalidOperationException("Chi nhánh nguồn và Chi nhánh đích không thể trùng nhau!");
        }

        if (req.Items == null || req.Items.Count == 0)
        {
            throw new InvalidOperationException("Phiếu điều chuyển phải có ít nhất một nguyên liệu!");
        }

        int countToday = await _db.StockTransfers.CountAsync(t => t.RequestedDate.Date == DateTime.UtcNow.Date);
        string code = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{(countToday + 1):D3}";

        var transfer = new StockTransfer
        {
            TransferCode = code,
            FromBranchId = req.FromBranchId,
            ToBranchId = req.ToBranchId,
            Status = "REQUESTED",
            RequestedDate = DateTime.UtcNow,
            RequestedByUserId = userId,
            RequestNote = req.RequestNote?.Trim(),
            Details = req.Items.Select(item => new StockTransferDetail
            {
                IngredientId = item.IngredientId,
                RequestedQuantity = item.Quantity,
                ApprovedQuantity = item.Quantity,
                DispatchedQuantity = 0,
                ReceivedQuantity = 0,
                DiscrepancyQuantity = 0,
                Uom = item.Uom
            }).ToList()
        };

        _db.StockTransfers.Add(transfer);
        await _db.SaveChangesAsync();

        return (await GetTransferByIdAsync(transfer.TransferId))!;
    }

    public async Task<StockTransferDto> ApproveTransferRequestAsync(ApproveStockTransferRequest req, int userId)
    {
        var transfer = await _db.StockTransfers
            .Include(t => t.Details)
            .FirstOrDefaultAsync(t => t.TransferId == req.TransferId);

        if (transfer == null) throw new InvalidOperationException("Không tìm thấy phiếu điều chuyển!");
        if (transfer.Status != "REQUESTED") throw new InvalidOperationException($"Không thể phê duyệt phiếu ở trạng thái: {transfer.Status}");

        if (!req.IsApproved)
        {
            transfer.Status = "REJECTED";
            transfer.ApprovedByUserId = userId;
            transfer.ApprovedDate = DateTime.UtcNow;
            transfer.ApprovalNote = req.ApprovalNote?.Trim() ?? "Chi nhánh nguồn từ chối yêu cầu";
        }
        else
        {
            transfer.Status = "APPROVED";
            transfer.ApprovedByUserId = userId;
            transfer.ApprovedDate = DateTime.UtcNow;
            transfer.ApprovalNote = req.ApprovalNote?.Trim();

            if (req.Items != null && req.Items.Count > 0)
            {
                foreach (var approvedItem in req.Items)
                {
                    var detail = transfer.Details.FirstOrDefault(d => d.DetailId == approvedItem.DetailId);
                    if (detail != null)
                    {
                        detail.ApprovedQuantity = Math.Max(0, approvedItem.ApprovedQuantity);
                    }
                }
            }
        }

        await _db.SaveChangesAsync();
        return (await GetTransferByIdAsync(transfer.TransferId))!;
    }

    public async Task<StockTransferDto> DispatchTransferAsync(DispatchStockTransferRequest req, int userId)
    {
        var transfer = await _db.StockTransfers
            .Include(t => t.FromBranch)
            .Include(t => t.ToBranch)
            .Include(t => t.Details)
                .ThenInclude(d => d.Ingredient)
            .FirstOrDefaultAsync(t => t.TransferId == req.TransferId);

        if (transfer == null) throw new InvalidOperationException("Không tìm thấy phiếu điều chuyển!");
        if (transfer.Status != "APPROVED" && transfer.Status != "REQUESTED")
        {
            throw new InvalidOperationException($"Không thể xuất kho phiếu ở trạng thái: {transfer.Status}");
        }

        // Tự động duyệt nếu đang ở REQUESTED
        if (transfer.Status == "REQUESTED")
        {
            transfer.ApprovedByUserId = userId;
            transfer.ApprovedDate = DateTime.UtcNow;
            transfer.ApprovalNote = "Duyệt & xuất kho trực tiếp";
        }

        // 1. Kiểm tra tồn kho tại chi nhánh xuất
        var ingredientIds = transfer.Details.Select(d => d.IngredientId).ToList();
        var fromStocks = await _db.BranchInventories
            .Where(bi => bi.BranchId == transfer.FromBranchId && ingredientIds.Contains(bi.IngredientId))
            .ToDictionaryAsync(bi => bi.IngredientId);

        foreach (var detail in transfer.Details)
        {
            decimal exportQty = detail.ApprovedQuantity > 0 ? detail.ApprovedQuantity : detail.RequestedQuantity;
            detail.DispatchedQuantity = exportQty;

            if (!fromStocks.TryGetValue(detail.IngredientId, out var stock) || stock.QuantityOnHand < exportQty)
            {
                decimal available = stock?.QuantityOnHand ?? 0;
                throw new InvalidOperationException(
                    $"Chi nhánh xuất không đủ tồn kho cho nguyên liệu '{detail.Ingredient?.IngredientName}'! Hiện còn: {available:N1} {detail.Uom}, cần xuất: {exportQty:N1} {detail.Uom}");
            }
        }

        // 2. Trừ tồn kho tại chi nhánh xuất & Ghi thẻ kho TRANSFER_OUT
        foreach (var detail in transfer.Details)
        {
            var stock = fromStocks[detail.IngredientId];
            stock.QuantityOnHand -= detail.DispatchedQuantity;
            stock.UpdatedAt = DateTime.UtcNow;

            var transaction = new InventoryTransaction
            {
                BranchId = transfer.FromBranchId,
                IngredientId = detail.IngredientId,
                TransactionType = "TRANSFER_OUT",
                QuantityChange = -detail.DispatchedQuantity,
                BalanceAfter = stock.QuantityOnHand,
                ReferenceType = "TRANSFER",
                ReferenceId = transfer.TransferCode,
                Note = $"Xuất điều chuyển tới {transfer.ToBranch?.BranchName}: -{detail.DispatchedQuantity:N1} {detail.Uom}",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId
            };
            _db.InventoryTransactions.Add(transaction);
        }

        transfer.Status = "IN_TRANSIT";
        transfer.DispatchedDate = DateTime.UtcNow;
        transfer.DispatchedByUserId = userId;
        if (!string.IsNullOrWhiteSpace(req.DispatchedNote))
        {
            transfer.ApprovalNote = (transfer.ApprovalNote + " | " + req.DispatchedNote).Trim();
        }

        await _db.SaveChangesAsync();
        return (await GetTransferByIdAsync(transfer.TransferId))!;
    }

    public async Task<StockTransferDto> ReceiveAndReconcileAsync(ReceiveAndReconcileTransferRequest req, int userId)
    {
        var transfer = await _db.StockTransfers
            .Include(t => t.FromBranch)
            .Include(t => t.ToBranch)
            .Include(t => t.Details)
                .ThenInclude(d => d.Ingredient)
            .FirstOrDefaultAsync(t => t.TransferId == req.TransferId);

        if (transfer == null) throw new InvalidOperationException("Không tìm thấy phiếu điều chuyển!");
        if (transfer.Status != "IN_TRANSIT")
        {
            throw new InvalidOperationException($"Chỉ có thể kiểm đếm nhận hàng phiếu đang vận chuyển (IN_TRANSIT)! Trạng thái hiện tại: {transfer.Status}");
        }

        bool hasDiscrepancy = false;

        // 1. Cập nhật số lượng thực nhận và tính toán sai lệch
        var reqItemsMap = req.Items?.ToDictionary(i => i.DetailId) ?? [];

        var ingredientIds = transfer.Details.Select(d => d.IngredientId).ToList();
        var toStocks = await _db.BranchInventories
            .Where(bi => bi.BranchId == transfer.ToBranchId && ingredientIds.Contains(bi.IngredientId))
            .ToDictionaryAsync(bi => bi.IngredientId);

        foreach (var detail in transfer.Details)
        {
            decimal actualReceived;
            string? reason = null;

            if (reqItemsMap.TryGetValue(detail.DetailId, out var recItem))
            {
                actualReceived = Math.Max(0, recItem.ReceivedQuantity);
                reason = recItem.DiscrepancyReason;
            }
            else
            {
                actualReceived = detail.DispatchedQuantity;
            }

            detail.ReceivedQuantity = actualReceived;
            detail.DiscrepancyQuantity = actualReceived - detail.DispatchedQuantity;
            detail.DiscrepancyReason = reason?.Trim();

            if (detail.DiscrepancyQuantity != 0)
            {
                hasDiscrepancy = true;
            }

            // 2. Cộng tồn kho tại chi nhánh nhận theo SỐ LƯỢNG THỰC NHẬN
            if (!toStocks.TryGetValue(detail.IngredientId, out var stock))
            {
                stock = new BranchInventory
                {
                    BranchId = transfer.ToBranchId,
                    IngredientId = detail.IngredientId,
                    QuantityOnHand = 0,
                    ReservedQuantity = 0,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.BranchInventories.Add(stock);
                toStocks[detail.IngredientId] = stock;
            }

            stock.QuantityOnHand += detail.ReceivedQuantity;
            stock.UpdatedAt = DateTime.UtcNow;

            // Thẻ kho TRANSFER_IN
            var transaction = new InventoryTransaction
            {
                BranchId = transfer.ToBranchId,
                IngredientId = detail.IngredientId,
                TransactionType = "TRANSFER_IN",
                QuantityChange = detail.ReceivedQuantity,
                BalanceAfter = stock.QuantityOnHand,
                ReferenceType = "TRANSFER",
                ReferenceId = transfer.TransferCode,
                Note = $"Nhận điều chuyển từ {transfer.FromBranch?.BranchName}: +{detail.ReceivedQuantity:N1} {detail.Uom}",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId
            };
            _db.InventoryTransactions.Add(transaction);

            // Ghi nhận hao hụt vận chuyển nếu có thiếu hụt
            if (detail.DiscrepancyQuantity < 0)
            {
                var lossTransaction = new InventoryTransaction
                {
                    BranchId = transfer.ToBranchId,
                    IngredientId = detail.IngredientId,
                    TransactionType = "TRANSIT_LOSS",
                    QuantityChange = detail.DiscrepancyQuantity,
                    BalanceAfter = stock.QuantityOnHand,
                    ReferenceType = "TRANSFER_LOSS",
                    ReferenceId = transfer.TransferCode,
                    Note = $"[Hao hụt vận chuyển] {detail.DiscrepancyReason ?? "Thiếu hụt khi nhận hàng"}: {detail.DiscrepancyQuantity:N1} {detail.Uom}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = userId
                };
                _db.InventoryTransactions.Add(lossTransaction);
            }
        }

        transfer.Status = hasDiscrepancy ? "COMPLETED_DISCREPANCY" : "COMPLETED";
        transfer.ReceivedDate = DateTime.UtcNow;
        transfer.ReceivedByUserId = userId;
        transfer.ReconciliationNote = req.ReconciliationNote?.Trim();

        await _db.SaveChangesAsync();
        return (await GetTransferByIdAsync(transfer.TransferId))!;
    }

    public async Task<bool> CancelTransferAsync(int transferId, int userId, string reason)
    {
        var transfer = await _db.StockTransfers
            .Include(t => t.FromBranch)
            .Include(t => t.Details)
            .FirstOrDefaultAsync(t => t.TransferId == transferId);

        if (transfer == null) return false;
        if (transfer.Status == "COMPLETED" || transfer.Status == "COMPLETED_DISCREPANCY")
        {
            throw new InvalidOperationException("Không thể hủy phiếu điều chuyển đã hoàn tất!");
        }

        // Nếu đã xuất kho (IN_TRANSIT), hoàn trả tồn kho về cho chi nhánh xuất
        if (transfer.Status == "IN_TRANSIT")
        {
            var ingredientIds = transfer.Details.Select(d => d.IngredientId).ToList();
            var fromStocks = await _db.BranchInventories
                .Where(bi => bi.BranchId == transfer.FromBranchId && ingredientIds.Contains(bi.IngredientId))
                .ToDictionaryAsync(bi => bi.IngredientId);

            foreach (var detail in transfer.Details)
            {
                if (detail.DispatchedQuantity > 0 && fromStocks.TryGetValue(detail.IngredientId, out var stock))
                {
                    stock.QuantityOnHand += detail.DispatchedQuantity;
                    stock.UpdatedAt = DateTime.UtcNow;

                    var transaction = new InventoryTransaction
                    {
                        BranchId = transfer.FromBranchId,
                        IngredientId = detail.IngredientId,
                        TransactionType = "TRANSFER_CANCEL",
                        QuantityChange = detail.DispatchedQuantity,
                        BalanceAfter = stock.QuantityOnHand,
                        ReferenceType = "TRANSFER",
                        ReferenceId = transfer.TransferCode,
                        Note = $"[HỦY ĐIỀU CHUYỂN] Hoàn trả kho: +{detail.DispatchedQuantity:N1} {detail.Uom} (Lý do: {reason})",
                        CreatedAt = DateTime.UtcNow,
                        CreatedByUserId = userId
                    };
                    _db.InventoryTransactions.Add(transaction);
                }
            }
        }

        transfer.Status = "CANCELLED";
        transfer.ReconciliationNote = $"[ĐÃ HỦY] {reason}";
        await _db.SaveChangesAsync();
        return true;
    }

    private static StockTransferDto MapToDto(StockTransfer t, Dictionary<int, string> users)
    {
        string reqUser = users.TryGetValue(t.RequestedByUserId, out var ru) ? ru : $"User #{t.RequestedByUserId}";
        string? appUser = t.ApprovedByUserId.HasValue && users.TryGetValue(t.ApprovedByUserId.Value, out var au) ? au : null;
        string? disUser = t.DispatchedByUserId.HasValue && users.TryGetValue(t.DispatchedByUserId.Value, out var du) ? du : null;
        string? recUser = t.ReceivedByUserId.HasValue && users.TryGetValue(t.ReceivedByUserId.Value, out var rec) ? rec : null;

        return new StockTransferDto
        {
            TransferId = t.TransferId,
            TransferCode = t.TransferCode,
            FromBranchId = t.FromBranchId,
            FromBranchName = t.FromBranch?.BranchName ?? $"Chi nhánh #{t.FromBranchId}",
            ToBranchId = t.ToBranchId,
            ToBranchName = t.ToBranch?.BranchName ?? $"Chi nhánh #{t.ToBranchId}",
            Status = t.Status,
            RequestedDate = t.RequestedDate,
            RequestedByUserId = t.RequestedByUserId,
            RequestedByName = reqUser,
            ApprovedDate = t.ApprovedDate,
            ApprovedByUserId = t.ApprovedByUserId,
            ApprovedByName = appUser,
            DispatchedDate = t.DispatchedDate,
            DispatchedByUserId = t.DispatchedByUserId,
            DispatchedByName = disUser,
            ReceivedDate = t.ReceivedDate,
            ReceivedByUserId = t.ReceivedByUserId,
            ReceivedByName = recUser,
            RequestNote = t.RequestNote,
            ApprovalNote = t.ApprovalNote,
            ReconciliationNote = t.ReconciliationNote,
            Details = t.Details.Select(d => new StockTransferDetailDto
            {
                DetailId = d.DetailId,
                TransferId = d.TransferId,
                IngredientId = d.IngredientId,
                IngredientName = d.Ingredient?.IngredientName ?? "Nguyên liệu",
                IngredientCode = d.Ingredient?.IngredientCode ?? string.Empty,
                RequestedQuantity = d.RequestedQuantity,
                ApprovedQuantity = d.ApprovedQuantity,
                DispatchedQuantity = d.DispatchedQuantity,
                ReceivedQuantity = d.ReceivedQuantity,
                DiscrepancyQuantity = d.DiscrepancyQuantity,
                DiscrepancyReason = d.DiscrepancyReason,
                Uom = d.Uom
            }).ToList()
        };
    }
}
