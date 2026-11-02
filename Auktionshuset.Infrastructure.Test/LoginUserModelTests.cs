using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Auktionshuset.Infrastructure.Test;

public class LoginUserModelTests
{
    [Fact]
    public void LoginUser_HasUniqueEmailAndUniqueCustomerLink()
    {
        var options = new DbContextOptionsBuilder<AHDBContext>()
            .UseNpgsql("Host=localhost;Database=auction-model-test;Username=postgres;Password=postgres")
            .Options;
        using var context = new AHDBContext(options);

        IEntityType loginUserType = context.Model.FindEntityType(typeof(LoginUser))!;

        IIndex emailIndex = Assert.Single(loginUserType.GetIndexes(), index =>
            index.Properties.Single().Name == nameof(LoginUser.Email));
        Assert.True(emailIndex.IsUnique);

        IIndex customerIndex = Assert.Single(loginUserType.GetIndexes(), index =>
            index.Properties.Single().Name == nameof(LoginUser.CustomerId));
        Assert.True(customerIndex.IsUnique);

        IForeignKey customerLink = Assert.Single(loginUserType.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Customer));
        Assert.Equal(typeof(Customer), customerLink.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(LoginUser.CustomerId), Assert.Single(customerLink.Properties).Name);
        Assert.Equal(DeleteBehavior.Cascade, customerLink.DeleteBehavior);

        Assert.True(Assert.Single(loginUserType.GetIndexes(), i => i.Properties.Single().Name == nameof(LoginUser.EmployeeId)).IsUnique);
        var employeeLink = Assert.Single(loginUserType.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Employee));
        Assert.Equal(DeleteBehavior.Restrict, employeeLink.DeleteBehavior);
        Assert.True(loginUserType.FindProperty(nameof(LoginUser.CustomerId))!.IsNullable);
        Assert.True(loginUserType.FindProperty(nameof(LoginUser.EmployeeId))!.IsNullable);
        // Model comparison only: this does not connect to PostgreSQL.
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
