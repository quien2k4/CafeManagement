using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Branches;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BranchesController : ControllerBase
{
    private readonly IBranchesService _branchesService;

    public BranchesController(IBranchesService branchesService)
    {
        _branchesService = branchesService;
    }

    [HttpGet]
    [HttpGet("/api/auth/branches")]
    public async Task<ActionResult<List<BranchDto>>> GetBranches([FromQuery] bool activeOnly = true)
    {
        var branches = await _branchesService.GetAllBranchesAsync(activeOnly);
        return Ok(branches);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BranchDto>> GetBranchById(int id)
    {
        var branch = await _branchesService.GetBranchByIdAsync(id);
        if (branch == null) return NotFound(new { message = "Không tìm thấy chi nhánh!" });
        return Ok(branch);
    }

    [HttpPost]
    public async Task<ActionResult<BranchDto>> CreateBranch([FromBody] BranchCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.BranchCode) || string.IsNullOrWhiteSpace(req.BranchName))
        {
            return BadRequest(new { message = "Mã chi nhánh và Tên chi nhánh không được để trống!" });
        }

        try
        {
            var created = await _branchesService.CreateBranchAsync(req);
            return CreatedAtAction(nameof(GetBranchById), new { id = created.BranchId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi khi thêm chi nhánh: {ex.Message}" });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BranchDto>> UpdateBranch(int id, [FromBody] BranchUpdateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.BranchName))
        {
            return BadRequest(new { message = "Tên chi nhánh không được để trống!" });
        }

        try
        {
            var updated = await _branchesService.UpdateBranchAsync(id, req);
            if (updated == null) return NotFound(new { message = "Không tìm thấy chi nhánh để cập nhật!" });
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi khi cập nhật chi nhánh: {ex.Message}" });
        }
    }

    [HttpPatch("{id:int}/toggle-active")]
    [HttpPost("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var ok = await _branchesService.ToggleBranchActiveAsync(id);
        if (!ok) return NotFound(new { message = "Không tìm thấy chi nhánh!" });
        return Ok(new { message = "Đã thay đổi trạng thái chi nhánh thành công!" });
    }
}
