using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabTrack.Api.Contracts;
using LabTrack.Api.Domain;
using LabTrack.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace LabTrack.Tests;

public sealed class SamplesApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("labtrack")
        .WithUsername("labtrack")
        .WithPassword("labtrack_dev")
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<LabTrackDbContext>>();
                services.RemoveAll<DbContextOptions<LabTrackDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.AddDbContext<LabTrackDbContext>(options =>
                    options.UseNpgsql(database.GetConnectionString()));
            }));
        client = factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    [Fact]
    public async Task Sample_lifecycle_persists_results_events_and_reports_database_failures()
    {
        var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var initialSamples = await client.GetFromJsonAsync<SampleSummaryResponse[]>("/api/samples", JsonOptions);
        Assert.NotNull(initialSamples);
        Assert.Equal(3, initialSamples.Length);

        var request = new CreateSampleRequest(
            " lab-e2e-0001 ", "SYN-E2E-01", "Serum", DateTimeOffset.UtcNow);
        var createResponse = await client.PostAsJsonAsync("/api/samples", request, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SampleDetailResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("LAB-E2E-0001", created.AccessionNumber);
        Assert.Equal(SampleStatus.Received, created.Status);
        Assert.Single(created.Events);

        var duplicateResponse = await client.PostAsJsonAsync("/api/samples", request, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var prematureCompletion = await client.PatchAsJsonAsync(
            $"/api/samples/{created.Id}/status",
            new { status = "Completed", expectedRevision = created.Revision });
        Assert.Equal(HttpStatusCode.BadRequest, prematureCompletion.StatusCode);

        var startedResponse = await client.PatchAsJsonAsync(
            $"/api/samples/{created.Id}/status",
            new { status = "InProgress", expectedRevision = created.Revision });
        Assert.Equal(HttpStatusCode.OK, startedResponse.StatusCode);
        var started = await startedResponse.Content.ReadFromJsonAsync<SampleDetailResponse>(JsonOptions);
        Assert.NotNull(started);
        Assert.Equal(2, started.Revision);

        var resultRequest = new AddResultRequest(
            "crp", "C-reactive protein", 12m, "mg/L", 0m, 5m, 1);
        var staleResult = await client.PostAsJsonAsync(
            $"/api/samples/{created.Id}/results", resultRequest, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, staleResult.StatusCode);

        var invalidRange = resultRequest with
        {
            ReferenceLow = 5m,
            ReferenceHigh = 0m,
            ExpectedRevision = started.Revision
        };
        var invalidRangeResponse = await client.PostAsJsonAsync(
            $"/api/samples/{created.Id}/results", invalidRange, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRangeResponse.StatusCode);

        var validResult = resultRequest with { ExpectedRevision = started.Revision };
        var resultResponse = await client.PostAsJsonAsync(
            $"/api/samples/{created.Id}/results", validResult, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        var withResult = await resultResponse.Content.ReadFromJsonAsync<SampleDetailResponse>(JsonOptions);
        Assert.NotNull(withResult);
        Assert.Equal(3, withResult.Revision);
        Assert.Equal("CRP", Assert.Single(withResult.Results).AnalyteCode);
        Assert.Equal("High", withResult.Results[0].Flag);

        var duplicateResult = await client.PostAsJsonAsync(
            $"/api/samples/{created.Id}/results",
            validResult with { AnalyteCode = "CRP", ExpectedRevision = withResult.Revision },
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResult.StatusCode);

        var persisted = await client.GetFromJsonAsync<SampleDetailResponse>(
            $"/api/samples/{created.Id}", JsonOptions);
        Assert.NotNull(persisted);
        Assert.Single(persisted.Results);
        Assert.Equal(3, persisted.Events.Count);

        var completedResponse = await client.PatchAsJsonAsync(
            $"/api/samples/{created.Id}/status",
            new { status = "Completed", expectedRevision = persisted.Revision });
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        var completed = await completedResponse.Content.ReadFromJsonAsync<SampleDetailResponse>(JsonOptions);
        Assert.NotNull(completed);
        Assert.Equal(SampleStatus.Completed, completed.Status);
        Assert.Equal(4, completed.Revision);
        Assert.Equal(4, completed.Events.Count);

        await InstallNonUniqueInsertFailureAsync();
        var databaseFailure = await client.PostAsJsonAsync(
            "/api/samples",
            new CreateSampleRequest("LAB-E2E-DBFAIL", "SYN-E2E-02", "Serum", DateTimeOffset.UtcNow),
            JsonOptions);
        Assert.Equal(HttpStatusCode.InternalServerError, databaseFailure.StatusCode);
    }

    private async Task InstallNonUniqueInsertFailureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LabTrackDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE FUNCTION reject_e2e_sample_insert() RETURNS trigger AS $$
            BEGIN
                IF NEW."AccessionNumber" = 'LAB-E2E-DBFAIL' THEN
                    RAISE EXCEPTION 'injected non-unique database failure';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER reject_e2e_sample_insert_trigger
            BEFORE INSERT ON "Samples"
            FOR EACH ROW EXECUTE FUNCTION reject_e2e_sample_insert();
            """;
        await command.ExecuteNonQueryAsync();
    }
}
