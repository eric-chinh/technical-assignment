using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Categories;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
public class CategoriesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> List(
        [FromServices] ListCategoriesHandler handler,
        [FromQuery] long? parentId, [FromQuery] bool? activeOnly, CancellationToken ct)
        => Ok(await handler.HandleAsync(parentId, activeOnly, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CategoryDto>> GetById(
        [FromServices] GetCategoryHandler handler, long id, CancellationToken ct)
        => Ok(await handler.HandleAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromServices] CreateCategoryHandler handler, [FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<CategoryDto>> Update(
        [FromServices] UpdateCategoryHandler handler, long id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
        => Ok(await handler.HandleAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        [FromServices] DeleteCategoryHandler handler, long id, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }
}
