using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Contracts.Dto.Customer.Request;
using Auktionshuset.Contracts.Dto.Customer.Response;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Customer;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/customer")
            .WithTags("Customer")
            .RequireAuthorization(SecurityPolicies.Customer);

        group.MapGet("/me", HandleGetProfileAsync)
            .WithName("GetMyCustomerProfile")
            .Produces<CustomerProfileResponse>()
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPut("/me", HandleUpdateProfileAsync)
            .WithName("UpdateMyCustomerProfile")
            .Produces<CustomerProfileResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapCustomerAuctions();
        Auktionshuset.Api.Endpoints.Billing.InvoiceEndpoints.MapCustomerInvoices(group);
        return endpoints;
    }

    public static async Task<Results<Ok<CustomerProfileResponse>, ProblemHttpResult>> HandleGetProfileAsync(
        ClaimsPrincipal user,
        CustomerProfileService profiles,
        CancellationToken cancellationToken)
    {
        CustomerProfileResult result = await profiles.GetOwnProfileAsync(user, cancellationToken);
        return result.Failure == CustomerProfileFailure.None
            ? TypedResults.Ok(result.Profile!)
            : TypedResults.Problem(CustomerProfileProblems.NoCustomerLink());
    }

    public static async Task<Results<Ok<CustomerProfileResponse>, ProblemHttpResult, ValidationProblem>>
        HandleUpdateProfileAsync(
            UpdateCustomerProfileRequest request,
            ClaimsPrincipal user,
            CustomerProfileService profiles,
            CancellationToken cancellationToken)
    {
        if (Validate(request) is ValidationProblem invalid)
        {
            return invalid;
        }

        UpdateOwnProfileResult result = await profiles.UpdateOwnProfileAsync(user, request, cancellationToken);
        return result.Failure == CustomerProfileFailure.None
            ? TypedResults.Ok(result.Profile!)
            : TypedResults.Problem(CustomerProfileProblems.NoCustomerLink());
    }

    /// <summary>
    /// Runs the request's data annotations explicitly, so validation behaves the same in
    /// hosts without the framework's automatic minimal API validation.
    /// </summary>
    private static ValidationProblem? Validate(UpdateCustomerProfileRequest request)
    {
        var context = new ValidationContext(request);
        List<ValidationResult> results = [];
        bool isValid = Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        if (isValid)
        {
            return null;
        }

        Dictionary<string, string[]> errors = results
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty).Select(member => (member, result)))
            .GroupBy(entry => entry.member, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.result.ErrorMessage ?? "Ugyldig værdi.").ToArray(),
                StringComparer.Ordinal);
        return TypedResults.ValidationProblem(errors);
    }
}

public static class CustomerProfileProblems
{
    public static ProblemDetails NoCustomerLink() => new()
    {
        Status = StatusCodes.Status403Forbidden,
        Title = "Adgang nægtet",
        Detail = "Din konto har ikke en gyldig kobling til en kundeprofil. Kontakt auktionshuset, hvis problemet fortsætter."
    };
}
