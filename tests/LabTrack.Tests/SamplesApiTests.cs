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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
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
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LabTrack"] = database.GetConnectionString(),
                    ["LabTrack:BootstrapAdmin:Email"] = "admin@labtrack.test",
                    ["LabTrack:BootstrapAdmin:Password"] = "LabTrack-Test-Admin-2026!",
                    ["LabTrack:Monitoring:MetricsToken"] = "test-metrics-token"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<LabTrackDbContext>>();
                services.RemoveAll<DbContextOptions<LabTrackDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.AddDbContext<LabTrackDbContext>(options =>
                    options.UseNpgsql(database.GetConnectionString()));
            });
        });
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

        var unauthenticated = await client.GetAsync("/api/samples");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        var invalidMetricsToken = await client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidMetricsToken.StatusCode);
        using var metricsRequest = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        metricsRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-metrics-token");
        var metrics = await client.SendAsync(metricsRequest);
        Assert.Equal(HttpStatusCode.OK, metrics.StatusCode);

        var adminLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@labtrack.test",
            password = "LabTrack-Test-Admin-2026!",
            rememberMe = false
        });
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);

        var createTechnician = await client.PostAsJsonAsync("/api/auth/users", new
        {
            email = "technician@labtrack.test",
            password = "LabTrack-Test-Tech-2026!",
            role = "LabTechnician"
        });
        Assert.Equal(HttpStatusCode.Created, createTechnician.StatusCode);

        var firstUserPage = await client.GetFromJsonAsync<UserResponse[]>(
            "/api/auth/users?skip=0&take=1", JsonOptions);
        var secondUserPage = await client.GetFromJsonAsync<UserResponse[]>(
            "/api/auth/users?skip=1&take=1", JsonOptions);
        Assert.NotNull(firstUserPage);
        Assert.NotNull(secondUserPage);
        Assert.Equal("admin@labtrack.test", Assert.Single(firstUserPage).Email);
        Assert.Equal("technician@labtrack.test", Assert.Single(secondUserPage).Email);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/auth/users?skip=-1")).StatusCode);

        using var technicianClient = factory.CreateClient();
        var technicianLogin = await technicianClient.PostAsJsonAsync("/api/auth/login", new
        {
            email = "technician@labtrack.test",
            password = "LabTrack-Test-Tech-2026!",
            rememberMe = false
        });
        Assert.Equal(HttpStatusCode.OK, technicianLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technicianClient.GetAsync("/api/auth/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await technicianClient.GetAsync("/api/samples")).StatusCode);

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

        var missingRevision = await client.PatchAsJsonAsync(
            $"/api/samples/{created.Id}/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.BadRequest, missingRevision.StatusCode);

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
        var unrepresentableResult = await client.PostAsJsonAsync(
            $"/api/samples/{created.Id}/results",
            resultRequest with { Value = 1.23456m, ExpectedRevision = started.Revision },
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, unrepresentableResult.StatusCode);

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

        var invalidSample = await client.PostAsJsonAsync(
            "/api/samples",
            new CreateSampleRequest("   ", "   ", "   ", DateTimeOffset.UtcNow),
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSample.StatusCode);

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

        await AssertAuditHistoryIsAppendOnlyAsync(persisted.Events[0].Id);

        var logout = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    private async Task AssertAuditHistoryIsAppendOnlyAsync(Guid eventId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LabTrackDbContext>();

        var update = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync(
                """UPDATE "Events" SET "Description" = 'tampered' WHERE "Id" = {0}""", eventId));
        Assert.Equal("55000", update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync(
                """DELETE FROM "Events" WHERE "Id" = {0}""", eventId));
        Assert.Equal("55000", delete.SqlState);

        var truncate = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("""TRUNCATE TABLE "Events" """));
        Assert.Equal("55000", truncate.SqlState);
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
