using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ims.Api.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log");
        b.Property(a => a.Action).HasMaxLength(50).IsRequired();
        b.Property(a => a.EntityType).HasMaxLength(50).IsRequired();
        b.Property(a => a.EntityId).HasMaxLength(50);
        b.Property(a => a.EntityRepr).HasMaxLength(300);
        b.Property(a => a.OldValues).HasColumnType("jsonb");
        b.Property(a => a.NewValues).HasColumnType("jsonb");
        b.Property(a => a.IpAddress).HasColumnType("inet");
        b.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
        b.HasOne(a => a.Actor).WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => a.CreatedAt);
    }
}
