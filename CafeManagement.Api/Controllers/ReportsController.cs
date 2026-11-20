using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Reports;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardReportDto>> GetDashboardReport(
        [FromQuery] int? branchId, 
        [FromQuery] string dateFilter = "ALL", 
        [FromQuery] int days = 7,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var report = await _reportService.GetDashboardReportAsync(branchId, dateFilter, days, fromDate, toDate);
        return Ok(report);
    }
}
