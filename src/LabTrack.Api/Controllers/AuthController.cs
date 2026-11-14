using LabTrack.Api.Contracts;
using LabTrack.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LabTrack.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    SignInManager<LabTrackUser> signInManager,
    UserManager<LabTrackUser> userManager) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid email or password." });

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid email or password." });

        return Ok(await ToResponseAsync(user));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(await ToResponseAsync(user));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await signInManager.SignOutAsync();
        return NoContent();
    }

    private async Task<UserResponse> ToResponseAsync(LabTrackUser user) =>
        new(user.Id, user.Email ?? "", (await userManager.GetRolesAsync(user)).ToArray());
}
