using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Contracts.Dto.Auth
{
    public sealed class LoginRequest
    {
        /// <summary>
        /// The email address of the user.
        /// the password of the user.
        
        [Required]
        [EmailAddress]
        public string Email { get; init; } = string.Empty;
        [Required]
        public string Password { get; init; } = string.Empty;
    }
}
