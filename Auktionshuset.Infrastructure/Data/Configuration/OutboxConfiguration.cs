using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    public class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.HasKey(o => o.OutboxId);
            builder.Property(o => o.EventType).IsRequired();
            builder.Property(o => o.Payload).IsRequired();
            builder.Property(o => o.OccuredAtTime).IsRequired();
            builder.Property(o => o.ProcessedAtTime).IsRequired(false);
            builder.Property(o => o.Attempts).IsRequired();
            builder.Property(o => o.NextAttemptAtTime)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();
            builder.Property(o => o.Error).IsRequired(false);
            builder.Property(o => o.ProcessedAtTime);
            builder.Property(o => o.LeaseToken);
            builder.Property(o => o.LeaseExpiresAtTime);
            builder.Property(o => o.QuarantinedAtTime);

            builder.HasIndex(o => new
            {
                o.NextAttemptAtTime,
                o.OccuredAtTime,
                o.OutboxId
            })
            .HasDatabaseName("OutboxMessages_Ready")
            .HasFilter(
                "\"ProcessedAtTime\" IS NULL " +
                "AND \"QuarantinedAtTime\" IS NULL");
        }
    }
}
