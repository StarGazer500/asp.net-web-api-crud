using MyProject.Shared;

namespace MyProject.Features.Products;

public class ProductEntity : BaseEntity
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string? ImageKey { get; set; }
}
