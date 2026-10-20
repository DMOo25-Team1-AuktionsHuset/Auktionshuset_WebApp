using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Contracts.Dto.Auth;

public sealed record RefreshTokenRequest(
    [property: Required, StringLength(128)] string RefreshToken);
