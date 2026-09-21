using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    /// <summary>
    /// Configures the <see cref="Employee"/> entity, whose members can act as auctionarius.
    /// </summary>
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        /// <summary>
        /// Applies the configuration for <see cref="Employee"/>.
        /// </summary>
        /// <param name="entity">The builder used to configure the <see cref="Employee"/> entity.</param>
        public void Configure(EntityTypeBuilder<Employee> entity)
        {
            entity.HasKey(e => e.EmployeeId);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(80);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(80);
            entity.Property(e => e.BirthDate).IsRequired();
            entity.Property(e => e.Address).IsRequired().HasMaxLength(200);

            entity.HasOne(e => e.AuctionHouse)
                .WithMany(e => e.Employees)
                .HasForeignKey(e => e.AuctionHouseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
