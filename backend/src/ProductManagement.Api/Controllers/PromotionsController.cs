using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Promotions;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/promotions")]
public class PromotionsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] ListPromotionsHandler handler, CancellationToken ct)
        => Ok(await handler.HandleAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] CreatePromotionHandler handler,
        [FromBody] CreatePromotionRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(List), result);
    }

    [HttpPost("{promotionId:long}/categories/{categoryId:long}")]
    public async Task<IActionResult> AttachCategory(
        [FromServices] AttachPromotionCategoryHandler handler,
        long promotionId, long categoryId, CancellationToken ct)
        => Ok(await handler.HandleAsync(promotionId, categoryId, ct));
}
