using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ims.Api.Data.Configurations;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> b)
    {
        b.ToTable("units");
        b.Property(u => u.Barcode).HasMaxLength(32).IsRequired();
        b.HasIndex(u => u.Barcode).IsUnique();
        b.Property(u => u.RegisteredAt).HasDefaultValueSql("now()");
        // Restrict = the database refuses to delete a product/user that a unit still points at.
        b.HasOne(u => u.Product).WithMany().HasForeignKey(u => u.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(u => u.RegisteredBy).WithMany().HasForeignKey(u => u.RegisteredById).OnDelete(DeleteBehavior.Restrict);
    }
}
