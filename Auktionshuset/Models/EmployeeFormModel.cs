using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Models;

public sealed class EmployeeFormModel : IValidatableObject
{

    public string FirstName { get; set; } = string.Empty;


    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Fødselsdato er påkrævet.")]
    [DataType(DataType.Date)]
    public DateOnly? BirthDate { get; set; }


    public string Address { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BirthDate is DateOnly birthDate && birthDate > DateOnly.FromDateTime(DateTime.Today))
        {
            yield return new ValidationResult("Fødselsdato må ikke ligge i fremtiden.", [nameof(BirthDate)]);
        }

        foreach (ValidationResult error in ValidateText(FirstName, nameof(FirstName), "Fornavn"))
        {
            yield return error;
        }

        foreach (ValidationResult error in ValidateText(LastName, nameof(LastName), "Efternavn"))
        {
            yield return error;
        }

        foreach (ValidationResult error in ValidateText(Address, nameof(Address), "Adresse"))
        {
            yield return error;
        }

    }

    private static IEnumerable<ValidationResult> ValidateText(string? value, string propertyName, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return new ValidationResult($"{label} er påkrævet.", [propertyName]);
        }
        else if (value.Trim().Length is < 2 or > 100)
        {
            yield return new ValidationResult($"{label} skal være mellem 2 og 100 tegn.", [propertyName]);
        }
    }
}
