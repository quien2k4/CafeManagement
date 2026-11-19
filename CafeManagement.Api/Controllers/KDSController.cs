using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.KDS;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KDSController : ControllerBase
{
    private readonly IKdsService _kdsService;

    public KDSController(IKdsService kdsService)
    {
        _kdsService = kdsService;
    }

    [HttpGet("tickets")]
    public async Task<ActionResult<List<KdsTicketDto>>> GetTickets([FromQuery] int branchId)
    {
        if (branchId <= 0) return BadRequest("BranchId không hợp lệ!");
        var tickets = await _kdsService.GetActiveTicketsAsync(branchId);
        return Ok(tickets);
    }

    [HttpPost("items/{orderItemId}/status")]
    public async Task<ActionResult<bool>> UpdateItemStatus(long orderItemId, [FromBody] UpdateKdsItemStatusRequest req)
    {
        var result = await _kdsService.UpdateItemStatusAsync(orderItemId, req.Status);
        if (!result) return NotFound("Không tìm thấy món cần cập nhật!");
        return Ok(result);
    }

    [HttpPost("orders/{orderId}/complete")]
    public async Task<ActionResult<bool>> CompleteOrder(long orderId)
    {
        var result = await _kdsService.CompleteOrderAsync(orderId);
        if (!result) return NotFound("Không tìm thấy đơn hàng cần bàn giao!");
        return Ok(result);
    }
}
