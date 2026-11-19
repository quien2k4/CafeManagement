using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Customers;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Orders;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Orders;

public class OrderService : IOrderService
{
    private readonly CafeDbContext _db;

    public OrderService(CafeDbContext db)
    {
        _db = db;
    }

    private static string GenerateOrderNumber() => $"HD{DateTime.Now:yyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest req)
    {
        if (req.Items.Count == 0)
        {
            throw new InvalidOperationException("Đơn hàng không có món nào để thanh toán!");
        }

        string orderNumber = GenerateOrderNumber();
        int branchId = req.BranchId > 0 ? req.BranchId : 1;
        int cashierId = req.CashierUserId > 0 ? req.CashierUserId : 1;

        var order = new Order
        {
            OrderNumber = orderNumber,
            BranchId = branchId,
            CashierUserId = cashierId,
            CustomerId = req.CustomerId,
            PromotionCode = req.PromotionCode,
            PointsUsed = req.CustomerPointsUsed,
            OrderType = req.OrderType ?? "TAKE_AWAY",
            OrderStatus = "PAID",
            SubTotal = req.SubTotal,
            DiscountAmount = req.DiscountAmount,
            TaxAmount = 0,
            FinalAmount = req.FinalAmount,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var cart in req.Items)
        {
            var orderItem = new OrderItem
            {
                VariantId = cart.VariantId > 0 ? cart.VariantId : 1,
                Quantity = cart.Quantity,
                UnitPrice = cart.UnitPrice,
                SubTotal = cart.UnitPrice * cart.Quantity,
                SugarLevel = cart.Sugar,
                IceLevel = cart.Ice,
                Note = cart.Note,
                ItemStatus = "QUEUED"
            };

            foreach (var top in cart.Toppings)
            {
                orderItem.Toppings.Add(new OrderItemTopping
                {
                    ToppingId = top.ToppingId,
                    Quantity = top.Quantity,
                    UnitPrice = top.UnitPrice
                });
            }

            order.Items.Add(orderItem);
        }

        // 1. Gán ca làm việc và cập nhật doanh thu ca (nếu có ca làm việc)
        long? shiftId = req.ShiftId;
        if (!shiftId.HasValue || shiftId.Value <= 0)
        {
            var openShift = await _db.WorkShifts
                .Where(s => s.BranchId == branchId && s.Status == "OPEN")
                .OrderByDescending(s => s.ShiftId)
                .FirstOrDefaultAsync();
            if (openShift != null) shiftId = openShift.ShiftId;
        }

        if (shiftId.HasValue && shiftId.Value > 0)
        {
            order.ShiftId = shiftId.Value;
            var shift = await _db.WorkShifts.FindAsync(shiftId.Value);
            if (shift != null)
            {
                if (req.PaymentMethod == "CASH")
                {
                    shift.CashSales += req.FinalAmount;
                }
                else
                {
                    shift.VietQrSales += req.FinalAmount;
                }
                shift.TotalSales += req.FinalAmount;
                shift.TotalOrders += 1;
                shift.ExpectedClosingCash = shift.OpeningCash + shift.CashSales;
            }
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // 2. Lưu giao dịch thanh toán
        var payment = new Payment
        {
            OrderId = order.OrderId,
            PaymentMethod = req.PaymentMethod,
            Amount = req.FinalAmount,
            Status = "SUCCESS",
            TransactionCode = req.PaymentMethod == "VIETQR" ? $"QR_{order.OrderNumber}" : null,
            CashierUserId = cashierId,
            PaidAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);

        // 3. Lưu biên nhận thanh toán (Order Receipt)
        decimal changeAmount = req.CustomerGivenAmount >= req.FinalAmount ? req.CustomerGivenAmount - req.FinalAmount : 0;
        var receipt = new OrderReceipt
        {
            ReceiptNumber = $"REC_{order.OrderNumber}",
            OrderId = order.OrderId,
            BranchId = branchId,
            CashierUserId = cashierId,
            TotalAmount = req.FinalAmount,
            AmountPaid = req.CustomerGivenAmount,
            ChangeAmount = changeAmount,
            IssuedAt = DateTime.UtcNow
        };
        _db.OrderReceipts.Add(receipt);

        // 4. Cập nhật điểm & nâng hạng khách hàng thành viên
        if (req.CustomerId.HasValue && req.CustomerId.Value > 0)
        {
            var dbCust = await _db.Customers.FindAsync(req.CustomerId.Value);
            if (dbCust != null)
            {
                if (req.CustomerPointsUsed > 0)
                {
                    dbCust.TotalPoints = Math.Max(0, dbCust.TotalPoints - req.CustomerPointsUsed);
                }

                int earned = (int)(req.FinalAmount / 10000m);
                dbCust.TotalPoints += earned;
                dbCust.LifetimePoints += earned;
                dbCust.TotalSpent += req.FinalAmount;
                order.PointsEarned = earned;

                CustomerService.UpdateTier(dbCust);
            }
        }

        // 5. Cập nhật lượt sử dụng Voucher
        if (!string.IsNullOrWhiteSpace(req.PromotionCode))
        {
            var dbPromo = await _db.Promotions.FirstOrDefaultAsync(p => p.PromotionCode == req.PromotionCode);
            if (dbPromo != null)
            {
                dbPromo.UsedCount++;
            }
        }

        // 6. Tự động trừ tồn kho nguyên liệu theo định mức công thức
        await DeductInventoryForOrderAsync(order, branchId);

        await _db.SaveChangesAsync();

        return new OrderDto
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            BranchId = order.BranchId,
            CashierUserId = order.CashierUserId,
            CustomerId = order.CustomerId,
            PromotionCode = order.PromotionCode,
            OrderType = order.OrderType,
            Status = order.OrderStatus,
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            FinalAmount = order.FinalAmount,
            PaymentMethod = req.PaymentMethod,
            CreatedAt = order.CreatedAt,
            PointsEarned = order.PointsEarned,
            PointsUsed = order.PointsUsed
        };
    }

    public async Task<List<OrderDto>> GetOrdersAsync(int? branchId = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Branch)
            .Include(o => o.Customer)
            .Include(o => o.Payments)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(o => o.BranchId == branchId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= to.Value);
        }

        var list = await query
            .OrderByDescending(o => o.OrderId)
            .ToListAsync();

        return list.Select(o => new OrderDto
        {
            OrderId = o.OrderId,
            OrderNumber = o.OrderNumber,
            BranchId = o.BranchId,
            BranchName = o.Branch?.BranchName ?? $"Chi nhánh #{o.BranchId}",
            CashierUserId = o.CashierUserId,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer?.FullName,
            PromotionCode = o.PromotionCode,
            OrderType = o.OrderType,
            Status = o.OrderStatus,
            SubTotal = o.SubTotal,
            DiscountAmount = o.DiscountAmount,
            FinalAmount = o.FinalAmount,
            PaymentMethod = o.Payments.FirstOrDefault()?.PaymentMethod ?? "CASH",
            CreatedAt = o.CreatedAt,
            PointsEarned = o.PointsEarned,
            PointsUsed = o.PointsUsed
        }).ToList();
    }

    public async Task<OrderDto?> GetOrderByIdAsync(long id)
    {
        var o = await _db.Orders
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.Customer)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.OrderId == id);

        if (o == null) return null;

        return new OrderDto
        {
            OrderId = o.OrderId,
            OrderNumber = o.OrderNumber,
            BranchId = o.BranchId,
            BranchName = o.Branch?.BranchName ?? $"Chi nhánh #{o.BranchId}",
            CashierUserId = o.CashierUserId,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer?.FullName,
            PromotionCode = o.PromotionCode,
            OrderType = o.OrderType,
            Status = o.OrderStatus,
            SubTotal = o.SubTotal,
            DiscountAmount = o.DiscountAmount,
            FinalAmount = o.FinalAmount,
            PaymentMethod = o.Payments.FirstOrDefault()?.PaymentMethod ?? "CASH",
            CreatedAt = o.CreatedAt,
            PointsEarned = o.PointsEarned,
            PointsUsed = o.PointsUsed
        };
    }

    private async Task DeductInventoryForOrderAsync(Order order, int branchId)
    {
        try
        {
            foreach (var item in order.Items)
            {
                var variant = await _db.ProductVariants
                    .Include(v => v.Product)
                    .FirstOrDefaultAsync(v => v.VariantId == item.VariantId);
                if (variant == null) continue;

                var deductions = new List<(string IngCode, decimal Amount)>();

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
                            deductions.Add((detail.Ingredient.IngredientCode, detail.Quantity));
                        }
                    }
                }
                else
                {
                    // Fallback theo định lượng chuẩn
                    string pCode = variant.Product?.ProductCode ?? "";
                    bool isSizeL = variant.Size == "L";

                    if (pCode.Contains("CF_SUA_DA"))
                    {
                        deductions.Add(("ING_ROBUSTA", isSizeL ? 35 : 25));
                        deductions.Add(("ING_SUADAC", isSizeL ? 50 : 40));
                        deductions.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("CF_DEN_DA"))
                    {
                        deductions.Add(("ING_ROBUSTA", isSizeL ? 35 : 25));
                        deductions.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("BAC_XIU"))
                    {
                        deductions.Add(("ING_ROBUSTA", isSizeL ? 20 : 15));
                        deductions.Add(("ING_SUADAC", isSizeL ? 60 : 50));
                        deductions.Add(("ING_SUATUOI", isSizeL ? 80 : 60));
                        deductions.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("TRA_DAO"))
                    {
                        deductions.Add(("ING_TRA_DEN", isSizeL ? 150 : 120));
                        deductions.Add(("ING_DAO_MIENG", isSizeL ? 60 : 40));
                        deductions.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("TRA_SUA"))
                    {
                        deductions.Add(("ING_TRA_OOLONG", isSizeL ? 150 : 120));
                        deductions.Add(("ING_SUADAC", isSizeL ? 40 : 30));
                        deductions.Add(("ING_SUATUOI", isSizeL ? 100 : 80));
                        deductions.Add(("ING_TRANCHAU", isSizeL ? 50 : 40));
                        deductions.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                }

                // Thực hiện trừ kho cho từng nguyên liệu trong công thức
                foreach (var (ingCode, singleAmount) in deductions)
                {
                    var ing = await _db.Ingredients.FirstOrDefaultAsync(i => i.IngredientCode == ingCode);
                    if (ing == null) continue;

                    decimal totalAmount = singleAmount * item.Quantity;

                    var stock = await _db.BranchInventories
                        .FirstOrDefaultAsync(bi => bi.BranchId == branchId && bi.IngredientId == ing.IngredientId);

                    if (stock != null)
                    {
                        stock.QuantityOnHand -= totalAmount;
                        stock.UpdatedAt = DateTime.UtcNow;

                        _db.InventoryTransactions.Add(new InventoryTransaction
                        {
                            BranchId = branchId,
                            IngredientId = ing.IngredientId,
                            TransactionType = "SALE_CONSUMPTION",
                            QuantityChange = -totalAmount,
                            BalanceAfter = stock.QuantityOnHand,
                            ReferenceId = order.OrderNumber,
                            Note = $"Xuất kho bán hàng đơn #{order.OrderNumber} ({item.Quantity}x {variant.Product?.ProductName ?? "Món"} - Size {variant.Size})",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi trừ kho bán hàng: {ex.Message}");
        }
    }
}
