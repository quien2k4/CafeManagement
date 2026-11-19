using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Payments;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RefundsController : ControllerBase
{
    private readonly IRefundService _refundService;

    public RefundsController(IRefundService refundService)
    {
        _refundService = refundService;
    }

    [HttpPost]
    public async Task<ActionResult<RefundDto>> CreateRefund([FromBody] RefundCreateRequest req)
    {
        try
        {
            var res = await _refundService.CreateRefundAsync(req);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi xử lý hoàn tiền: {ex.Message}" });
        }
    }

    [HttpGet("orders/{orderId:long}")]
    public async Task<ActionResult<List<RefundDto>>> GetRefundsByOrderId(long orderId)
    {
        var list = await _refundService.GetRefundsByOrderIdAsync(orderId);
        return Ok(list);
    }

    [HttpGet]
    public async Task<ActionResult<List<RefundDto>>> GetRefunds(
        [FromQuery] int? branchId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var list = await _refundService.GetRefundsAsync(branchId, from, to);
        return Ok(list);
    }
}
