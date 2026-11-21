using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.Property(p => p.Id).IsRequired();
            builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
            builder.Property(p => p.Description).IsRequired();
            builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
            builder.Property(p => p.PictureUrl).IsRequired();

            // Agent attributes (Phase 2). No HasDefaultValue on StockQuantity on purpose:
            // EF would then skip a real 0 and save the default instead.
            builder.Property(p => p.Color).HasMaxLength(30);
            builder.Property(p => p.Material).HasMaxLength(40);
            builder.Property(p => p.StockQuantity).IsRequired();
            builder.Property(p => p.Rating).HasColumnType("numeric(2,1)");
            builder.HasOne(p => p.ProductBrand).WithMany()
                .HasForeignKey(p => p.ProductBrandId);
            builder.HasOne(p => p.ProductType).WithMany()
                .HasForeignKey(p => p.ProductTypeId);
        }
    }
}