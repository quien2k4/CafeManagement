using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Inventory;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("stocks")]
    public async Task<ActionResult<List<BranchInventoryDto>>> GetBranchInventory([FromQuery] int branchId)
    {
        if (branchId <= 0) return BadRequest("BranchId không hợp lệ!");
        var stocks = await _inventoryService.GetBranchInventoryAsync(branchId);
        return Ok(stocks);
    }

    [HttpGet("ingredients")]
    public async Task<ActionResult<List<IngredientDto>>> GetIngredients([FromQuery] bool activeOnly = true)
    {
        var list = await _inventoryService.GetIngredientsAsync(activeOnly);
        return Ok(list);
    }

    [HttpPost("ingredients")]
    public async Task<ActionResult<IngredientDto>> CreateIngredient([FromBody] IngredientCreateRequest req)
    {
        try
        {
            var result = await _inventoryService.CreateIngredientAsync(req);
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

    [HttpPut("ingredients/{id}")]
    public async Task<ActionResult<IngredientDto>> UpdateIngredient(int id, [FromBody] IngredientUpdateRequest req)
    {
        try
        {
            var result = await _inventoryService.UpdateIngredientAsync(id, req);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("import")]
    public async Task<ActionResult<BranchInventoryDto>> ImportStock([FromBody] ImportStockRequest req)
    {
        try
        {
            var result = await _inventoryService.ImportStockAsync(req);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("adjust")]
    public async Task<ActionResult<BranchInventoryDto>> AdjustStock([FromBody] AdjustStockRequest req)
    {
        try
        {
            var result = await _inventoryService.AdjustStockAsync(req);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("transactions/{id}/cancel")]
    public async Task<ActionResult<bool>> CancelReceipt(long id)
    {
        var result = await _inventoryService.CancelReceiptAsync(id);
        if (!result) return BadRequest("Không thể hủy phiếu nhập kho này!");
        return Ok(result);
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<List<InventoryTransactionDto>>> GetTransactions([FromQuery] int branchId, [FromQuery] int limit = 500)
    {
        if (branchId <= 0) return BadRequest("BranchId không hợp lệ!");
        var list = await _inventoryService.GetTransactionsAsync(branchId, limit);
        return Ok(list);
    }
}
