<!-- If you named the repo something other than "SavvyWebAPI", update the badge URL below. -->
# Savvy Ltd — Backend API

[![CI](https://github.com/applesauce-ai/SavvyWebAPI/actions/workflows/ci.yml/badge.svg)](https://github.com/applesauce-ai/SavvyWebAPI/actions/workflows/ci.yml)

A .NET 8 / ASP.NET Core Web API for managing practices, shifts, timesheets and payment runs —
backed by SQL Server via EF Core, secured with JWT, and monitored by a Discord/Teams alerting
watchdog.

> Full design write-up and rationale: **[SOLUTION.md](SOLUTION.md)**. Original brief/plan:
> **[PROJECT_PLAN.md](PROJECT_PLAN.md)**.

## Features

- **Practices, shifts, timesheets, payment runs** with role- and practice-scoped authorization.
- **JWT auth** (ASP.NET Core Identity `PasswordHasher`, PBKDF2) with an `/api/auth/login` endpoint.
- **Idempotent** timesheet and payment-run submission (safe under retries; unique-index backstop).
- **Money/time correctness** — hours and fees computed with documented away-from-zero rounding.
- **Secrets via vault** — Azure Key Vault in production, a faithful local mock in dev; nothing secret in source.
- **Health endpoints** + a standalone **watchdog** that alerts to Discord (Teams-ready) on outage/recovery.
- **Business-event notifications** (Discord/Teams) when timesheets and payment runs are logged.
- **51 unit + integration tests**; CI on every push/PR.

## Tech stack

.NET 8 · ASP.NET Core Web API · EF Core 8 (SQL Server) · JWT bearer · xUnit · SQLite (test DB) ·
GitHub Actions.

## Solution layout

```
src/
  Savvy.Domain          Entities, enums, pure calculations (hours, fees)
  Savvy.Application     Use-cases, DTOs, validation, interfaces
  Savvy.Infrastructure  EF Core DbContext, configs, migrations, seeder, notifications
  Savvy.Api             Controllers, middleware, JWT auth, Key Vault, health endpoints
  Savvy.Watchdog        Standalone worker: polls health, alerts to Discord/Teams
tests/
  Savvy.UnitTests           Calculation + watchdog/notifier logic
  Savvy.IntegrationTests    WebApplicationFactory end-to-end (SQLite), incl. real-JWT happy path
db/
  schema.sql   seed.sql     Raw DDL + mockup data (mirrors the EF model/seeder)
postman/                     Ready-to-run Postman collection
```

## Getting started

**Prerequisites:** .NET 8 SDK, a local SQL Server instance, and a SQL login for the app.

1. **Create the database + login** (once, as a SQL sysadmin):
   ```sql
   CREATE LOGIN [savvy_app] WITH PASSWORD = '<choose-one>';
   CREATE DATABASE [SavvyDb];
   USE [SavvyDb];
   CREATE USER [savvy_app] FOR LOGIN [savvy_app];
   ALTER ROLE db_owner ADD MEMBER [savvy_app];
   ```

2. **Provide secrets** via the local mock vault (dev stand-in for Azure Key Vault):
   ```bash
   cp src/Savvy.Api/keyvault.mock.example.json src/Savvy.Api/keyvault.mock.json
   # fill in ConnectionStrings--SavvyDb (with the savvy_app password) and Jwt--SigningKey
   ```

3. **Run** (applies EF migrations + seeds demo data in Development):
   ```bash
   dotnet run --project src/Savvy.Api
   ```
   Swagger UI: `https://localhost:7011/swagger` (or `http://localhost:5064/swagger`).
   Click **Authorize** and paste a token from `/api/auth/login`.

Alternatively, build the schema/data by hand:
```bash
sqlcmd -S localhost -E -d SavvyDb -i db/schema.sql
sqlcmd -S localhost -E -d SavvyDb -i db/seed.sql
```

## API

| Method | Endpoint | Roles |
|---|---|---|
| POST | `/api/auth/login` | anonymous |
| GET / POST | `/api/practices/{id}/shifts` | Admin, PracticeManager |
| PUT | `/api/shifts/{id}` | Admin, PracticeManager |
| POST | `/api/shifts/{id}/timesheets` | Clinician |
| GET | `/api/timesheets/{publicId}` | Admin, PracticeManager, owning Clinician |
| POST | `/api/practices/{id}/payment-runs` | Admin, PracticeManager |
| GET | `/api/payment-runs/{publicId}` | Admin, PracticeManager |
| GET | `/health/live`, `/health/ready` | anonymous |

A [Postman collection](postman/Savvy.postman_collection.json) drives the whole flow (login →
create shift → submit timesheet → run payment) with automatic token/id chaining.

### Demo credentials (local/dev only)

| Email | Password | Role |
|---|---|---|
| admin@savvy.test | `Admin#12345` | Admin |
| manager@savvy.test | `Manager#12345` | PracticeManager |
| clinician@savvy.test | `Clinician#12345` | Clinician |

## Monitoring & notifications

- **Health:** `/health/live` (liveness) and `/health/ready` (liveness + DB check).
- **Watchdog:** `dotnet run --project src/Savvy.Watchdog` — polls `/health/ready` and posts to a
  Discord webhook on outage/recovery (and announces its own start/graceful stop). Swaps to a Teams
  webhook via `Notifications:Provider`. In Azure this role is played by Azure Monitor.
- **Business events:** new timesheets and payment runs post a summary to the configured chat channel.

Webhook URLs are secrets (vault / user-secrets), never committed.

## Testing

```bash
dotnet test
```
Unit tests cover the calculation and watchdog logic; integration tests exercise the API end-to-end
over an **in-memory SQLite** database (so they need no SQL Server or secrets — the same reason CI
runs them on a clean runner).

## Branches

`main` is the integration branch; `staging`, `uat`, and `prod` are the deployment branches.
CI (build + test) runs on every push and pull request to all four.
