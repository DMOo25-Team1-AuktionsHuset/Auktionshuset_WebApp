using System;
using System.Collections.Generic;
using System.Text;


namespace Auktionshuset.Contracts.Dto.Auth
{
    public sealed record LoginResponse(
        string AccessToken,
        string TokenType,
        DateTimeOffset ExpiresAtUtc,
        AuthenticatedUserResponse User,
        string? RefreshToken = null,
        DateTimeOffset? RefreshTokenExpiresAtUtc = null
        );

    public sealed record AuthenticatedUserResponse(
        Guid UserId,
        string Email,
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> Permissions
        );
    

    
}
