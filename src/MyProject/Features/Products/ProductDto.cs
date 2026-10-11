using System.ComponentModel.DataAnnotations;

namespace MyProject.Features.Products;

public record ProductDto(int Id, string Name, decimal Price, string? ImageKey);

public class CreateProductDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }
}
