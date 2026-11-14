# LabTrack

LabTrack is a personal, end-to-end portfolio project for a small laboratory sample workflow. It lets a user register a specimen, move it through a controlled processing lifecycle, record test results, and review the sample's event history.

> **Portfolio demo only.** All seeded records are synthetic. This app is not a medical device, does not provide clinical advice, and must never be used to store real patient or laboratory data.

## What it demonstrates

- C# 14 and ASP.NET Core 10 Web API
- PostgreSQL with Entity Framework Core, relational constraints, and SQL-backed filtering
- ASP.NET Core Identity cookie authentication with administrator and technician roles
- Versioned EF Core migrations and PostgreSQL append-only audit-event triggers
- Request validation, API error responses, and a clear service/domain boundary
- A controlled lifecycle: `Received → InProgress → Completed` or `Rejected`
- Reference-range flagging for results, with validated lower and upper limits
- An append-only workflow event history
- Optimistic concurrency using a sample revision sent by the client
- A React 19 and strict TypeScript browser UI for sign-in, registration, search, workflow actions, results, and history
- Runtime-validated API contracts, React Query server state, and React Router navigation
- Prometheus metrics, PostgreSQL exporter, and a provisioned Grafana dashboard
- Manual PostgreSQL backup and confirmation-gated restore scripts
- xUnit unit tests for lifecycle rules, result flags, and API error responses
- Docker Compose and GitHub Actions build/test workflow

## Project context and scope

This is a self-directed portfolio project, not a system built from laboratory stakeholder interviews. The intended users (a lab technician and an administrator) and the workflow are assumptions used to create a realistic engineering exercise. The project is meant to demonstrate a small, traceable sample lifecycle, not model a real laboratory's procedures.

The demo's acceptance targets are that staff can register and find synthetic samples, move them through allowed states, record results while processing, and review an append-only event history. Administrators manage accounts; technicians can process samples but cannot manage users. Concurrent edits should not silently overwrite each other.

Deliberate boundaries: no real patient data, clinical decisions, laboratory-system integrations, regulatory claims, multi-tenancy, or high-availability guarantees. Before any real use, the workflow, authorization model, data handling, and operational controls would need domain-owner and security review.

Key design choices reflect that scope:

- ASP.NET Core serves both the controller API and the built React assets, keeping the demo to one application deployment rather than operating a separate frontend service.
- Identity cookies suit this same-origin browser UI; a token-based public-client flow is not needed for the current scope.
- EF Core migrations preserve schema history. The application currently applies them at startup for a simple Compose experience; a production deployment should run migrations as a controlled release step.
- Sample revisions reject stale writes. PostgreSQL triggers protect event history from ordinary update/delete/truncate operations, but database owners can change those protections.
- Compose makes the full stack reproducible locally. Its manual backups, local-only ports, and monitoring dashboard are a learning/demo baseline—not a tested production recovery or alerting service.

## Run the app

Requires Docker Desktop or Docker Engine with Docker Compose.

```bash
Copy-Item .env.example .env
# Edit .env and set unique, strong values for every variable.
docker compose up --build
```

Open [http://localhost:8080](http://localhost:8080) and sign in with `LABTRACK_ADMIN_EMAIL` and `LABTRACK_ADMIN_PASSWORD` from `.env`. Unauthenticated users are directed to the dedicated `/login` page, and signing out returns there. The app creates that administrator only on first startup; changing the environment password later does not change an existing account.

The initial dashboard is at [http://localhost:3000](http://localhost:3000) (user `admin`, password `GRAFANA_ADMIN_PASSWORD` from `.env`). Prometheus is available at [http://localhost:9090](http://localhost:9090). The app's `/metrics` endpoint requires the bearer token configured by `LABTRACK_METRICS_TOKEN`.

The app starts with three synthetic samples. Stop it with `Ctrl+C`. PostgreSQL, data-protection keys, Prometheus, and Grafana state use Docker volumes. Remove them to reset the demo environment:

```bash
docker compose down -v
```

The API is served by the same application as the browser UI. `GET /api/health` checks app/database availability. The database schema is applied through EF Core migrations at startup.

The UI is a React app built with Vite. Docker builds the frontend as part of the image. To run the .NET app locally, first build the frontend (requires Node.js 24):

```bash
cd src/LabTrack.Api/ClientApp
npm ci
npm run build
cd ../../..
dotnet run --project src/LabTrack.Api/LabTrack.Api.csproj
```

Frontend quality checks (strict type-checking, ESLint, component/API tests, a production build, and a Playwright workflow against the built UI with a deterministic API mock plus Axe WCAG checks) run in CI. Backend integration tests separately exercise the API and PostgreSQL. To run frontend checks locally, install the Playwright Chromium browser once with `npx playwright install chromium`, then run `npm run check`.

For local execution outside Compose, also set `ConnectionStrings__LabTrack`, `LabTrack__BootstrapAdmin__Email`, `LabTrack__BootstrapAdmin__Password`, and `LabTrack__Monitoring__MetricsToken`. Use a unique bootstrap password and a random metrics token; do not commit them.

## Run the tests

Requires the .NET 10 SDK and Docker Desktop or Docker Engine. API integration tests start an isolated PostgreSQL container. From this directory:

```bash
dotnet test tests/LabTrack.Tests/LabTrack.Tests.csproj
```

To build the API without Docker (build the React assets first if you need to serve the UI):

```bash
dotnet build src/LabTrack.Api/LabTrack.Api.csproj
```

## Accounts and roles

LabTrack uses ASP.NET Core Identity with HTTP-only, same-site authentication cookies, lockout after repeated failed logins, and 12-character minimum passwords with mixed character classes.

The app also sets a restrictive same-origin Content Security Policy and standard browser security headers. Compose is intended for local use over HTTP; any non-local deployment must terminate TLS at a trusted HTTPS endpoint. Production cookies are marked `Secure`.

- `LabTechnician` can read and process samples.
- `LabAdmin` can do everything a technician can and manage accounts.
- Public account registration is disabled. The bootstrap administrator comes from environment variables; administrators create accounts with `POST /api/auth/users`.

Sample endpoints require an authenticated staff role. `/api/health`, `/metrics` (bearer-token protected), and the development-only OpenAPI document are separate.

## Migrations and audit protection

EF Core migrations are applied on app startup. Migration authoring uses the pinned local EF tool:

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> --project src/LabTrack.Api/LabTrack.Api.csproj
```

The first migration captures the original sample schema; the next creates Identity tables and the audit triggers. Existing databases created by the earlier `EnsureCreated` demo are detected and baselined before later migrations run. Back up existing data before deploying schema changes.

PostgreSQL triggers reject `UPDATE`, `DELETE`, and `TRUNCATE` operations against sample events. The same SQL is available at [ops/postgres/protect-audit.sql](ops/postgres/protect-audit.sql). These triggers protect against ordinary SQL changes, not a database owner or superuser who can change triggers or privileges. A production deployment should use a separate least-privilege app role and an independently retained audit copy.

## Backup, restore, and monitoring

Run backups from PowerShell while the Compose database is running:

```powershell
./ops/backup.ps1
./ops/backup.ps1 -BackupPath ./backups/before-upgrade.dump
./ops/restore.ps1 -BackupPath ./backups/before-upgrade.dump
```

Restore prompts for the exact word `RESTORE`; `-Force` skips the prompt and should only be used deliberately. Backups are manual files on the local machine—schedule them, encrypt them, move copies off the host, and test restores for any real deployment.

Prometheus scrapes authenticated application metrics and PostgreSQL exporter metrics. Grafana provisions the LabTrack overview dashboard automatically. The Compose ports for the app, PostgreSQL, Grafana, and Prometheus bind to localhost; keep them behind appropriate access controls if the deployment changes.

## API overview

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/health` | Application and database health |
| `GET` | `/api/samples?search=&status=` | Search and filter the latest 100 samples |
| `POST` | `/api/samples` | Register a specimen |
| `GET` | `/api/samples/{id}` | Read sample, results, and event history |
| `PATCH` | `/api/samples/{id}/status` | Make a validated lifecycle transition |
| `POST` | `/api/samples/{id}/results` | Record a result while processing is in progress |
| `GET` | `/api/auth/users?skip=0&take=50` | List a bounded page of accounts (administrator only; `take` is 1–100) |

Mutation requests include `expectedRevision`. The server returns `409 Conflict` if the sample has changed since it was read. Accession numbers and analyte codes are unique within their intended scope at both service and database level.

The API is intentionally unversioned while it is a single-deployment personal project; UI and API changes ship together. If independent external clients are introduced, add a versioned contract and compatibility policy before publishing breaking changes.

## Design notes

`SampleService` owns the workflow decisions and persistence operations, while `SampleWorkflow` contains the small, deterministic lifecycle and reference-flag rules. The API never accepts an arbitrary status field in sample creation, and a sample cannot be completed without at least one result. Result and status changes append an event in the same EF Core save operation as the corresponding data change. `LabTrackDbContext` extends the Identity EF context, and policy-protected controllers separate staff operations from administrator-only account management.

The React UI is built into static assets and served by ASP.NET Core, so one process hosts the browser and API. Docker Compose provisions the database, app, Prometheus, and Grafana together.

## Suggested demo path

1. Open `LAB-2026-0041` and start processing.
2. Record an analyte and reference range, such as CRP with a value of `12` and limits `0`–`5`.
3. Observe the calculated `High` flag and the audit event.
4. Complete the sample and confirm the final state.
5. Register a second sample using a duplicate accession number to see conflict handling.

## Project structure

```text
src/LabTrack.Api/
  Application/       workflow service interfaces and implementations
  Contracts/         request and response records
  Controllers/       HTTP endpoints
  Domain/            sample, result, event, and workflow rules
  Infrastructure/    EF Core migrations, Identity, and demo seed data
  Middleware/        API exception-to-Problem-Details handling
  ClientApp/         React/TypeScript/Vite source, unit tests, and Playwright workflow test
ops/                 audit SQL, backups, Prometheus, and Grafana configuration

tests/LabTrack.Tests/ unit tests
.github/workflows/    build and test automation
```
