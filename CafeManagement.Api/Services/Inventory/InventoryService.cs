using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Inventory;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Inventory;

public class InventoryService : IInventoryService
{
    private readonly CafeDbContext _db;

    public InventoryService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<List<BranchInventoryDto>> GetBranchInventoryAsync(int branchId)
    {
        var stocks = await _db.BranchInventories
            .AsNoTracking()
            .Include(bi => bi.Ingredient)
            .Where(bi => bi.BranchId == branchId)
            .OrderBy(bi => bi.Ingredient!.IngredientName)
            .ToListAsync();

        return stocks.Select(s => new BranchInventoryDto
        {
            BranchInventoryId = s.BranchInventoryId,
            BranchId = s.BranchId,
            IngredientId = s.IngredientId,
            IngredientCode = s.Ingredient?.IngredientCode ?? string.Empty,
            IngredientName = s.Ingredient?.IngredientName ?? "Nguyên liệu",
            BaseUom = s.Ingredient?.BaseUom ?? "g",
            QuantityOnHand = s.QuantityOnHand,
            MinStockAlert = s.Ingredient?.MinStockAlert ?? 0
        }).ToList();
    }

    public async Task<List<IngredientDto>> GetIngredientsAsync(bool activeOnly = true)
    {
        var query = _db.Ingredients.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(i => i.IsActive);
        }

        var list = await query.OrderBy(i => i.IngredientName).ToListAsync();
        return list.Select(i => new IngredientDto
        {
            IngredientId = i.IngredientId,
            IngredientCode = i.IngredientCode,
            IngredientName = i.IngredientName,
            BaseUom = i.BaseUom,
            MinStockAlert = i.MinStockAlert,
            IsActive = i.IsActive
        }).ToList();
    }

    public async Task<IngredientDto> CreateIngredientAsync(IngredientCreateRequest req)
    {
        if (await _db.Ingredients.AnyAsync(i => i.IngredientCode == req.IngredientCode))
        {
            throw new InvalidOperationException($"Mã nguyên liệu '{req.IngredientCode}' đã tồn tại!");
        }

        var newIng = new Ingredient
        {
            IngredientCode = req.IngredientCode.Trim(),
            IngredientName = req.IngredientName.Trim(),
            BaseUom = req.BaseUom.Trim(),
            MinStockAlert = req.MinStockAlert,
            IsActive = true
        };

        _db.Ingredients.Add(newIng);
        await _db.SaveChangesAsync();

        // Tạo BranchInventory cho các chi nhánh trong hệ thống
        var branches = await _db.Branches.ToListAsync();
        int curBranchId = req.CurrentBranchId.GetValueOrDefault(1);

        foreach (var b in branches)
        {
            decimal initQty = (b.BranchId == curBranchId) ? req.InitialStock : 0;
            var branchStock = new BranchInventory
            {
                BranchId = b.BranchId,
                IngredientId = newIng.IngredientId,
                QuantityOnHand = initQty,
                ReservedQuantity = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _db.BranchInventories.Add(branchStock);

            if (initQty > 0)
            {
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    BranchId = b.BranchId,
                    IngredientId = newIng.IngredientId,
                    TransactionType = "INITIAL_STOCK",
                    QuantityChange = initQty,
                    BalanceAfter = initQty,
                    ReferenceType = "INITIAL",
                    ReferenceId = $"INIT_{newIng.IngredientCode}",
                    Note = $"Khởi tạo tồn kho ban đầu khi tạo nguyên liệu mới ({req.CreatorName ?? "Admin"})",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();

        return new IngredientDto
        {
            IngredientId = newIng.IngredientId,
            IngredientCode = newIng.IngredientCode,
            IngredientName = newIng.IngredientName,
            BaseUom = newIng.BaseUom,
            MinStockAlert = newIng.MinStockAlert,
            IsActive = newIng.IsActive
        };
    }

    public async Task<IngredientDto> UpdateIngredientAsync(int ingredientId, IngredientUpdateRequest req)
    {
        var ing = await _db.Ingredients.FindAsync(ingredientId);
        if (ing == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy nguyên liệu ID {ingredientId}");
        }

        ing.IngredientName = req.IngredientName.Trim();
        ing.BaseUom = req.BaseUom.Trim();
        ing.MinStockAlert = req.MinStockAlert;

        await _db.SaveChangesAsync();

        return new IngredientDto
        {
            IngredientId = ing.IngredientId,
            IngredientCode = ing.IngredientCode,
            IngredientName = ing.IngredientName,
            BaseUom = ing.BaseUom,
            MinStockAlert = ing.MinStockAlert,
            IsActive = ing.IsActive
        };
    }

    public async Task<BranchInventoryDto> ImportStockAsync(ImportStockRequest req)
    {
        var stock = await _db.BranchInventories
            .Include(bi => bi.Ingredient)
            .FirstOrDefaultAsync(bi => bi.BranchId == req.BranchId && bi.IngredientId == req.IngredientId);

        if (stock == null)
        {
            stock = new BranchInventory
            {
                BranchId = req.BranchId,
                IngredientId = req.IngredientId,
                QuantityOnHand = req.Quantity,
                ReservedQuantity = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _db.BranchInventories.Add(stock);
        }
        else
        {
            stock.QuantityOnHand += req.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;
        }

        string refId = $"PN{DateTime.Now:yyMMddHHmm}";
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            BranchId = req.BranchId,
            IngredientId = req.IngredientId,
            TransactionType = "GOODS_RECEIPT",
            QuantityChange = req.Quantity,
            BalanceAfter = stock.QuantityOnHand,
            ReferenceType = "GOODS_RECEIPT",
            ReferenceId = refId,
            Note = req.Note,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        // Reload ingredient name if newly added
        if (stock.Ingredient == null)
        {
            await _db.Entry(stock).Reference(s => s.Ingredient).LoadAsync();
        }

        return new BranchInventoryDto
        {
            BranchInventoryId = stock.BranchInventoryId,
            BranchId = stock.BranchId,
            IngredientId = stock.IngredientId,
            IngredientCode = stock.Ingredient?.IngredientCode ?? string.Empty,
            IngredientName = stock.Ingredient?.IngredientName ?? "Nguyên liệu",
            BaseUom = stock.Ingredient?.BaseUom ?? "g",
            QuantityOnHand = stock.QuantityOnHand,
            MinStockAlert = stock.Ingredient?.MinStockAlert ?? 0
        };
    }

    public async Task<BranchInventoryDto> AdjustStockAsync(AdjustStockRequest req)
    {
        var stock = await _db.BranchInventories
            .Include(bi => bi.Ingredient)
            .FirstOrDefaultAsync(bi => bi.BranchId == req.BranchId && bi.IngredientId == req.IngredientId);

        if (stock == null)
        {
            throw new KeyNotFoundException("Không tìm thấy dữ liệu tồn kho để điều chỉnh!");
        }

        decimal oldQty = stock.QuantityOnHand;
        decimal newQty = req.NewQuantity;
        decimal diff = newQty - oldQty;

        stock.QuantityOnHand = newQty;
        stock.UpdatedAt = DateTime.UtcNow;

        string auditRef = $"KK{DateTime.Now:yyMMddHHmm}";
        string noteDetail = string.IsNullOrWhiteSpace(req.Note) ? "" : $" ({req.Note})";

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            BranchId = req.BranchId,
            IngredientId = req.IngredientId,
            TransactionType = "STOCK_ADJUSTMENT",
            QuantityChange = diff,
            BalanceAfter = newQty,
            ReferenceType = "ADJUSTMENT",
            ReferenceId = auditRef,
            Note = $"[Kiểm kê] {req.Reason}{noteDetail} | Cũ: {oldQty:N1}{stock.Ingredient?.BaseUom} -> Mới: {newQty:N1}{stock.Ingredient?.BaseUom} | Bởi: {req.PerformedBy}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return new BranchInventoryDto
        {
            BranchInventoryId = stock.BranchInventoryId,
            BranchId = stock.BranchId,
            IngredientId = stock.IngredientId,
            IngredientCode = stock.Ingredient?.IngredientCode ?? string.Empty,
            IngredientName = stock.Ingredient?.IngredientName ?? "Nguyên liệu",
            BaseUom = stock.Ingredient?.BaseUom ?? "g",
            QuantityOnHand = stock.QuantityOnHand,
            MinStockAlert = stock.Ingredient?.MinStockAlert ?? 0
        };
    }

    public async Task<bool> CancelReceiptAsync(long transactionId)
    {
        var item = await _db.InventoryTransactions.FindAsync(transactionId);
        if (item == null || item.TransactionType != "GOODS_RECEIPT" || item.QuantityChange <= 0)
        {
            return false;
        }

        var stock = await _db.BranchInventories
            .Include(bi => bi.Ingredient)
            .FirstOrDefaultAsync(bi => bi.BranchId == item.BranchId && bi.IngredientId == item.IngredientId);

        if (stock != null)
        {
            stock.QuantityOnHand -= item.QuantityChange;
            if (stock.QuantityOnHand < 0) stock.QuantityOnHand = 0;
            stock.UpdatedAt = DateTime.UtcNow;
        }

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            BranchId = item.BranchId,
            IngredientId = item.IngredientId,
            TransactionType = "RECEIPT_CANCELLED",
            QuantityChange = -item.QuantityChange,
            BalanceAfter = stock?.QuantityOnHand ?? 0,
            ReferenceType = "CANCEL_RECEIPT",
            ReferenceId = $"HUY_{item.ReferenceId}",
            Note = $"[HỦY PHIẾU #{item.ReferenceId}] Thu hồi do nhập sai: -{item.QuantityChange:N1} {stock?.Ingredient?.BaseUom}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<InventoryTransactionDto>> GetTransactionsAsync(int branchId, int limit = 500)
    {
        var trans = await _db.InventoryTransactions
            .AsNoTracking()
            .Include(t => t.Ingredient)
            .Where(t => t.BranchId == branchId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return trans.Select(t => new InventoryTransactionDto
        {
            TransactionId = t.TransactionId,
            BranchId = t.BranchId,
            IngredientId = t.IngredientId,
            IngredientName = t.Ingredient?.IngredientName ?? string.Empty,
            BaseUom = t.Ingredient?.BaseUom ?? "g",
            TransactionType = t.TransactionType,
            QuantityChange = t.QuantityChange,
            BalanceAfter = t.BalanceAfter,
            ReferenceId = t.ReferenceId,
            Note = t.Note,
            CreatedAt = t.CreatedAt
        }).ToList();
    }
}
