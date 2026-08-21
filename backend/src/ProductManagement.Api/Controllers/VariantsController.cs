using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Variants;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/products/{productId:long}/variants")]
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

    [HttpPut("{variantId:long}")]
    public async Task<IActionResult> Update(
        [FromServices] UpdateVariantHandler handler, long productId, long variantId,
        [FromBody] UpdateVariantRequest request, CancellationToken ct)
        => Ok(await handler.HandleAsync(variantId, request, ct));

    [HttpDelete("{variantId:long}")]
    public async Task<IActionResult> Delete(
        [FromServices] DeleteVariantHandler handler, long productId, long variantId, CancellationToken ct)
    {
        await handler.HandleAsync(variantId, ct);
        return NoContent();
    }

    [HttpPatch("{variantId:long}/stock")]
    public async Task<IActionResult> AdjustStock(
        [FromServices] AdjustStockHandler handler, long productId, long variantId,
        [FromBody] AdjustStockRequest request, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var values) ? values.ToString() : null;
        var result = await handler.HandleAsync(productId, variantId, request, idempotencyKey, ct);
        return result.Succeeded ? Ok(result) : Conflict(result);
    }
}
