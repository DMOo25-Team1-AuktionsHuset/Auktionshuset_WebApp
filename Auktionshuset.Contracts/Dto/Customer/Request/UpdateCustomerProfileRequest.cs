using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Contracts.Dto.Customer.Request
{
    /// <summary>
    /// Fields the signed-in customer may change on their own profile. The login email,
    /// role, permissions, user id, and customer id are deliberately not part of the
    /// contract and can never be changed through it.
    /// </summary>
    public class UpdateCustomerProfileRequest
    {
        [Required(ErrorMessage = "Fornavn skal udfyldes.")]
        [StringLength(100, ErrorMessage = "Fornavn må højst fylde 100 tegn.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Efternavn skal udfyldes.")]
        [StringLength(100, ErrorMessage = "Efternavn må højst fylde 100 tegn.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefonnummer skal udfyldes.")]
        [RegularExpression(@"^(\+?[\d ()\-]{8,20})$", ErrorMessage = "Indtast et gyldigt telefonnummer.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Adresse skal udfyldes.")]
        [StringLength(200, ErrorMessage = "Adresse må højst fylde 200 tegn.")]
        public string Address { get; set; } = string.Empty;
    }
}
