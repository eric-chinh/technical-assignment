using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using ProductManagement.Application.Products;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] ListProductsHandler handler,
        [FromQuery] long? categoryId, [FromQuery] short? status, [FromQuery] string? q,
        [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice, [FromQuery] string? attributes,
        [FromQuery] string? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var page = await handler.HandleAsync(categoryId, status, q, minPrice, maxPrice, attributes, cursor, limit, ct);
        return Ok(page);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById([FromServices] GetProductHandler handler, long id, CancellationToken ct)
    {
        var result = await handler.ByIdAsync(id, ct);
        SetETag(result.Xmin);
        return Ok(result.Product);
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug([FromServices] GetProductHandler handler, string slug, CancellationToken ct)
    {
        var result = await handler.BySlugAsync(slug, ct);
        SetETag(result.Xmin);
        return Ok(result.Product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] CreateProductHandler handler, [FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        [FromServices] UpdateProductHandler handler, long id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var expectedXmin = ParseIfMatch();
        var result = await handler.HandleAsync(id, expectedXmin, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromServices] DeleteProductHandler handler, long id, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/image")]
    public async Task<IActionResult> UploadImage(
        [FromServices] UploadProductImageHandler handler, long id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var url = await handler.HandleAsync(id, stream, file.FileName, file.ContentType, file.Length, ct);
        return Ok(new { imageUrl = url });
    }

    [HttpDelete("{id:long}/image")]
    public async Task<IActionResult> DeleteImage(
        [FromServices] DeleteProductImageHandler handler, long id, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }

    private void SetETag(uint xmin) => Response.Headers.ETag = new EntityTagHeaderValue($"\"{xmin}\"").ToString();

    private uint ParseIfMatch()
    {
        var header = Request.Headers.IfMatch.ToString().Trim('"');
        return uint.TryParse(header, out var value) ? value : 0; // 0 never matches a real xmin -> guaranteed 409
    }
}
