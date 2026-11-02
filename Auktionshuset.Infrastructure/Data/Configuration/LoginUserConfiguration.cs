using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration;

public sealed class LoginUserConfiguration : IEntityTypeConfiguration<LoginUser>
{
    public void Configure(EntityTypeBuilder<LoginUser> entity)
    {
        entity.HasKey(loginUser => loginUser.UserId);
        entity.Property(loginUser => loginUser.Email).IsRequired();
        entity.Property(loginUser => loginUser.PasswordHash).IsRequired();
        entity.Property(loginUser => loginUser.Role).IsRequired();
        entity.Property(loginUser => loginUser.IsActive).HasDefaultValue(true);
        entity.ToTable("LoginUsers", table => table.HasCheckConstraint("CK_LoginUser_Owner",
            "(\"CustomerId\" IS NOT NULL AND \"EmployeeId\" IS NULL AND \"Role\" = 'Customer') OR (\"CustomerId\" IS NULL AND \"EmployeeId\" IS NOT NULL AND \"Role\" IN ('Admin', 'AuctionAdmin', 'LotAdmin', 'Employee'))"));

        // Registration normalizes emails to lowercase; the unique index is the database backstop.
        entity.HasIndex(loginUser => loginUser.Email).IsUnique();
        entity.HasIndex(loginUser => loginUser.CustomerId).IsUnique();
        entity.HasIndex(loginUser => loginUser.EmployeeId).IsUnique();
        entity.HasOne(loginUser => loginUser.Employee).WithOne()
            .HasForeignKey<LoginUser>(loginUser => loginUser.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(loginUser => loginUser.Customer)
            .WithOne()
            .HasForeignKey<LoginUser>(loginUser => loginUser.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
