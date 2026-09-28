using System.ComponentModel.DataAnnotations;
using Xunit;
using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;
using Auktionshuset.Domain;

namespace Auktionshuset.Tests;

public class EmployeeRequestValidationTests
{
    [Fact]
    public void CreateAndUpdate_RejectMissingWhitespaceAndFutureBirthDates()
    {
        DateOnly future = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        CreateEmployeeRequest create = new()
        {
            FirstName = "  ",
            LastName = string.Empty,
            BirthDate = future,
            Address = "  ",
            AuctionHouseId = Guid.Empty
        };
        UpdateEmployeeRequest update = new()
        {
            FirstName = "  ",
            LastName = string.Empty,
            BirthDate = future,
            Address = "  ",
            AuctionHouseId = Guid.Empty
        };

        foreach (IValidatableObject request in new IValidatableObject[] { create, update })
        {
            List<ValidationResult> results = Validate(request);
            Assert.Contains(results, result => result.MemberNames.Contains("FirstName"));
            Assert.Contains(results, result => result.MemberNames.Contains("LastName"));
            Assert.Contains(results, result => result.MemberNames.Contains("Address"));
            Assert.Contains(results, result => result.MemberNames.Contains("BirthDate"));
            Assert.Contains(results, result => result.MemberNames.Contains("AuctionHouseId"));
        }
    }

    [Fact]
    public void CreateAndUpdate_RequireBirthDate()
    {
        CreateEmployeeRequest create = new()
        {
            FirstName = "Anna", LastName = "Jensen", BirthDate = default,
            Address = "Testvej 1", AuctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId
        };
        UpdateEmployeeRequest update = new()
        {
            FirstName = "Anna", LastName = "Jensen", BirthDate = default,
            Address = "Testvej 1", AuctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId
        };

        Assert.Contains(Validate(create), result => result.MemberNames.Contains("BirthDate"));
        Assert.Contains(Validate(update), result => result.MemberNames.Contains("BirthDate"));
    }


    private static List<ValidationResult> Validate(IValidatableObject value)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
