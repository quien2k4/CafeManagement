using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Customers;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> GetCustomers([FromQuery] string? search = null, [FromQuery] string? tier = null)
    {
        var list = await _customerService.GetCustomersAsync(search, tier);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> GetCustomerById(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null) return NotFound(new { message = "Không tìm thấy khách hàng!" });
        return Ok(customer);
    }

    [HttpGet("by-phone/{phone}")]
    public async Task<ActionResult<CustomerDto>> GetCustomerByPhone(string phone)
    {
        var customer = await _customerService.GetCustomerByPhoneAsync(phone);
        if (customer == null) return NotFound(new { message = $"Không tìm thấy khách hàng với SĐT '{phone}'!" });
        return Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CustomerCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PhoneNumber) || string.IsNullOrWhiteSpace(req.FullName))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ họ tên và số điện thoại!" });
        }

        try
        {
            var created = await _customerService.CreateCustomerAsync(req);
            return CreatedAtAction(nameof(GetCustomerById), new { id = created.CustomerId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi tạo khách hàng: {ex.Message}" });
        }
    }

    [HttpPost("adjust-points")]
    public async Task<ActionResult<CustomerDto>> AdjustPoints([FromBody] AdjustCustomerPointsRequest req)
    {
        try
        {
            var customer = await _customerService.AdjustPointsAsync(req);
            return Ok(customer);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi điều chỉnh điểm: {ex.Message}" });
        }
    }
}
