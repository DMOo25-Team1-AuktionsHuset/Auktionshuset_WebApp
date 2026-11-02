namespace Auktionshuset.Contracts.Dto.Customer.Response
{
    /// <summary>
    /// The signed-in customer's own profile. The customer id and email come from the
    /// validated session, never from the request.
    /// </summary>
    public sealed record CustomerProfileResponse(
        Guid CustomerId,
        string Email,
        string FirstName,
        string LastName,
        string PhoneNumber,
        DateOnly BirthDate,
        string Address);
}
