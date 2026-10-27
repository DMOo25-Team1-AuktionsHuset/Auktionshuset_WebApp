using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    public class MessagingReplayAuditConfiguration : IEntityTypeConfiguration<MessageReplayAudit>
    {
        public void Configure(EntityTypeBuilder<MessageReplayAudit> builder)
        {
            builder.ToTable("MessageReplayAudits");

            builder.HasKey(x => x.ReplayId);

            builder.Property(x => x.Pipeline)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(x => x.ActorId)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Reason)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(x => x.RequestedAtTime)
                .HasColumnType("timestamp with time zone");

            builder.HasIndex(x => new
            {
                x.Pipeline,
                x.MessageId,
                x.RequestedAtTime
            });
        }
    }
}
