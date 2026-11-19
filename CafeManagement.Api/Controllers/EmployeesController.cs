using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Employees;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmployeeDto>>> GetEmployees()
    {
        var list = await _employeeService.GetEmployeesAsync();
        return Ok(list);
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
    {
        var list = await _employeeService.GetRolesAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee([FromBody] EmployeeCreateRequest req)
    {
        try
        {
            var result = await _employeeService.CreateEmployeeAsync(req);
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

    [HttpPut("{id}")]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(int id, [FromBody] EmployeeUpdateRequest req)
    {
        try
        {
            var result = await _employeeService.UpdateEmployeeAsync(id, req);
            return Ok(result);
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

    [HttpPost("{id}/toggle-status")]
    public async Task<ActionResult<bool>> ToggleStatus(int id)
    {
        var success = await _employeeService.ToggleStatusAsync(id);
        if (!success) return NotFound("Không tìm thấy nhân viên!");
        return Ok(success);
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<bool>> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var success = await _employeeService.ResetPasswordAsync(req.UserId, req.NewPassword);
        if (!success) return NotFound("Không tìm thấy tài khoản người dùng!");
        return Ok(success);
    }
}
