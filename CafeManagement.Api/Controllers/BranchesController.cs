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
    public async Task<ActionResult<List<BranchDto>>> GetBranches()
    {
        var branches = await _branchesService.GetActiveBranchesAsync();
        return Ok(branches);
    }
}
