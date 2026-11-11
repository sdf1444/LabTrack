using System.Text.Json.Serialization;
using LabTrack.Api.Application;
using LabTrack.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("LabTrack")
    ?? "Host=localhost;Port=5432;Database=labtrack;Username=labtrack;Password=labtrack_dev";
builder.Services.AddDbContext<LabTrackDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SampleService>();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks().AddDbContextCheck<LabTrackDbContext>(name: "database");

var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHealthChecks("/api/health");
app.MapFallbackToFile("index.html");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LabTrackDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.Run();

public partial class Program { }
