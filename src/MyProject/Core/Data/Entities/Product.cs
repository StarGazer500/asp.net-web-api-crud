using MyProject.Shared;

namespace MyProject.Core.Data.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string? ImageKey { get; set; }
}
