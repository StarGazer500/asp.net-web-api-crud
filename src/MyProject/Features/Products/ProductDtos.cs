using System.ComponentModel.DataAnnotations;

namespace MyProject.Features.Products;

// What the API returns.
public record ProductResponse(int Id, string Name, decimal Price, string? ImageUrl);

// POST body.
public class CreateProductRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    // Nullable so a missing price fails [Required] instead of silently becoming 0.
    [Required, Range(0, 1_000_000)]
    public decimal? Price { get; set; }
}

// PUT body.
public class UpdateProductRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [Required, Range(0, 1_000_000)]
    public decimal? Price { get; set; }
}
