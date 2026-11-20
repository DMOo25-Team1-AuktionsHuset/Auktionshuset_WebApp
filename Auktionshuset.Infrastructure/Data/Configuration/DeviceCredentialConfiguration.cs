using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    public sealed class DeviceCredentialConfiguration : IEntityTypeConfiguration<DeviceCredential>
    {
        public void Configure(
            EntityTypeBuilder<DeviceCredential> entity)
        {
            entity.ToTable("DeviceCredentials");

            entity.HasKey(credential => credential.DeviceId);

            entity.Property(credential => credential.SecretHash)
                .IsRequired();

            entity.Property(credential => credential.IsEnabled)
                .IsRequired();

            entity.Property(credential => credential.CredentialVersion)
                .IsRequired()
                .IsConcurrencyToken();

            entity.HasOne(credential => credential.Device)
                .WithOne()
                .HasForeignKey<DeviceCredential>(
                    credential => credential.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
