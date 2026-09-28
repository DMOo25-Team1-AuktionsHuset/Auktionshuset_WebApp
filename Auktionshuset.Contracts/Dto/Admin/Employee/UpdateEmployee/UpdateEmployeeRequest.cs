using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee
{
    public class UpdateEmployeeRequest : IValidatableObject
    {


        public string FirstName { get; init; } = string.Empty;



        public string LastName { get; init; } = string.Empty;

        public DateOnly BirthDate { get; init; }



        public string Address { get; init; } = string.Empty;

        public Guid AuctionHouseId { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(FirstName))
            {
                yield return new ValidationResult("Fornavn er påkrævet.", [nameof(FirstName)]);
            }
            else if (FirstName.Trim().Length is < 2 or > 100)
            {
                yield return new ValidationResult("Fornavn skal være mellem 2 og 100 tegn.", [nameof(FirstName)]);
            }

            if (string.IsNullOrWhiteSpace(LastName))
            {
                yield return new ValidationResult("Efternavn er påkrævet.", [nameof(LastName)]);
            }
            else if (LastName.Trim().Length is < 2 or > 100)
            {
                yield return new ValidationResult("Efternavn skal være mellem 2 og 100 tegn.", [nameof(LastName)]);
            }

            if (string.IsNullOrWhiteSpace(Address))
            {
                yield return new ValidationResult("Adresse er påkrævet.", [nameof(Address)]);
            }
            else if (Address.Trim().Length is < 2 or > 100)
            {
                yield return new ValidationResult("Adresse skal være mellem 2 og 100 tegn.", [nameof(Address)]);
            }

            if (BirthDate == default)
            {
                yield return new ValidationResult("Fødselsdato er påkrævet.", [nameof(BirthDate)]);
            }
            else if (BirthDate > DateOnly.FromDateTime(DateTime.Today))
            {
                yield return new ValidationResult("Fødselsdato må ikke ligge i fremtiden.", [nameof(BirthDate)]);
            }

            if (AuctionHouseId == Guid.Empty)
            {
                yield return new ValidationResult("Auktionshus er påkrævet.", [nameof(AuctionHouseId)]);
            }
        }
    }
}
