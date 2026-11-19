using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Products;
using CafeManagement.Core.DTOs.Toppings;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetProducts([FromQuery] int? categoryId = null, [FromQuery] bool activeOnly = false)
    {
        var list = await _productService.GetProductsAsync(categoryId, activeOnly);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetProductById(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null) return NotFound(new { message = "Không tìm thấy món ăn/nước uống!" });
        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] ProductCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProductName) || string.IsNullOrWhiteSpace(req.ProductCode))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ mã món và tên món!" });
        }

        try
        {
            var created = await _productService.CreateProductAsync(req);
            return CreatedAtAction(nameof(GetProductById), new { id = created.ProductId }, created);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi khi thêm món: {ex.Message}" });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(int id, [FromBody] ProductUpdateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProductName))
        {
            return BadRequest(new { message = "Tên món không được để trống!" });
        }

        try
        {
            var updated = await _productService.UpdateProductAsync(id, req);
            if (updated == null) return NotFound(new { message = "Không tìm thấy món để cập nhật!" });
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi khi cập nhật món: {ex.Message}" });
        }
    }

    [HttpPatch("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var ok = await _productService.ToggleProductActiveAsync(id);
        if (!ok) return NotFound(new { message = "Không tìm thấy món!" });
        return Ok(new { message = "Đã thay đổi trạng thái kinh doanh thành công!" });
    }

    [HttpGet("toppings")]
    [HttpGet("/api/toppings")]
    public async Task<ActionResult<List<ToppingDto>>> GetToppings([FromQuery] bool activeOnly = false)
    {
        var list = await _productService.GetToppingsAsync(activeOnly);
        return Ok(list);
    }
}
