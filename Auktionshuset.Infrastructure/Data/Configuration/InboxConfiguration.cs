using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    public class InboxConfiguration : IEntityTypeConfiguration<InboxMessage>
    {
        public void Configure(EntityTypeBuilder<InboxMessage> builder)
        {
            builder.HasKey(i => i.InboxId);
            builder.Property(i => i.TransportMessageId);
            builder.Property(i => i.ConsumerName).IsRequired();
            builder.Property(i => i.MessageKey).IsRequired();
            builder.Property(i => i.EventType).IsRequired();
            builder.Property(i => i.Payload).HasColumnType("bytea").IsRequired();
            builder.Property(i => i.RecievedAtTime).HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
            builder.Property(i => i.ProcessedAtTime).IsRequired(false);
            builder.Property(i => i.Error).IsRequired(false);
            builder.Property(i => i.InboxQuarantinedAtTime).IsRequired(false);

            builder.HasIndex(i => new
            {
                i.ConsumerName,
                i.MessageKey
            })
            .IsUnique()
            .HasDatabaseName("InboxMessages_Identity");

            builder.HasIndex(i => new
            {
                i.RecievedAtTime,
                i.InboxId
            })
            .HasDatabaseName("InboxMessages_Ready")
            .HasFilter(
                "\"ProcessedAtTime\" IS NULL " +
                "AND \"InboxQuarantinedAtTime\" IS NULL");
        }
    }
}
