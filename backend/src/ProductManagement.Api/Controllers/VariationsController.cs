using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Variations;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/categories/{categoryId:long}/variations")]
public class VariationsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] ListVariationsHandler handler, long categoryId, CancellationToken ct)
        => Ok(await handler.HandleAsync(categoryId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] CreateVariationHandler handler, long categoryId,
        [FromBody] CreateVariationRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(categoryId, request, ct);
        return CreatedAtAction(nameof(List), new { categoryId }, result);
    }
}

[ApiController]
[Route("api/v1/variations")]
public class VariationOptionsController : ControllerBase
{
    [HttpPost("{variationId:long}/options")]
    public async Task<IActionResult> CreateOption(
        [FromServices] CreateVariationOptionHandler handler, long variationId,
        [FromBody] CreateVariationOptionRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(variationId, request, ct);
        return Created($"/api/v1/variations/{variationId}/options/{result.Id}", result);
    }
}
