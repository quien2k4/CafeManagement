using Microsoft.AspNetCore.Mvc;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Recipes;

namespace CafeManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _recipeService;

    public RecipesController(IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet("variants/{variantId:int}")]
    public async Task<ActionResult<RecipeDto>> GetCurrentRecipe(int variantId)
    {
        var recipe = await _recipeService.GetCurrentRecipeByVariantIdAsync(variantId);
        if (recipe == null)
        {
            return Ok(new RecipeDto { VariantId = variantId, Details = [] });
        }
        return Ok(recipe);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> SaveRecipe([FromBody] SaveRecipeRequest req)
    {
        if (req.VariantId <= 0)
        {
            return BadRequest(new { message = "Mã biến thể không hợp lệ!" });
        }

        try
        {
            var saved = await _recipeService.SaveRecipeAsync(req);
            return Ok(saved);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Lỗi khi lưu công thức: {ex.Message}" });
        }
    }
}
