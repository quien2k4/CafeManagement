using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.WorkShifts;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public WorkShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [HttpGet("current")]
    public async Task<ActionResult<WorkShiftDto?>> GetCurrentOpenShift([FromQuery] int branchId)
    {
        if (branchId <= 0) return BadRequest("BranchId không hợp lệ!");
        var shift = await _shiftService.GetCurrentOpenShiftAsync(branchId);
        return Ok(shift);
    }

    [HttpPost("open")]
    public async Task<ActionResult<WorkShiftDto>> OpenShift([FromBody] OpenShiftRequest req)
    {
        try
        {
            var shift = await _shiftService.OpenShiftAsync(req);
            return Ok(shift);
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

    [HttpPost("close")]
    public async Task<ActionResult<WorkShiftDto>> CloseShift([FromBody] CloseShiftRequest req)
    {
        try
        {
            var shift = await _shiftService.CloseShiftAsync(req);
            return Ok(shift);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
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

    [HttpGet("{id}/z-report")]
    public async Task<ActionResult<ZReportDto>> GetZReport(long id)
    {
        try
        {
            var rpt = await _shiftService.GetZReportAsync(id);
            return Ok(rpt);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkShiftDto>>> GetShifts([FromQuery] int? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var list = await _shiftService.GetShiftsAsync(branchId, from, to);
        return Ok(list);
    }
}
