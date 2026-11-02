using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Contracts.Dto.Auth
{
    /// <summary>
    /// Public customer self-registration. The server always assigns the Customer role;
    /// the contract deliberately has no role, permission, or customer id fields.
    /// </summary>
    public sealed record RegisterCustomerRequest
    {
        [Required(ErrorMessage = "Fornavn skal udfyldes.")]
        [StringLength(100, ErrorMessage = "Fornavn må højst fylde 100 tegn.")]
        public string FirstName { get; init; } = string.Empty;

        [Required(ErrorMessage = "Efternavn skal udfyldes.")]
        [StringLength(100, ErrorMessage = "Efternavn må højst fylde 100 tegn.")]
        public string LastName { get; init; } = string.Empty;

        [Required(ErrorMessage = "Email skal udfyldes.")]
        [EmailAddress(ErrorMessage = "Indtast en gyldig emailadresse.")]
        [StringLength(254, ErrorMessage = "Email må højst fylde 254 tegn.")]
        public string Email { get; init; } = string.Empty;

        [Required(ErrorMessage = "Telefonnummer skal udfyldes.")]
        [RegularExpression(@"^(\+?[\d ()\-]{8,20})$", ErrorMessage = "Indtast et gyldigt telefonnummer.")]
        public string PhoneNumber { get; init; } = string.Empty;

        [Required(ErrorMessage = "Fødselsdato skal udfyldes.")]
        public DateOnly BirthDate { get; init; }

        [Required(ErrorMessage = "Adresse skal udfyldes.")]
        [StringLength(200, ErrorMessage = "Adresse må højst fylde 200 tegn.")]
        public string Address { get; init; } = string.Empty;

        [Required(ErrorMessage = "Adgangskode skal udfyldes.")]
        [MinLength(8, ErrorMessage = "Adgangskoden skal være mindst 8 tegn.")]
        [StringLength(1024, ErrorMessage = "Adgangskoden må højst fylde 1024 tegn.")]
        public string Password { get; init; } = string.Empty;

        [Required(ErrorMessage = "Gentag adgangskode skal udfyldes.")]
        public string ConfirmPassword { get; init; } = string.Empty;
    }

    public sealed record RegisterCustomerResponse(Guid CustomerId, string Email);
}
