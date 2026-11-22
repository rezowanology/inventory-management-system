using Ims.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Data;

/// <summary>
/// The EF Core "session" with the database. IdentityUserContext gives us the Identity user tables
/// (but not role tables: our role is a plain column on <see cref="ApplicationUser"/>).
/// Each DbSet is a table; each entity's columns are described in Data/Configurations.
/// </summary>
public class ImsDbContext(DbContextOptions<ImsDbContext> options)
    : IdentityUserContext<ApplicationUser, long>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<Point> Points => Set<Point>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // let Identity describe its tables first

        // Pick up every IEntityTypeConfiguration<T> class in this assembly.
        builder.ApplyConfigurationsFromAssembly(typeof(ImsDbContext).Assembly);

        // Give the remaining Identity tables snake_case names too.
        builder.Entity<IdentityUserClaim<long>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<long>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<long>>().ToTable("user_tokens");
    }
}
