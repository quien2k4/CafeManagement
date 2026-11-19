using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Promotions;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public PromotionsController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PromotionDto>>> GetPromotions([FromQuery] bool activeOnly = false)
    {
        var list = await _promotionService.GetPromotionsAsync(activeOnly);
        return Ok(list);
    }

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<PromotionDto>> GetPromotionByCode(string code)
    {
        var promo = await _promotionService.GetPromotionByCodeAsync(code);
        if (promo == null) return NotFound(new { message = $"Mã khuyến mãi '{code}' không tồn tại hoặc đã hết hạn!" });
        return Ok(promo);
    }

    [HttpPost]
    public async Task<ActionResult<PromotionDto>> CreatePromotion([FromBody] PromotionCreateUpdateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PromotionCode) || string.IsNullOrWhiteSpace(req.PromotionName))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ mã và tên khuyến mãi!" });
        }

        try
        {
            var created = await _promotionService.CreatePromotionAsync(req);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi tạo khuyến mãi: {ex.Message}" });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PromotionDto>> UpdatePromotion(int id, [FromBody] PromotionCreateUpdateRequest req)
    {
        try
        {
            var updated = await _promotionService.UpdatePromotionAsync(id, req);
            if (updated == null) return NotFound(new { message = "Không tìm thấy chương trình khuyến mãi!" });
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi cập nhật: {ex.Message}" });
        }
    }

    [HttpPatch("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var ok = await _promotionService.TogglePromotionActiveAsync(id);
        if (!ok) return NotFound(new { message = "Không tìm thấy chương trình khuyến mãi!" });
        return Ok(new { message = "Đã thay đổi trạng thái thành công!" });
    }
}
