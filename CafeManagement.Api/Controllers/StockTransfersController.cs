using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Inventory;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _transferService;

    public StockTransfersController(IStockTransferService transferService)
    {
        _transferService = transferService;
    }

    private int GetCurrentUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdVal) && int.TryParse(userIdVal, out int uid) && uid > 0)
        {
            return uid;
        }
        return 1;
    }

    [HttpGet]
    public async Task<ActionResult<List<StockTransferDto>>> GetTransfers([FromQuery] int? branchId, [FromQuery] string status = "ALL")
    {
        var list = await _transferService.GetTransfersAsync(branchId, status);
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StockTransferDto>> GetTransferById(int id)
    {
        var item = await _transferService.GetTransferByIdAsync(id);
        if (item == null) return NotFound(new { message = $"Không tìm thấy phiếu điều chuyển #{id}" });
        return Ok(item);
    }

    [HttpPost("request")]
    public async Task<ActionResult<StockTransferDto>> CreateTransferRequest([FromBody] CreateStockTransferRequest req)
    {
        try
        {
            int userId = GetCurrentUserId();
            var result = await _transferService.CreateTransferRequestAsync(req, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("approve")]
    public async Task<ActionResult<StockTransferDto>> ApproveTransfer([FromBody] ApproveStockTransferRequest req)
    {
        try
        {
            int userId = GetCurrentUserId();
            var result = await _transferService.ApproveTransferRequestAsync(req, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("dispatch")]
    public async Task<ActionResult<StockTransferDto>> DispatchTransfer([FromBody] DispatchStockTransferRequest req)
    {
        try
        {
            int userId = GetCurrentUserId();
            var result = await _transferService.DispatchTransferAsync(req, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("receive")]
    public async Task<ActionResult<StockTransferDto>> ReceiveAndReconcile([FromBody] ReceiveAndReconcileTransferRequest req)
    {
        try
        {
            int userId = GetCurrentUserId();
            var result = await _transferService.ReceiveAndReconcileAsync(req, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelTransfer(int id, [FromQuery] string? reason)
    {
        try
        {
            int userId = GetCurrentUserId();
            bool ok = await _transferService.CancelTransferAsync(id, userId, reason ?? "Hủy phiếu điều chuyển");
            if (!ok) return NotFound(new { message = "Không tìm thấy phiếu để hủy" });
            return Ok(new { message = "Đã hủy phiếu điều chuyển thành công" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
