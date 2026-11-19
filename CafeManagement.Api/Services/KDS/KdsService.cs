using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.KDS;

namespace CafeManagement.Api.Services.KDS;

public class KdsService : IKdsService
{
    private readonly CafeDbContext _db;

    public KdsService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<List<KdsTicketDto>> GetActiveTicketsAsync(int branchId)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Variant)
                    .ThenInclude(v => v!.Product)
            .Include(o => o.Items)
                .ThenInclude(i => i.Toppings)
                    .ThenInclude(t => t.Topping)
            .Where(o => o.BranchId == branchId && (o.OrderStatus == "PAID" || o.OrderStatus == "PREPARING" || o.OrderStatus == "READY"))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        var tickets = new List<KdsTicketDto>();
        foreach (var ord in orders)
        {
            var ticket = new KdsTicketDto
            {
                OrderId = ord.OrderId,
                OrderNumber = ord.OrderNumber,
                BranchId = ord.BranchId,
                OrderType = ord.OrderType,
                OrderStatus = ord.OrderStatus,
                CreatedAt = ord.CreatedAt,
                Items = []
            };

            foreach (var itm in ord.Items)
            {
                var topSummary = itm.Toppings.Count > 0
                    ? "+ " + string.Join(", ", itm.Toppings.Select(t => t.Quantity > 1 ? $"{t.Topping?.ToppingName} (x{t.Quantity})" : t.Topping?.ToppingName))
                    : "";

                var optParts = new List<string> { $"Size {itm.Variant?.Size ?? "M"}", $"{itm.SugarLevel} đường", $"{itm.IceLevel} đá" };
                if (!string.IsNullOrEmpty(topSummary)) optParts.Add(topSummary);

                ticket.Items.Add(new KdsItemDto
                {
                    OrderItemId = itm.OrderItemId,
                    OrderId = ord.OrderId,
                    ProductName = itm.Variant?.Product?.ProductName ?? "Món nước",
                    Size = itm.Variant?.Size ?? "M",
                    Quantity = itm.Quantity,
                    SugarLevel = itm.SugarLevel,
                    IceLevel = itm.IceLevel,
                    Note = itm.Note ?? "",
                    OptionsSummary = string.Join(" | ", optParts),
                    Status = itm.ItemStatus,
                    Toppings = itm.Toppings.Select(t => t.Topping?.ToppingName ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                });
            }

            tickets.Add(ticket);
        }

        return tickets;
    }

    public async Task<bool> UpdateItemStatusAsync(long orderItemId, string status)
    {
        var item = await _db.OrderItems.FindAsync(orderItemId);
        if (item == null) return false;

        item.ItemStatus = status;

        var order = await _db.Orders.FindAsync(item.OrderId);
        if (order != null)
        {
            if (status == "PREPARING" && order.OrderStatus == "PAID")
            {
                order.OrderStatus = "PREPARING";
            }
            else if (status == "READY")
            {
                var allItems = await _db.OrderItems.Where(i => i.OrderId == item.OrderId).ToListAsync();
                bool allReady = allItems.All(i => i.OrderItemId == orderItemId || i.ItemStatus == "READY" || i.ItemStatus == "SERVED");
                if (allReady)
                {
                    order.OrderStatus = "READY";
                }
            }
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CompleteOrderAsync(long orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return false;

        order.OrderStatus = "COMPLETED";

        var items = await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync();
        foreach (var itm in items)
        {
            itm.ItemStatus = "SERVED";
        }

        await _db.SaveChangesAsync();
        return true;
    }
}
