using Microsoft.AspNetCore.Mvc;

namespace MyProject.Features.Products;

[ApiController]
[Route("api/products")]
public class ProductsController(ProductService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> List(CancellationToken ct) =>
        Ok(await service.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> Get(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/image")]
    public async Task<ActionResult<ProductDto>> UploadImage(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadImageAsync(id, stream, file.FileName, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
