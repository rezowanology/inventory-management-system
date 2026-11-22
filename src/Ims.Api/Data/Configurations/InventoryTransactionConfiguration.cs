using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ims.Api.Data.Configurations;

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> b)
    {
        b.ToTable("inventory_transactions", t =>
        {
            // CHECK constraints: the database itself rejects rows that break these rules.
            t.HasCheckConstraint("ck_tx_component_only_on_tracked",
                "(event_type = 'COMPONENT_TRACKED' AND component_id IS NOT NULL AND component_barcode IS NOT NULL) OR " +
                "(event_type <> 'COMPONENT_TRACKED' AND component_id IS NULL AND component_barcode IS NULL)");
            t.HasCheckConstraint("ck_tx_reversal_shape",
                "(event_type = 'REVERSED' AND reverses_id IS NOT NULL AND char_length(btrim(reason)) >= 5) OR " +
                "(event_type <> 'REVERSED' AND reverses_id IS NULL)");
        });
        b.Property(t => t.EventType).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.ComponentBarcode).HasMaxLength(32);
        b.Property(t => t.Reason).HasDefaultValue("");
        b.Property(t => t.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(t => t.Unit).WithMany().HasForeignKey(t => t.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(t => t.Point).WithMany().HasForeignKey(t => t.PointId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(t => t.Component).WithMany().HasForeignKey(t => t.ComponentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(t => t.PerformedBy).WithMany().HasForeignKey(t => t.PerformedById).OnDelete(DeleteBehavior.Restrict);
        // Self-reference + unique index = "a row can be reversed at most once".
        b.HasOne(t => t.Reverses).WithMany().HasForeignKey(t => t.ReversesId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(t => t.ReversesId).IsUnique();

        b.HasIndex(t => new { t.UnitId, t.OccurredAt });
        b.HasIndex(t => new { t.EventType, t.OccurredAt });
        b.HasIndex(t => t.ComponentBarcode);
    }
}
