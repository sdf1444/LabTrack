using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using LabTrack.Api.Application;
using LabTrack.Api.Domain;
using LabTrack.Api.Infrastructure;
using LabTrack.Api.Middleware;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("LabTrack")
    ?? throw new InvalidOperationException("Set ConnectionStrings__LabTrack before starting LabTrack.");
var metricsToken = builder.Configuration["LabTrack:Monitoring:MetricsToken"];
if (string.IsNullOrWhiteSpace(metricsToken))
    throw new InvalidOperationException("Set LabTrack__Monitoring__MetricsToken before starting LabTrack.");

builder.Services.AddDbContext<LabTrackDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentity<LabTrackUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<LabTrackDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "LabTrack.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(LabTrackRoles.StaffPolicy, policy => policy.RequireRole(LabTrackRoles.Staff));
    options.AddPolicy(LabTrackRoles.AdministratorPolicy, policy => policy.RequireRole(LabTrackRoles.Administrator));
});
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ISampleService, SampleService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks().AddDbContextCheck<LabTrackDbContext>(name: "database");

var dataProtectionPath = builder.Configuration["LabTrack:DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
        .SetApplicationName("LabTrack");
}

var app = builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpMetrics();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/metrics")
    {
        var suppliedToken = context.Request.Headers.Authorization.ToString();
        var expectedToken = $"Bearer {metricsToken}";
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(suppliedToken), Encoding.UTF8.GetBytes(expectedToken)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/api/health").AllowAnonymous();
app.MapMetrics().AllowAnonymous();
if (app.Environment.IsDevelopment())
    app.MapOpenApi().AllowAnonymous();
app.MapFallbackToFile("index.html");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LabTrackDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
    await IdentityInitializer.InitializeAsync(scope.ServiceProvider, app.Configuration);
}

app.Run();

public partial class Program { }
