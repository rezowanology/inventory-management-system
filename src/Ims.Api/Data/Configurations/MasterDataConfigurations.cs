using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ims.Api.Data.Configurations;

// The "name must be unique among non-deleted rows" indexes are partial indexes created with raw SQL
// in the AppendOnlyAndIndexes migration, because EF cannot express lower(name) indexes directly.
public static class MasterDataColumns
{
    public static void Configure<T>(EntityTypeBuilder<T> b) where T : MasterDataEntity
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.IsDeleted).HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b) { b.ToTable("products"); MasterDataColumns.Configure(b); }
}

public class ComponentConfiguration : IEntityTypeConfiguration<Component>
{
    public void Configure(EntityTypeBuilder<Component> b) { b.ToTable("components"); MasterDataColumns.Configure(b); }
}

public class PointConfiguration : IEntityTypeConfiguration<Point>
{
    public void Configure(EntityTypeBuilder<Point> b)
    {
        b.ToTable("points");
        MasterDataColumns.Configure(b);
        b.Property(x => x.PointType).HasConversion<string>().HasMaxLength(20);
    }
}
