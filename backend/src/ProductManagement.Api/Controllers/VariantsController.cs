using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Variants;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/products/{productId:long}/items")]
public class VariantsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] ListVariantsHandler handler, long productId, CancellationToken ct)
        => Ok(await handler.HandleAsync(productId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] CreateVariantHandler handler, long productId,
        [FromBody] CreateVariantRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(productId, request, ct);
        return CreatedAtAction(nameof(List), new { productId }, result);
    }
}

[ApiController]
[Route("api/v1/product-items")]
public class ProductItemsController : ControllerBase
{
    [HttpPatch("{itemId:long}")]
    public async Task<IActionResult> Update(
        [FromServices] UpdateVariantHandler handler, long itemId,
        [FromBody] UpdateVariantRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("If-Match", out var etag) ||
            !int.TryParse(etag.ToString().Trim('"'), out var version))
            return StatusCode(428, new { title = "If-Match header with current version is required." });

        var requestWithVersion = request with { ExpectedVersion = version };
        return Ok(await handler.HandleAsync(itemId, requestWithVersion, ct));
    }

    [HttpDelete("{itemId:long}")]
    public async Task<IActionResult> Delete(
        [FromServices] DeleteVariantHandler handler, long itemId, CancellationToken ct)
    {
        await handler.HandleAsync(itemId, ct);
        return NoContent();
    }

    [HttpPost("{itemId:long}/inventory/adjust")]
    public async Task<IActionResult> AdjustStock(
        [FromServices] AdjustStockHandler handler, long itemId,
        [FromBody] AdjustStockRequest request, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var values) ? values.ToString() : null;
        var result = await handler.HandleAsync(null, itemId, request, idempotencyKey, ct);
        return result.Succeeded ? Ok(result) : Conflict(result);
    }
}
