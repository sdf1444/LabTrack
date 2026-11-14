using LabTrack.Api.Application;
using LabTrack.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace LabTrack.Api.Infrastructure;

public static class IdentityInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var roleName in LabTrackRoles.All)
        {
            if (await roles.RoleExistsAsync(roleName)) continue;
            var result = await roles.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create the {roleName} role: {string.Join("; ", result.Errors.Select(x => x.Description))}");
        }

        var email = configuration["LabTrack:BootstrapAdmin:Email"]?.Trim();
        var password = configuration["LabTrack:BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Set LabTrack__BootstrapAdmin__Email and LabTrack__BootstrapAdmin__Password before starting LabTrack.");

        var users = services.GetRequiredService<UserManager<LabTrackUser>>();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new LabTrackUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create the bootstrap administrator: {string.Join("; ", result.Errors.Select(x => x.Description))}");
        }

        if (!await users.IsInRoleAsync(user, LabTrackRoles.Administrator))
        {
            var result = await users.AddToRoleAsync(user, LabTrackRoles.Administrator);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Could not assign the bootstrap administrator role: {string.Join("; ", result.Errors.Select(x => x.Description))}");
        }
    }
}
