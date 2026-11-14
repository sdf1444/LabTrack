using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LabTrack.Api.Infrastructure;

public sealed class LabTrackDesignTimeDbContextFactory : IDesignTimeDbContextFactory<LabTrackDbContext>
{
    public LabTrackDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("LabTrack")
            ?? "Host=localhost;Port=5432;Database=labtrack;Username=labtrack;Password=labtrack_dev";
        var options = new DbContextOptionsBuilder<LabTrackDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new LabTrackDbContext(options);
    }
}
