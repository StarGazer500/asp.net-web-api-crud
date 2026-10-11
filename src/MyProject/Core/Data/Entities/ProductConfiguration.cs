using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyProject.Core.Data.Entities;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products");
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Price).HasPrecision(18, 2);
    }
}
