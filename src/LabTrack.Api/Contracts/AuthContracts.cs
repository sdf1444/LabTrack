using System.ComponentModel.DataAnnotations;

namespace LabTrack.Api.Contracts;

/// <summary>Credentials and session preferences for signing in.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }

    public bool RememberMe { get; init; }
}

/// <summary>Creates a staff account with one of the supported LabTrack roles.</summary>
public sealed record CreateUserRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [Required, MinLength(12), MaxLength(100)]
    public required string Password { get; init; }

    [Required]
    public required string Role { get; init; }
}

/// <summary>Signed-in account details and assigned roles.</summary>
public sealed record UserResponse(Guid Id, string Email, IReadOnlyList<string> Roles);
