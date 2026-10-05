using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("RefreshTokens");
        entity.HasKey(token => token.Id);
        entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        entity.HasIndex(token => token.TokenHash).IsUnique();
        entity.HasIndex(token => token.FamilyId);
        entity.HasIndex(token => token.ExpiresAt);
        // Custom users aren't stored in AspNetUsers, so there is deliberately no Identity FK.
    }
}
