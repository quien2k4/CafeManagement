using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Customers;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Payments;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Orders;

public class RefundService : IRefundService
{
    private readonly CafeDbContext _db;

    public RefundService(CafeDbContext db)
    {
        _db = db;
    }

    private async Task EnsureTableExistsAsync()
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderRefunds')
                BEGIN
                    CREATE TABLE dbo.OrderRefunds (
                        RefundId BIGINT IDENTITY(1,1) PRIMARY KEY,
                        OrderId BIGINT NOT NULL,
                        PaymentId BIGINT NULL,
                        RefundAmount DECIMAL(18,2) NOT NULL,
                        RefundMethod VARCHAR(20) NOT NULL DEFAULT 'CASH',
                        RefundReason NVARCHAR(255) NOT NULL,
                        Status VARCHAR(20) NOT NULL DEFAULT 'COMPLETED',
                        ProcessedByUserId INT NOT NULL DEFAULT 1,
                        ProcessedByName NVARCHAR(100) NOT NULL DEFAULT '',
                        RefundedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                        CONSTRAINT FK_OrderRefunds_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId) ON DELETE CASCADE
                    );
                END
            ");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EnsureTableExists OrderRefunds error: {ex.Message}");
        }
    }

    public async Task<RefundDto> CreateRefundAsync(RefundCreateRequest req)
    {
        await EnsureTableExistsAsync();

        if (req.RefundAmount <= 0)
        {
            throw new InvalidOperationException("Số tiền hoàn trả phải lớn hơn 0!");
        }

        if (string.IsNullOrWhiteSpace(req.RefundReason))
        {
            throw new InvalidOperationException("Vui lòng cung cấp lý do hoàn tiền!");
        }

        var order = await _db.Orders
            .Include(o => o.Payments)
            .Include(o => o.Refunds)
            .Include(o => o.Items)
                .ThenInclude(i => i.Variant)
                    .ThenInclude(v => v!.Product)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
            .FirstOrDefaultAsync(o => o.OrderId == req.OrderId);

        if (order == null)
        {
            throw new InvalidOperationException("Không tìm thấy đơn hàng cần hoàn tiền!");
        }

        if (order.OrderStatus == "CANCELLED")
        {
            throw new InvalidOperationException("Đơn hàng này đã bị hủy, không thể thực hiện hoàn tiền!");
        }

        if (order.OrderStatus == "REFUNDED")
        {
            throw new InvalidOperationException("Đơn hàng này đã được hoàn trả toàn bộ số tiền trước đó!");
        }

        decimal alreadyRefunded = order.Refunds
            .Where(r => r.Status == "COMPLETED")
            .Sum(r => r.RefundAmount);

        decimal maxRefundable = order.FinalAmount - alreadyRefunded;
        if (req.RefundAmount > maxRefundable)
        {
            throw new InvalidOperationException($"Số tiền hoàn ({req.RefundAmount:N0} đ) vượt quá số tiền tối đa có thể hoàn ({maxRefundable:N0} đ)!");
        }

        var primaryPayment = order.Payments.FirstOrDefault();
        string refundMethod = !string.IsNullOrWhiteSpace(req.RefundMethod) && req.RefundMethod != "ORIGINAL"
            ? req.RefundMethod
            : (primaryPayment?.PaymentMethod ?? "CASH");

        var refund = new OrderRefund
        {
            OrderId = order.OrderId,
            PaymentId = primaryPayment?.PaymentId,
            RefundAmount = req.RefundAmount,
            RefundMethod = refundMethod,
            RefundReason = req.RefundReason.Trim(),
            Status = "COMPLETED",
            ProcessedByUserId = req.ProcessedByUserId > 0 ? req.ProcessedByUserId : 1,
            ProcessedByName = !string.IsNullOrWhiteSpace(req.ProcessedByName) ? req.ProcessedByName : "Thu ngân / Quản lý",
            RefundedAt = DateTime.UtcNow
        };

        _db.OrderRefunds.Add(refund);

        // 1. Cập nhật trạng thái Order
        decimal totalRefundedAfterThis = alreadyRefunded + req.RefundAmount;
        if (totalRefundedAfterThis >= order.FinalAmount)
        {
            order.OrderStatus = "REFUNDED";
        }
        else
        {
            order.OrderStatus = "PARTIALLY_REFUNDED";
        }

        // 2. Cập nhật doanh thu ca làm việc (WorkShift)
        long? shiftId = order.ShiftId;
        if (!shiftId.HasValue || shiftId.Value <= 0)
        {
            var openShift = await _db.WorkShifts
                .Where(s => s.BranchId == order.BranchId && s.Status == "OPEN")
                .OrderByDescending(s => s.ShiftId)
                .FirstOrDefaultAsync();
            if (openShift != null) shiftId = openShift.ShiftId;
        }

        if (shiftId.HasValue && shiftId.Value > 0)
        {
            var shift = await _db.WorkShifts.FindAsync(shiftId.Value);
            if (shift != null)
            {
                if (refundMethod == "CASH")
                {
                    shift.CashSales = Math.Max(0, shift.CashSales - req.RefundAmount);
                    shift.ExpectedClosingCash = Math.Max(0, shift.ExpectedClosingCash - req.RefundAmount);
                }
                else
                {
                    shift.VietQrSales = Math.Max(0, shift.VietQrSales - req.RefundAmount);
                }
                shift.TotalSales = Math.Max(0, shift.TotalSales - req.RefundAmount);
            }
        }

        // 3. Khấu trừ điểm tích lũy thành viên đã cộng từ khoản tiền hoàn này
        if (order.CustomerId.HasValue && order.CustomerId.Value > 0)
        {
            var cust = await _db.Customers.FindAsync(order.CustomerId.Value);
            if (cust != null)
            {
                int pointsToRevert = (int)(req.RefundAmount / 10000m);
                if (pointsToRevert > 0)
                {
                    cust.TotalPoints = Math.Max(0, cust.TotalPoints - pointsToRevert);
                    cust.LifetimePoints = Math.Max(0, cust.LifetimePoints - pointsToRevert);
                    cust.TotalSpent = Math.Max(0, cust.TotalSpent - req.RefundAmount);
                    CustomerService.UpdateTier(cust);
                }
            }
        }

        // 4. Tùy chọn: Hoàn nhập nguyên liệu vào kho nếu chưa chế biến hoặc khách trả món nguyên vẹn
        if (req.ReturnIngredientsToInventory)
        {
            await RestockInventoryForOrderAsync(order, req.Items, order.BranchId);
        }

        await _db.SaveChangesAsync();

        return new RefundDto
        {
            RefundId = refund.RefundId,
            OrderId = refund.OrderId,
            OrderNumber = order.OrderNumber,
            PaymentId = refund.PaymentId,
            RefundAmount = refund.RefundAmount,
            RefundMethod = refund.RefundMethod,
            RefundReason = refund.RefundReason,
            Status = refund.Status,
            ProcessedByUserId = refund.ProcessedByUserId,
            ProcessedByName = refund.ProcessedByName,
            RefundedAt = refund.RefundedAt
        };
    }

    public async Task<List<RefundDto>> GetRefundsByOrderIdAsync(long orderId)
    {
        await EnsureTableExistsAsync();

        var list = await _db.OrderRefunds
            .AsNoTracking()
            .Include(r => r.Order)
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.RefundedAt)
            .ToListAsync();

        return list.Select(r => new RefundDto
        {
            RefundId = r.RefundId,
            OrderId = r.OrderId,
            OrderNumber = r.Order?.OrderNumber ?? $"#{r.OrderId}",
            PaymentId = r.PaymentId,
            RefundAmount = r.RefundAmount,
            RefundMethod = r.RefundMethod,
            RefundReason = r.RefundReason,
            Status = r.Status,
            ProcessedByUserId = r.ProcessedByUserId,
            ProcessedByName = r.ProcessedByName,
            RefundedAt = r.RefundedAt
        }).ToList();
    }

    public async Task<List<RefundDto>> GetRefundsAsync(int? branchId = null, DateTime? from = null, DateTime? to = null)
    {
        await EnsureTableExistsAsync();

        var query = _db.OrderRefunds
            .AsNoTracking()
            .Include(r => r.Order)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(r => r.Order != null && r.Order.BranchId == branchId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(r => r.RefundedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(r => r.RefundedAt <= to.Value);
        }

        var list = await query.OrderByDescending(r => r.RefundedAt).ToListAsync();

        return list.Select(r => new RefundDto
        {
            RefundId = r.RefundId,
            OrderId = r.OrderId,
            OrderNumber = r.Order?.OrderNumber ?? $"#{r.OrderId}",
            PaymentId = r.PaymentId,
            RefundAmount = r.RefundAmount,
            RefundMethod = r.RefundMethod,
            RefundReason = r.RefundReason,
            Status = r.Status,
            ProcessedByUserId = r.ProcessedByUserId,
            ProcessedByName = r.ProcessedByName,
            RefundedAt = r.RefundedAt
        }).ToList();
    }

    private async Task RestockInventoryForOrderAsync(Order order, List<RefundItemRequest> reqItems, int branchId)
    {
        try
        {
            // Xác định danh sách món cần hoàn kho:
            // Nếu người dùng tick chọn món cụ thể thì chỉ hoàn đúng các món đó.
            // Nếu danh sách món gửi lên trống, fallback hoàn tất cả các món trong đơn.
            var itemsToProcess = new List<(int VariantId, int Quantity, string ProductName)>();

            if (reqItems != null && reqItems.Count > 0)
            {
                foreach (var ri in reqItems)
                {
                    var origItem = order.Items.FirstOrDefault(oi => oi.OrderItemId == ri.OrderItemId || oi.VariantId == ri.VariantId);
                    int variantId = ri.VariantId > 0 ? ri.VariantId : (origItem?.VariantId ?? 0);
                    if (variantId <= 0) continue;

                    string pName = !string.IsNullOrWhiteSpace(ri.ProductName) 
                        ? ri.ProductName 
                        : (origItem?.Variant?.Product?.ProductName ?? "Món");
                    int qty = ri.Quantity > 0 ? ri.Quantity : 1;

                    itemsToProcess.Add((variantId, qty, pName));
                }
            }
            else
            {
                foreach (var oi in order.Items)
                {
                    itemsToProcess.Add((oi.VariantId, oi.Quantity, oi.Variant?.Product?.ProductName ?? "Món"));
                }
            }

            foreach (var item in itemsToProcess)
            {
                var variant = await _db.ProductVariants
                    .Include(v => v.Product)
                    .FirstOrDefaultAsync(v => v.VariantId == item.VariantId);
                if (variant == null) continue;

                var restockItems = new List<(string IngCode, decimal SingleAmount)>();

                // 1. Kiểm tra công thức BOM đã cấu hình trong CSDL (Recipes -> RecipeVersions -> RecipeDetails)
                var recipeVersion = await _db.RecipeVersions
                    .Include(rv => rv.Details)
                        .ThenInclude(d => d.Ingredient)
                    .Where(rv => rv.Recipe!.VariantId == variant.VariantId && rv.IsCurrent)
                    .FirstOrDefaultAsync();

                if (recipeVersion != null && recipeVersion.Details.Count > 0)
                {
                    foreach (var detail in recipeVersion.Details)
                    {
                        if (detail.Ingredient != null)
                        {
                            restockItems.Add((detail.Ingredient.IngredientCode, detail.Quantity));
                        }
                    }
                }
                else
                {
                    // Fallback theo định lượng chuẩn khớp chính xác 100% với OrderService.cs
                    string pCode = variant.Product?.ProductCode ?? "";
                    bool isSizeL = variant.Size == "L";

                    if (pCode.Contains("CF_SUA_DA"))
                    {
                        restockItems.Add(("ING_ROBUSTA", isSizeL ? 35 : 25));
                        restockItems.Add(("ING_SUADAC", isSizeL ? 50 : 40));
                        restockItems.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("CF_DEN_DA"))
                    {
                        restockItems.Add(("ING_ROBUSTA", isSizeL ? 35 : 25));
                        restockItems.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("BAC_XIU"))
                    {
                        restockItems.Add(("ING_ROBUSTA", isSizeL ? 20 : 15));
                        restockItems.Add(("ING_SUADAC", isSizeL ? 60 : 50));
                        restockItems.Add(("ING_SUATUOI", isSizeL ? 80 : 60));
                        restockItems.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("TRA_DAO"))
                    {
                        restockItems.Add(("ING_TRA_DEN", isSizeL ? 150 : 120));
                        restockItems.Add(("ING_DAO_MIENG", isSizeL ? 60 : 40));
                        restockItems.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("TRA_SUA"))
                    {
                        restockItems.Add(("ING_TRA_OOLONG", isSizeL ? 150 : 120));
                        restockItems.Add(("ING_SUADAC", isSizeL ? 40 : 30));
                        restockItems.Add(("ING_SUATUOI", isSizeL ? 100 : 80));
                        restockItems.Add(("ING_TRANCHAU", isSizeL ? 50 : 40));
                        restockItems.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                }

                // Thực hiện cộng lại kho chính xác 100% định lượng: QuantityChange = SingleAmount * item.Quantity
                foreach (var (ingCode, singleAmount) in restockItems)
                {
                    var ing = await _db.Ingredients.FirstOrDefaultAsync(i => i.IngredientCode == ingCode);
                    if (ing == null) continue;

                    decimal restockQty = singleAmount * item.Quantity;
                    if (restockQty <= 0) continue;

                    var stock = await _db.BranchInventories
                        .FirstOrDefaultAsync(bi => bi.BranchId == branchId && bi.IngredientId == ing.IngredientId);

                    if (stock != null)
                    {
                        stock.QuantityOnHand += restockQty;
                        stock.UpdatedAt = DateTime.UtcNow;

                        _db.InventoryTransactions.Add(new InventoryTransaction
                        {
                            BranchId = branchId,
                            IngredientId = ing.IngredientId,
                            TransactionType = "REFUND_RESTOCK",
                            QuantityChange = restockQty,
                            BalanceAfter = stock.QuantityOnHand,
                            ReferenceId = order.OrderNumber,
                            Note = $"Hoàn nhập kho do hoàn tiền món {item.ProductName} ({item.Quantity}x) đơn #{order.OrderNumber} ({ing.IngredientName})",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi hoàn nhập kho: {ex.Message}");
        }
    }
}
