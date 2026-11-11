# LabTrack

LabTrack is a personal, end-to-end portfolio project for a small laboratory sample workflow. It lets a user register a specimen, move it through a controlled processing lifecycle, record test results, and review the sample's event history.

> **Portfolio demo only.** All seeded records are synthetic. This app is not a medical device, does not provide clinical advice, and must never be used to store real patient or laboratory data.

## What it demonstrates

- C# 14 and ASP.NET Core 10 Web API
- PostgreSQL with Entity Framework Core, relational constraints, and SQL-backed filtering
- Request validation, API error responses, and a clear service/domain boundary
- A controlled lifecycle: `Received → InProgress → Completed` or `Rejected`
- Reference-range flagging for results, with validated lower and upper limits
- An append-only workflow event history
- Optimistic concurrency using a sample revision sent by the client
- A small browser UI for registration, search, workflow actions, results, and history
- xUnit unit tests for lifecycle rules and result flags
- Docker Compose and GitHub Actions build/test workflow

## Run the app

Requires Docker Desktop or Docker Engine with Docker Compose.

```bash
docker compose up --build
```

Open [http://localhost:8080](http://localhost:8080). The app starts with three synthetic samples. Stop it with `Ctrl+C`. PostgreSQL data stays in the `labtrack-data` Docker volume; remove that volume to reset the demo data:

```bash
docker compose down -v
```

The API is served by the same application as the browser UI. Check app and database availability at `GET /api/health`.

## Run the tests

Requires the .NET 10 SDK and Docker Desktop or Docker Engine. API integration tests start an isolated PostgreSQL container. From this directory:

```bash
dotnet test tests/LabTrack.Tests/LabTrack.Tests.csproj
```

To build the API without Docker:

```bash
dotnet build src/LabTrack.Api/LabTrack.Api.csproj
```

For local execution outside Docker, start a PostgreSQL 16 database and set `ConnectionStrings__LabTrack`, then run:

```bash
dotnet run --project src/LabTrack.Api/LabTrack.Api.csproj
```

The local default connection string targets PostgreSQL on `localhost:5432` with the demo credentials in `appsettings.json`. Change it for your own environment.

## API overview

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/health` | Application and database health |
| `GET` | `/api/samples?search=&status=` | Search and filter the latest 100 samples |
| `POST` | `/api/samples` | Register a specimen |
| `GET` | `/api/samples/{id}` | Read sample, results, and event history |
| `PATCH` | `/api/samples/{id}/status` | Make a validated lifecycle transition |
| `POST` | `/api/samples/{id}/results` | Record a result while processing is in progress |

Mutation requests include `expectedRevision`. The server returns `409 Conflict` if the sample has changed since it was read. Accession numbers and analyte codes are unique within their intended scope at both service and database level.

## Design notes

`SampleService` owns the workflow decisions and persistence operations, while `SampleWorkflow` contains the small, deterministic lifecycle and reference-flag rules. The API never accepts an arbitrary status field in sample creation, and a sample cannot be completed without at least one result. Result and status changes append an event in the same EF Core save operation as the corresponding data change.

The UI is intentionally a small static client served by ASP.NET Core, so one process hosts the browser and API. The demo uses `EnsureCreated` for fast startup; a production service would use reviewed EF Core migrations, authentication/authorization, managed secret storage, privacy controls, structured audit retention, backups, and operational monitoring. These are future production requirements, not features of this portfolio demo.

## Suggested demo path

1. Open `LAB-2026-0041` and start processing.
2. Record an analyte and reference range, such as CRP with a value of `12` and limits `0`–`5`.
3. Observe the calculated `High` flag and the audit event.
4. Complete the sample and confirm the final state.
5. Register a second sample using a duplicate accession number to see conflict handling.

## Project structure

```text
src/LabTrack.Api/
  Application/       workflow service and API error handling
  Contracts/         request and response records
  Controllers/       HTTP endpoints
  Domain/            sample, result, event, and workflow rules
  Infrastructure/    EF Core model and demo seed data
  wwwroot/           browser UI

tests/LabTrack.Tests/ unit tests
.github/workflows/    build and test automation
```
