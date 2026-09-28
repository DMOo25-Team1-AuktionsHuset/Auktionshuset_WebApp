using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> entity)
    {
        entity.HasKey(customer => customer.CustomerId);
        entity.Property(customer => customer.FirstName).IsRequired();
        entity.Property(customer => customer.LastName).IsRequired();
        entity.Property(customer => customer.Email).IsRequired();
        entity.Property(customer => customer.PhoneNumber).IsRequired();
        entity.Property(customer => customer.BirthDate).IsRequired();
        entity.Property(customer => customer.Address).IsRequired();
    }
}
