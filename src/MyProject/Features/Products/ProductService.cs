using Microsoft.EntityFrameworkCore;
using MyProject.Core.Data;
using MyProject.Shared.Abstractions;
using MyProject.Shared.Exceptions;

namespace MyProject.Features.Products;

public class ProductService(AppDbContext db, IFileStorage storage)
{
    public async Task<ProductResponse> GetAsync(int id, CancellationToken ct = default) =>
        ToResponse(await FindAsync(id, ct));

    public async Task<List<ProductResponse>> ListAsync(CancellationToken ct = default) =>
        (await db.Products.AsNoTracking().ToListAsync(ct)).Select(ToResponse).ToList();

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = new ProductEntity { Name = request.Name, Price = request.Price!.Value };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);

        product.Name = request.Name;
        product.Price = request.Price!.Value;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    public async Task<ProductResponse> UploadImageAsync(int id, Stream image, string fileName, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);

        product.ImageKey = await storage.UploadAsync(image, $"products/{id}/{Path.GetFileName(fileName)}", ct);
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    public async Task<(Stream Content, string FileName)> GetImageAsync(int id, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);
        if (product.ImageKey is null)
            throw new NotFoundException($"Product {id} has no image.");

        var content = await storage.DownloadAsync(product.ImageKey, ct)
            ?? throw new NotFoundException($"Image for product {id} was not found.");
        return (content, Path.GetFileName(product.ImageKey));
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);

        if (product.ImageKey is not null)
            await storage.DeleteAsync(product.ImageKey, ct);
    }

    private async Task<ProductEntity> FindAsync(int id, CancellationToken ct) =>
        await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException($"Product {id} was not found.");

    // Clients get a URL they can call, not the internal storage key.
    private static ProductResponse ToResponse(ProductEntity p) =>
        new(p.Id, p.Name, p.Price, p.ImageKey is null ? null : $"/api/products/{p.Id}/image");
}
