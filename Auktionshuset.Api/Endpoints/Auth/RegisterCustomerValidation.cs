using System.ComponentModel.DataAnnotations;
using Auktionshuset.Contracts.Dto.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Auth;

/// <summary>
/// Server-side validation for public customer registration. The same rules are
/// enforced in the frontend; the API never trusts the caller alone.
/// </summary>
public static class RegisterCustomerValidation
{
    private const int MinimumAgeInYears = 0;
    private const int MaximumAgeInYears = 120;

    public static ValidationProblem? Validate(RegisterCustomerRequest request)
    {
        Dictionary<string, string[]> errors = new(StringComparer.Ordinal);

        void Add(string field, string message) =>
            errors[field] = [message];

        string firstName = request.FirstName.Trim();
        string lastName = request.LastName.Trim();
        string email = request.Email.Trim();
        string phoneNumber = request.PhoneNumber.Trim();
        string address = request.Address.Trim();

        if (firstName.Length == 0) Add("firstName", "Fornavn skal udfyldes.");
        else if (firstName.Length > 100) Add("firstName", "Fornavn må højst fylde 100 tegn.");

        if (lastName.Length == 0) Add("lastName", "Efternavn skal udfyldes.");
        else if (lastName.Length > 100) Add("lastName", "Efternavn må højst fylde 100 tegn.");

        if (email.Length == 0) Add("email", "Email skal udfyldes.");
        else if (email.Length > 254) Add("email", "Email må højst fylde 254 tegn.");
        else if (!new EmailAddressAttribute().IsValid(email)) Add("email", "Indtast en gyldig emailadresse.");

        if (phoneNumber.Length == 0) Add("phoneNumber", "Telefonnummer skal udfyldes.");
        else if (!new RegularExpressionAttribute(@"^(\+?[\d ()\-]{8,20})$").IsValid(phoneNumber))
        {
            Add("phoneNumber", "Indtast et gyldigt telefonnummer.");
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.BirthDate < today.AddYears(-MaximumAgeInYears)
            || request.BirthDate > today.AddYears(-MinimumAgeInYears))
        {
            Add("birthDate", "Indtast en gyldig fødselsdato.");
        }

        if (address.Length == 0) Add("address", "Adresse skal udfyldes.");
        else if (address.Length > 200) Add("address", "Adresse må højst fylde 200 tegn.");

        if (request.Password.Length < 8) Add("password", "Adgangskoden skal være mindst 8 tegn.");
        else if (request.Password.Length > 1024) Add("password", "Adgangskoden må højst fylde 1024 tegn.");

        if (request.ConfirmPassword.Length == 0) Add("confirmPassword", "Gentag adgangskode skal udfyldes.");
        else if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            Add("confirmPassword", "Adgangskoderne er ikke ens.");
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }

    public static ProblemDetails DuplicateEmailProblem() => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Emailen er allerede i brug",
        Detail = "Der findes allerede en konto med denne emailadresse."
    };
}
