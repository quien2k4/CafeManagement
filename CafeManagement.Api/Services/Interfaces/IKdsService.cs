using CafeManagement.Core.DTOs.KDS;

namespace CafeManagement.Api.Services.Interfaces;

public interface IKdsService
{
    Task<List<KdsTicketDto>> GetActiveTicketsAsync(int branchId);
    Task<bool> UpdateItemStatusAsync(long orderItemId, string status);
    Task<bool> CompleteOrderAsync(long orderId);
}
