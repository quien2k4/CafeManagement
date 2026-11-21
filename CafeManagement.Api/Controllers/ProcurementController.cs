using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Procurement;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/procurement")]
public class ProcurementController : ControllerBase
{
    private readonly IProcurementService _procurementService;

    public ProcurementController(IProcurementService procurementService)
    {
        _procurementService = procurementService;
    }

    #region 1. Nhà Cung Cấp (Suppliers)
    [HttpGet("suppliers")]
    public async Task<ActionResult<List<SupplierDto>>> GetSuppliers([FromQuery] bool activeOnly = false)
    {
        var list = await _procurementService.GetSuppliersAsync(activeOnly);
        return Ok(list);
    }

    [HttpGet("suppliers/{id:int}")]
    public async Task<ActionResult<SupplierDto>> GetSupplier(int id)
    {
        var supplier = await _procurementService.GetSupplierByIdAsync(id);
        if (supplier == null) return NotFound(new { message = $"Không tìm thấy nhà cung cấp #{id}" });
        return Ok(supplier);
    }

    [HttpPost("suppliers")]
    public async Task<ActionResult<SupplierDto>> CreateSupplier([FromBody] CreateOrUpdateSupplierRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SupplierName))
        {
            return BadRequest(new { message = "Tên nhà cung cấp không được để trống." });
        }
        var created = await _procurementService.CreateSupplierAsync(req);
        return CreatedAtAction(nameof(GetSupplier), new { id = created.SupplierId }, created);
    }

    [HttpPut("suppliers/{id:int}")]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(int id, [FromBody] CreateOrUpdateSupplierRequest req)
    {
        var updated = await _procurementService.UpdateSupplierAsync(id, req);
        if (updated == null) return NotFound(new { message = $"Không tìm thấy nhà cung cấp #{id}" });
        return Ok(updated);
    }

    [HttpDelete("suppliers/{id:int}")]
    public async Task<ActionResult> DeleteSupplier(int id)
    {
        var success = await _procurementService.DeleteSupplierAsync(id);
        if (!success) return NotFound(new { message = $"Không tìm thấy nhà cung cấp #{id}" });
        return Ok(new { message = "Đã xóa nhà cung cấp thành công." });
    }
    #endregion

    #region 2. Bảng Giá & Mặt Hàng Cung Ứng
    [HttpGet("suppliers/{supplierId:int}/ingredients")]
    public async Task<ActionResult<List<SupplierIngredientDto>>> GetSupplierIngredients(int supplierId)
    {
        var list = await _procurementService.GetSupplierIngredientsAsync(supplierId);
        return Ok(list);
    }

    [HttpPost("suppliers/ingredients")]
    public async Task<ActionResult<SupplierIngredientDto>> SetSupplierIngredient([FromBody] SetSupplierIngredientRequest req)
    {
        if (req.SupplierId <= 0 || req.IngredientId <= 0)
        {
            return BadRequest(new { message = "Thông tin nhà cung cấp và nguyên liệu không hợp lệ." });
        }
        var result = await _procurementService.SetSupplierIngredientAsync(req);
        return Ok(result);
    }

    [HttpDelete("suppliers/ingredients/{id:int}")]
    public async Task<ActionResult> DeleteSupplierIngredient(int id)
    {
        var success = await _procurementService.DeleteSupplierIngredientAsync(id);
        if (!success) return NotFound(new { message = $"Không tìm thấy mặt hàng bảng giá #{id}" });
        return Ok(new { message = "Đã xóa mặt hàng khỏi bảng giá của nhà cung cấp." });
    }
    #endregion

    #region 3. Đơn Đặt Hàng Mua (Purchase Orders - PO)
    [HttpGet("purchase-orders")]
    public async Task<ActionResult<List<PurchaseOrderDto>>> GetPurchaseOrders([FromQuery] int? branchId, [FromQuery] string status = "ALL")
    {
        var list = await _procurementService.GetPurchaseOrdersAsync(branchId, status);
        return Ok(list);
    }

    [HttpGet("purchase-orders/{id:int}")]
    public async Task<ActionResult<PurchaseOrderDto>> GetPurchaseOrder(int id)
    {
        var po = await _procurementService.GetPurchaseOrderByIdAsync(id);
        if (po == null) return NotFound(new { message = $"Không tìm thấy đơn đặt hàng #{id}" });
        return Ok(po);
    }

    [HttpPost("purchase-orders")]
    public async Task<ActionResult<PurchaseOrderDto>> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest req)
    {
        try
        {
            var created = await _procurementService.CreatePurchaseOrderAsync(req);
            return CreatedAtAction(nameof(GetPurchaseOrder), new { id = created.PoId }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Lỗi tạo đơn đặt hàng: {ex.Message}" });
        }
    }

    [HttpPut("purchase-orders/{id:int}/status")]
    public async Task<ActionResult> UpdatePoStatus(int id, [FromQuery] string newStatus)
    {
        var success = await _procurementService.UpdatePoStatusAsync(id, newStatus);
        if (!success) return NotFound(new { message = $"Không tìm thấy đơn đặt hàng #{id}" });
        return Ok(new { message = $"Đã cập nhật trạng thái đơn #{id} sang {newStatus}" });
    }
    #endregion

    #region 4. Tiếp Nhận Hàng Hóa & QC
    [HttpGet("goods-receipts")]
    public async Task<ActionResult<List<GoodsReceiptDto>>> GetGoodsReceipts([FromQuery] int? branchId, [FromQuery] int? poId)
    {
        var list = await _procurementService.GetGoodsReceiptsAsync(branchId, poId);
        return Ok(list);
    }

    [HttpGet("goods-receipts/{id:int}")]
    public async Task<ActionResult<GoodsReceiptDto>> GetGoodsReceipt(int id)
    {
        var gr = await _procurementService.GetGoodsReceiptByIdAsync(id);
        if (gr == null) return NotFound(new { message = $"Không tìm thấy phiếu tiếp nhận #{id}" });
        return Ok(gr);
    }

    [HttpPost("goods-receipts")]
    public async Task<ActionResult<GoodsReceiptDto>> ProcessGoodsReceipt([FromBody] CreateGoodsReceiptRequest req)
    {
        try
        {
            var result = await _procurementService.ProcessGoodsReceiptAsync(req);
            return CreatedAtAction(nameof(GetGoodsReceipt), new { id = result.ReceiptId }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Lỗi xử lý tiếp nhận hàng hóa: {ex.Message}" });
        }
    }
    #endregion
}
