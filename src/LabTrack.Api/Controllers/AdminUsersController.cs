using System.ComponentModel.DataAnnotations;
using LabTrack.Api.Application;
using LabTrack.Api.Contracts;
using LabTrack.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LabTrack.Api.Controllers;

[ApiController]
[Authorize(Policy = LabTrackRoles.AdministratorPolicy)]
[Route("api/auth/users")]
public sealed class AdminUsersController(
    UserManager<LabTrackUser> users,
    ILogger<AdminUsersController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(
        CancellationToken ct,
        [FromQuery, Range(0, int.MaxValue)] int skip = 0,
        [FromQuery, Range(1, 100)] int take = 50)
    {
        var accountPage = await users.Users.AsNoTracking()
            .OrderBy(x => x.Email)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        var responses = new List<UserResponse>();
        foreach (var user in accountPage)
            responses.Add(await ToResponseAsync(user));
        return Ok(responses);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(id.ToString());
        return user is null ? NotFound() : Ok(await ToResponseAsync(user));
    }

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!LabTrackRoles.All.Contains(request.Role, StringComparer.Ordinal))
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = $"Role must be {LabTrackRoles.Technician} or {LabTrackRoles.Administrator}."
            });

        var email = request.Email.Trim();
        var user = new LabTrackUser { UserName = email, Email = email, EmailConfirmed = true };
        var createResult = await users.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName"))
                return Conflict(new ProblemDetails { Status = 409, Title = "An account with that email already exists." });

            var errors = createResult.Errors.ToDictionary(x => x.Code, x => new[] { x.Description });
            return BadRequest(new ValidationProblemDetails(errors));
        }

        var roleResult = await users.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            var roleErrorCodes = string.Join(", ", roleResult.Errors.Select(error => error.Code));
            logger.LogError(
                "Could not assign role {Role} to new user {UserId}. Identity error codes: {ErrorCodes}",
                request.Role, user.Id, roleErrorCodes);

            var rollbackResult = await users.DeleteAsync(user);
            if (!rollbackResult.Succeeded)
            {
                var rollbackErrorCodes = string.Join(", ", rollbackResult.Errors.Select(error => error.Code));
                logger.LogCritical(
                    "Could not remove user {UserId} after role assignment failed. Identity error codes: {ErrorCodes}",
                    user.Id, rollbackErrorCodes);
            }

            return Problem(
                title: "The account could not be assigned its requested role.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var response = await ToResponseAsync(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, response);
    }

    private async Task<UserResponse> ToResponseAsync(LabTrackUser user) =>
        new(user.Id, user.Email ?? "", (await users.GetRolesAsync(user)).ToArray());
}
