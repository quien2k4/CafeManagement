using CafeManagement.Core.DTOs.Payments;

namespace CafeManagement.Api.Services.Interfaces;

public interface IRefundService
{
    Task<RefundDto> CreateRefundAsync(RefundCreateRequest req);
    Task<List<RefundDto>> GetRefundsByOrderIdAsync(long orderId);
    Task<List<RefundDto>> GetRefundsAsync(int? branchId = null, DateTime? from = null, DateTime? to = null);
}
