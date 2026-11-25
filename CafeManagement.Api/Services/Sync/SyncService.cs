using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Orders;
using CafeManagement.Core.DTOs.Sync;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Sync;

public class SyncService : ISyncService
{
    private readonly CafeDbContext _db;
    private readonly IOrderService _orderService;

    public SyncService(CafeDbContext db, IOrderService orderService)
    {
        _db = db;
        _orderService = orderService;
    }

    public Task<HealthCheckResponse> CheckHealthAsync()
    {
        return Task.FromResult(new HealthCheckResponse
        {
            Status = "ONLINE",
            ServerTime = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }

    public async Task<SyncBatchResponse> SyncBatchOrdersAsync(SyncBatchRequest batch)
    {
        var response = new SyncBatchResponse
        {
            TotalSubmitted = batch.Orders.Count
        };

        foreach (var offlineOrder in batch.Orders)
        {
            try
            {
                // Kiểm tra xem đơn này đã từng được đồng bộ lên Server chưa
                var existingOrder = await _db.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => 
                        (!string.IsNullOrEmpty(offlineOrder.LocalId) && o.Note != null && o.Note.Contains(offlineOrder.LocalId)) ||
                        (!string.IsNullOrEmpty(offlineOrder.OrderNumber) && o.OrderNumber == offlineOrder.OrderNumber));

                if (existingOrder != null)
                {
                    // Đã tồn tại đơn trên Server -> Kiểm tra có xung đột về tiền hoặc trạng thái không
                    decimal localFinal = offlineOrder.OrderRequest.FinalAmount;
                    decimal serverFinal = existingOrder.FinalAmount;

                    if (Math.Abs(localFinal - serverFinal) > 0.01m)
                    {
                        // XUNG ĐỘT (Conflict): Local và Server khác tổng tiền
                        response.ConflictCount++;
                        response.Results.Add(new SyncItemResult
                        {
                            LocalId = offlineOrder.LocalId,
                            Status = "CONFLICT",
                            ServerOrderId = existingOrder.OrderId,
                            ServerOrderNumber = existingOrder.OrderNumber,
                            Message = $"Xung đột dữ liệu: Quầy={localFinal:N0}đ, Máy chủ={serverFinal:N0}đ",
                            ConflictDetail = new SyncConflictDetailDto
                            {
                                LocalId = offlineOrder.LocalId,
                                OrderNumber = existingOrder.OrderNumber,
                                ServerOrderId = existingOrder.OrderId,
                                ConflictReason = $"Phát hiện sai lệch tiền giữa quầy ({localFinal:N0} đ) và máy chủ ({serverFinal:N0} đ)",
                                LocalSubTotal = offlineOrder.OrderRequest.SubTotal,
                                LocalDiscount = offlineOrder.OrderRequest.DiscountAmount,
                                LocalFinalAmount = localFinal,
                                LocalPaymentMethod = offlineOrder.OrderRequest.PaymentMethod,
                                LocalItemsSummary = $"{offlineOrder.OrderRequest.Items.Count} món",
                                LocalCreatedAt = offlineOrder.CreatedAt,
                                ServerSubTotal = existingOrder.SubTotal,
                                ServerDiscount = existingOrder.DiscountAmount,
                                ServerFinalAmount = serverFinal,
                                ServerPaymentMethod = "CASH",
                                ServerItemsSummary = $"{existingOrder.Items.Count} món",
                                ServerCreatedAt = existingOrder.CreatedAt,
                                ServerStatus = existingOrder.OrderStatus
                            }
                        });
                        continue;
                    }
                    else
                    {
                        // Đã đồng bộ trước đó và khớp dữ liệu hoàn toàn
                        response.SuccessCount++;
                        response.Results.Add(new SyncItemResult
                        {
                            LocalId = offlineOrder.LocalId,
                            Status = "SUCCESS",
                            ServerOrderId = existingOrder.OrderId,
                            ServerOrderNumber = existingOrder.OrderNumber,
                            Message = "Đơn hàng đã được đồng bộ trước đó."
                        });
                        continue;
                    }
                }

                // Chưa có trên server -> Tạo đơn mới từ dữ liệu Offline
                var req = offlineOrder.OrderRequest;
                if (req.Items == null || req.Items.Count == 0)
                {
                    response.ErrorCount++;
                    response.Results.Add(new SyncItemResult
                    {
                        LocalId = offlineOrder.LocalId,
                        Status = "ERROR",
                        Message = "Đơn hàng offline không có món nào."
                    });
                    continue;
                }

                var createdDto = await _orderService.CreateOrderAsync(req);

                // Cập nhật lại thông tin đồng bộ vào Note và thời gian gốc của đơn hàng
                var createdOrder = await _db.Orders.FindAsync(createdDto.OrderId);
                if (createdOrder != null)
                {
                    createdOrder.Note = $"[Offline Sync: {offlineOrder.LocalId}] " + (createdOrder.Note ?? "");
                    if (offlineOrder.CreatedAt > DateTime.MinValue)
                    {
                        createdOrder.CreatedAt = offlineOrder.CreatedAt;
                    }
                    await _db.SaveChangesAsync();
                }

                response.SuccessCount++;
                response.Results.Add(new SyncItemResult
                {
                    LocalId = offlineOrder.LocalId,
                    Status = "SUCCESS",
                    ServerOrderId = createdDto.OrderId,
                    ServerOrderNumber = createdDto.OrderNumber,
                    Message = "Đồng bộ thành công lên máy chủ."
                });
            }
            catch (Exception ex)
            {
                response.ErrorCount++;
                response.Results.Add(new SyncItemResult
                {
                    LocalId = offlineOrder.LocalId,
                    Status = "ERROR",
                    Message = $"Lỗi đồng bộ: {ex.Message}"
                });
            }
        }

        return response;
    }

    public async Task<SyncItemResult> ResolveConflictAsync(ResolveConflictRequest req)
    {
        if (req.ResolutionChoice == "USE_LOCAL")
        {
            // Người dùng chọn USE_LOCAL (Ưu tiên máy POS: Ghi đè Server bằng dữ liệu quầy)
            Order? order = null;
            if (req.ServerOrderId.HasValue && req.ServerOrderId.Value > 0)
            {
                order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderId == req.ServerOrderId.Value);
            }
            else if (!string.IsNullOrEmpty(req.LocalId))
            {
                order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Note != null && o.Note.Contains(req.LocalId));
            }

            if (order != null && req.LocalOrderData != null)
            {
                order.SubTotal = req.LocalOrderData.SubTotal;
                order.DiscountAmount = req.LocalOrderData.DiscountAmount;
                order.FinalAmount = req.LocalOrderData.FinalAmount;
                order.Note = (order.Note ?? "") + $" [Conflict Resolved: Used Local at {DateTime.UtcNow:HH:mm dd/MM/yyyy}]";
                await _db.SaveChangesAsync();

                return new SyncItemResult
                {
                    LocalId = req.LocalId,
                    Status = "SUCCESS",
                    ServerOrderId = order.OrderId,
                    ServerOrderNumber = order.OrderNumber,
                    Message = "Đã cập nhật máy chủ theo dữ liệu thực tế tại quầy (Use Local)."
                };
            }
        }
        else if (req.ResolutionChoice == "USE_SERVER")
        {
            // Người dùng chọn USE_SERVER: Giữ nguyên máy chủ
            return new SyncItemResult
            {
                LocalId = req.LocalId,
                Status = "SUCCESS",
                ServerOrderId = req.ServerOrderId,
                Message = "Đã xác nhận giữ nguyên số liệu máy chủ (Use Server)."
            };
        }

        return new SyncItemResult
        {
            LocalId = req.LocalId,
            Status = "ERROR",
            Message = "Không tìm thấy đơn hàng cần giải quyết xung đột."
        };
    }
}
