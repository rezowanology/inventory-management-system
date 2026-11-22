using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ims.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.ToTable("users");
        b.Property(u => u.FirstName).HasMaxLength(150).HasDefaultValue("");
        b.Property(u => u.LastName).HasMaxLength(150).HasDefaultValue("");
        b.Property(u => u.EmployeeNo).HasMaxLength(20).IsRequired();
        b.HasIndex(u => u.EmployeeNo).IsUnique();
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20); // no DB default: ADMIN is the enum zero value and would be overwritten by it
        b.Property(u => u.IsActive).HasDefaultValue(false);
        b.Ignore(u => u.DisplayName); // computed in C#, not a column
    }
}
