using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace MyProject.Features.Products;

[ApiController]
[Route("api/products")]
public class ProductsController(ProductService service) : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    [HttpGet]
    public async Task<ActionResult<List<ProductResponse>>> List(CancellationToken ct) =>
        Ok(await service.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> Get(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductResponse>> Update(int id, UpdateProductRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/image")]
    public async Task<ActionResult<ProductResponse>> UploadImage(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadImageAsync(id, stream, file.FileName, ct));
    }

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetImage(int id, CancellationToken ct)
    {
        var (content, fileName) = await service.GetImageAsync(id, ct);
        if (!ContentTypes.TryGetContentType(fileName, out var contentType))
            contentType = "application/octet-stream";
        return File(content, contentType);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
