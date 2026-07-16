# Savvy Ltd Backend — Solution

A .NET 8 / ASP.NET Core Web API for managing practices, shifts, timesheets and payment runs,
backed by SQL Server via EF Core.

---

## 1. Tech stack

- **.NET 8** / ASP.NET Core Web API (controllers), pinned via [`global.json`](global.json)
- **EF Core 8** (SQL Server provider), code-first migrations
- **SQL Server 2022** (local default instance)
- **JWT bearer** auth with ASP.NET Core Identity's `PasswordHasher` (PBKDF2)
- **xUnit** unit + integration tests (integration tests use SQLite in-memory)

## 2. Architecture

Layered — enough separation to show intent without Clean-Architecture ceremony:

```
src/
  Savvy.Domain          Entities, enums, pure calculations (hours, fees). No dependencies.
  Savvy.Application     Use-case services, DTOs, validation, interfaces (ISavvyDbContext,
                        ICurrentUserContext, ITokenService). Depends on Domain.
  Savvy.Infrastructure  EF Core DbContext, entity configs, migrations, seeder. Implements the
                        Application abstractions. Depends on Application + Domain.
  Savvy.Api             Controllers, middleware, JWT auth, DI wiring, Program.cs, Key Vault.
tests/
  Savvy.UnitTests       Calculation logic (hours, fee rounding edge cases).
  Savvy.IntegrationTests  WebApplicationFactory end-to-end tests (SQLite), incl. real-JWT happy path.
```

The Application layer talks to persistence through `ISavvyDbContext` (implemented by
`SavvyDbContext`) so use-cases don't bind to the EF provider and are unit-testable.

## 3. Running locally

**Prerequisites:** .NET 8 SDK, a local SQL Server instance, the `savvy_app` SQL login.

The database and login were provisioned once (see [`db/`](db) and §5). To reproduce on a fresh
machine:

```sql
-- as a sysadmin (Windows auth)
CREATE LOGIN [savvy_app] WITH PASSWORD = '<choose-one>';
CREATE DATABASE [SavvyDb];
USE [SavvyDb];
CREATE USER [savvy_app] FOR LOGIN [savvy_app];
ALTER ROLE db_owner ADD MEMBER [savvy_app];
```

Then put the app's secrets in the mock vault (dev stand-in for Azure Key Vault):

```
cp src/Savvy.Api/keyvault.mock.example.json src/Savvy.Api/keyvault.mock.json
# fill in ConnectionStrings--SavvyDb (with the savvy_app password) and Jwt--SigningKey
```

Run:

```
dotnet run --project src/Savvy.Api      # applies migrations + seeds demo data in Development
```

Swagger UI is at `/swagger`. Click **Authorize** and paste a token from `/api/auth/login`.

Alternatively, create the schema/data by hand with the raw scripts:

```
sqlcmd -S localhost -E -d SavvyDb -i db/schema.sql   # tables + indexes
sqlcmd -S localhost -E -d SavvyDb -i db/seed.sql     # mockup data
```

**Tests:** `dotnet test` (33 tests: 9 unit + 24 integration).

## 4. Demo credentials (local/dev only)

Seeded by [`SavvySeeder`](src/Savvy.Infrastructure/Persistence/SavvySeeder.cs) and
[`db/seed.sql`](db/seed.sql). Production users are provisioned separately — these are not real.

| Email | Password | Role | Practice |
|---|---|---|---|
| admin@savvy.test | `Admin#12345` | Admin | — |
| manager@savvy.test | `Manager#12345` | PracticeManager | Savvy Medical Practice |
| clinician@savvy.test | `Clinician#12345` | Clinician | Savvy Medical Practice |

## 5. Configuration & secrets

Precedence (lowest → highest): `appsettings.json` → `appsettings.{env}.json` → environment
variables → **vault** (added last, wins).

- **`appsettings.json`** holds only non-secrets: a Windows-auth `SavvyDb` fallback, JWT
  issuer/audience/expiry, Key Vault switches.
- **Secrets** (`ConnectionStrings--SavvyDb` with the SQL password, `Jwt--SigningKey`) live in the
  **vault**:
  - **Production** → real **Azure Key Vault** (`KeyVault:Uri` set by App Service), read via
    `DefaultAzureCredential` → **Managed Identity**. No credentials stored on the host.
  - **Local dev** → a **mock vault** ([`keyvault.mock.json`](src/Savvy.Api/keyvault.mock.example.json),
    gitignored) that faithfully mimics Key Vault, including its `--` → `:` secret-name translation
    (`ConnectionStrings--SavvyDb` → `ConnectionStrings:SavvyDb`). The app consumes secrets
    identically in both cases; only the source swaps.

**Privilege separation:** the app runs as the SQL login `savvy_app`; EF **migrations** run as the
developer/CI identity (Windows auth), not the app. For production the app identity should be
narrowed to `db_datareader` + `db_datawriter` + `EXECUTE`, with migrations applied by a separate
higher-privilege identity in the deployment pipeline. `db_owner` is a **local-dev convenience**.

## 6. Data model

Int PKs everywhere for efficient FK joins. `Users`, `Timesheets` and `PaymentRuns` additionally
carry a `PublicId` (GUID) — the non-guessable external identifier used in routes and as the JWT
`sub`. `Roles` is a table (not an enum) so new roles are a data insert, not a redeploy.

DB-level guarantees (see [`db/schema.sql`](db/schema.sql)):
- Unique: `Roles.Name`, `Users.Email`, `Users.PublicId`, `Timesheets.PublicId`,
  `Timesheets.BusinessReference`, `PaymentRuns.PublicId`, `PaymentRuns.BusinessReference`.
- **Unique `Timesheets.ShiftId`** — exactly one timesheet per shift, enforced by the DB.
- All FKs are `NO ACTION` (restrict) — financial data never cascade-deletes silently.

## 7. API surface

| Endpoint | Method | Roles | Notes |
|---|---|---|---|
| `/api/auth/login` | POST | anonymous | email + password → JWT |
| `/api/practices/{id}/shifts` | GET | Admin, PracticeManager | practice-scoped |
| `/api/practices/{id}/shifts` | POST | Admin, PracticeManager | create shift |
| `/api/shifts/{id}` | PUT | Admin, PracticeManager | update shift |
| `/api/shifts/{id}/timesheets` | POST | Clinician | idempotent; 201 new / 200 replay |
| `/api/timesheets/{publicId}` | GET | Admin, PracticeManager, owning Clinician | |
| `/api/practices/{id}/payment-runs` | POST | Admin, PracticeManager | idempotent; 201 / 200 |
| `/api/payment-runs/{publicId}` | GET | Admin, PracticeManager | summary + line items |

**Authorization is two-layered:** `[Authorize(Roles=…)]` gates by role, and the service layer
enforces ownership/practice scoping (a PracticeManager can't reach another practice; a Clinician
can only timesheet/view their own). Role alone is not sufficient.

## 8. Calculations & rounding

Pure, unit-tested domain logic:
- **Hours** ([`WorkHours`](src/Savvy.Domain/Calculations/WorkHours.cs)) =
  `(end − start − unpaidBreak)`, 2dp, `MidpointRounding.AwayFromZero`.
- **Fees** ([`FeeCalculation`](src/Savvy.Domain/Calculations/FeeCalculation.cs)) per line:
  `gross = hours × rate`; `fee = gross × feePercentage + fixedFee`; `net = gross − fee`.
  **Rounding is applied at the line level (2dp away-from-zero), then the run totals are the sum of
  the rounded lines** — this is a deliberate, documented choice because where you round changes the
  totals.

Worked example (proven in tests): 7.50h × £25 = £187.50; fee = 187.50 × 0.15 + 5.00 = 33.125 →
**£33.13**; net **£154.37**.

## 9. Idempotency

Timesheet submission and payment runs are idempotent on a caller-supplied `BusinessReference`:
- Same reference + same payload → returns the original resource (**200**), no duplicate.
- Same reference + materially different payload → **409 Conflict**.
- New reference → **201 Created**.

The unique indexes on `BusinessReference` (and `Timesheets.ShiftId`) are the real backstop: a
concurrent double-submit that races past the pre-check hits the constraint, and the resulting
`DbUpdateException` is resolved by re-reading and returning the winner idempotently. This is a
data-integrity feature (no duplicate financial records under retry), not just REST etiquette.

## 10. Error handling & data protection

- Global [`ExceptionHandlingMiddleware`](src/Savvy.Api/Middleware/ExceptionHandlingMiddleware.cs)
  → RFC 7807 `ProblemDetails`. Known app exceptions map to 400/401/403/404/409; anything else is a
  generic **500 with no internal detail leaked** and is logged with a `traceId` for correlation.
- Login failures are uniform ("Invalid email or password") regardless of whether the email exists.
- All times are UTC; payment-run date filters are inclusive on `WorkedStartUtc`.
- No secrets or PII in logs; secrets never leave the vault.

## 10a. Monitoring & alerting

**Health endpoints** (anonymous):
- `GET /health/live` — liveness (process is up; no dependency checks).
- `GET /health/ready` — readiness (includes an EF Core `DbContext` check that the database is
  reachable). Returns `200` Healthy / `503` Unhealthy with a compact JSON body (no internal detail).

**Local alerting** — [`Savvy.Watchdog`](src/Savvy.Watchdog) is a separate worker that polls
`/health/ready` on an interval and posts to a **Discord webhook** on Down and Recovery transitions.
It runs as its own process on purpose: an in-process check can't report that its own host has
crashed. A failure threshold debounces transient blips, and alerts fire only on state *transitions*
(no repeat spam). The notifier is behind `IAlertNotifier`, so the Discord implementation swaps for a
Teams one with no change to the worker.

It also announces its **own** lifecycle: a "🟢 Watchdog online" message at startup and a
"⚠️ Watchdog offline" message on **graceful** shutdown (Ctrl+C / SIGTERM, via `StopAsync`). A *hard*
kill (Task Manager, `kill -9`, power loss) can't self-report — a process can't announce its own
sudden death. True crash detection needs an **external heartbeat / dead-man's switch** (the watchdog
writes a heartbeat that a separate monitor alerts on if it stops) — which, again, is exactly the role
Azure Monitor plays in production.

Run it alongside the API:
```
dotnet user-secrets set "Watchdog:Discord:WebhookUrl" "https://discord.com/api/webhooks/..." \
  --project src/Savvy.Watchdog
dotnet run --project src/Savvy.Watchdog
```

**In Azure** you wouldn't run the watchdog — the platform provides the external watcher:
- **App Service Health check** pings `/health/ready` and pulls unhealthy instances out of rotation.
- **Azure Monitor availability tests** ping the endpoint from outside; an **alert rule → action
  group** posts to a **Teams** channel (incoming webhook) — the same pattern as the local Discord
  webhook, just a different payload shape (swap `DiscordWebhookNotifier` for a `TeamsWebhookNotifier`).

**Business-event notifications** — new timesheet submissions and payment-run creations post a rich
message to a chat channel via a provider-agnostic `IWebhookNotifier`
([`Savvy.Infrastructure/Notifications`](src/Savvy.Infrastructure/Notifications)):
- `Notifications:Provider` selects **Discord** (local) or **Teams** (Azure); a
  [`TeamsWebhookNotifier`](src/Savvy.Infrastructure/Notifications/TeamsWebhookNotifier.cs) is
  included as a mockup (legacy MessageCard; a production Teams integration would post an Adaptive
  Card to a Workflows URL).
- Fired only on genuinely new records, never on idempotent replays.
- Best-effort: the notifier swallows all failures so a webhook outage can't break a timesheet or
  payment. The call is currently awaited in-request; **in production this should move to an outbox /
  queue** (e.g. Azure Service Bus) so the HTTP call is off the request path.
- Webhook URLs are secrets (`Notifications--Discord--WebhookUrl` / `Notifications--Teams--WebhookUrl`)
  supplied by the vault — never committed.

## 11. Azure deployment (target)

- **App Service** (Linux, .NET 8) hosting the API; HTTPS-only, TLS 1.2+.
- **Azure SQL Database** for persistence.
- **Azure Key Vault** for `Jwt--SigningKey` and the SQL connection string, referenced via the App
  Service **Managed Identity** (`Key Vault Secrets User` role) — set `KeyVault:Uri` in App Service
  configuration and the app loads them at startup. No credentials on the host.
- **CI/CD:** GitHub Actions → build, test, `dotnet publish`, `azure/webapps-deploy`. Migrations
  applied in the pipeline by a higher-privilege identity; the app identity stays least-privilege.
- Diagnostics to Application Insights, no sensitive data in logs.

## 12. Assumptions & deliberate simplifications

- **Fee rules are supplied per payment-run request** (percentage + fixed), not a versioned config
  table. `FeePercentage` is a **fraction** (0.15 = 15%). A real system would likely version fee
  rules per practice/period.
- **One role per user** (`Users.RoleId`), no many-to-many `UserRoles` — sufficient for the brief.
- **Payment runs include all matching timesheets in the period** — there is no "already paid" flag,
  so overlapping runs could double-count. Matches the brief ("pull all timesheets for the period");
  a real system would mark timesheets paid or exclude already-run ones.
- **Shift assignment**: `Shifts.ClinicianId` is nullable (assigned at creation or later); a clinician
  can only timesheet a shift assigned to them.
- **IDs in routes**: `PublicId` GUIDs are used for externally-addressed resources. Moving ids into
  request bodies was considered and rejected — GET-with-body is poorly supported and the real IDOR
  defense is per-request authorization scoping, which is implemented.

## 13. What I'd add next

- Refresh tokens / token revocation; per-endpoint rate limiting.
- Pagination and filtering on list endpoints.
- Mark-timesheets-as-paid to prevent double-counting across payment runs.
- FluentValidation for richer request validation messages.
- More integration coverage around concurrency (parallel idempotent submits).
