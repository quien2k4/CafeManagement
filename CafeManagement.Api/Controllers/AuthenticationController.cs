using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.Common.Models;
using CafeManagement.Core.DTOs.Authentication;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/auth")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;

    public AuthenticationController(IAuthenticationService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserSession>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu!" });
        }

        try
        {
            var session = await _authService.AuthenticateAsync(request.Username, request.Password, request.BranchId);
            if (session == null)
            {
                return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không chính xác!" });
            }

            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi hệ thống: {ex.Message}" });
        }
    }

    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        await _authService.EnsureAuthSeedDataAsync();
        return Ok(new { message = "Đã khởi tạo dữ liệu mẫu thành công." });
    }
}
