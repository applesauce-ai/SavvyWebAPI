# Savvy Ltd Backend Assessment — Project Plan

Stack: C# / .NET 8 / ASP.NET Core Web API / SQL Server / EF Core

## 1. Assumptions to state up front (in SOLUTION.md)

The brief leaves a few things open — call these out explicitly rather than guessing silently:

- **Fee rules source**: "Fee rules for the period" aren't tied to a specific entity. Treat them as parameters supplied on the payment run request (percentage fee + optional fixed fee per timesheet), not a separate config table. Note this as a deliberate simplification; a real system would likely version fee rules per practice/period.
- **Practice-Shift-Timesheet scoping**: Clinicians only see/submit timesheets for shifts assigned to them. PracticeManagers are scoped to their own practice (need a `PracticeId` on the PracticeManager/Clinician user record).
- **Shift → Clinician assignment**: shifts need an assigned clinician (nullable until claimed, or assigned at creation) so "shifts they worked" is enforceable.
- **Idempotency semantics**: "handle conflicts safely without creating duplicates" = treat a repeated business reference as a no-op that returns the original record/result (200 with existing resource), not a 409, unless the payload materially differs (then 409 Conflict with a clear message).

## 2. Architecture

Keep it simple and defensible for a senior take-home — avoid over-engineering:

```
/src
  Savvy.Api            → Controllers, middleware, auth, DI wiring, Program.cs
  Savvy.Application     → Services/use-cases, DTOs, validation, interfaces
  Savvy.Domain          → Entities, enums, domain logic (hours calc, rounding)
  Savvy.Infrastructure   → EF Core DbContext, repositories, migrations, Identity
/tests
  Savvy.UnitTests       → domain calculation logic (hours, rounding, fees)
  Savvy.IntegrationTests → WebApplicationFactory + in-memory/SQL Server test DB
```

Layered, not full Clean Architecture ceremony — enough separation to show judgment without gold-plating a take-home.

## 3. Data model

Primary keys are `int` (auto-increment) across the board. `Users`, `Timesheets`, and `PaymentRuns` additionally carry a separate `PublicId` (`GUID`, `uniqueidentifier`, unique, generated `Guid.NewGuid()` at creation) — these three are the tables that hold identity or financial data and are addressed directly via API routes (`/api/timesheets/{id}`, `/api/payment-runs/{id}`), so they get the non-guessable external identifier. Internal FKs (e.g. `Shifts.ClinicianId`, `PaymentRunLineItems.TimesheetId`) still reference the int `Id` for efficient joins/indexing — `PublicId` is purely the external-facing lookup key, used as the JWT `sub` claim for users and the route parameter for timesheets/payment runs. `Roles`, `Practices`, `Shifts`, and `PaymentRunLineItems` keep plain int PKs — they're either reference/lookup data (`Roles`) or not the primary sensitive record being protected (a `Shift` alone has no financial figures attached; `PaymentRunLineItems` are only ever read as part of their parent payment run, never addressed directly).

| Table | Key fields |
|---|---|
| `Roles` | Id (int), Name (unique — "Admin", "PracticeManager", "Clinician") |
| `Practices` | Id (int), Name |
| `Users` | Id (int, internal PK), PublicId (GUID, unique, external-facing), Email (unique), PasswordHash, RoleId (FK → Roles), PracticeId (nullable for Admin) |
| `Shifts` | Id (int), PracticeId, ClinicianId, Date, StartUtc, EndUtc, HourlyRate, Role, Location, Status (Open/Completed) |
| `Timesheets` | Id (int, internal PK), PublicId (GUID, unique, external-facing), ShiftId (unique), ClinicianId, WorkedStartUtc, WorkedEndUtc, UnpaidBreakMinutes, Notes, BusinessReference (unique), CreatedAtUtc |
| `PaymentRuns` | Id (int, internal PK), PublicId (GUID, unique, external-facing), PracticeId, PeriodStartUtc, PeriodEndUtc, FeePercentage, FixedFeePerTimesheet, BusinessReference (unique), Currency, GrossTotal, FeeTotal, NetTotal, CreatedAtUtc |
| `PaymentRunLineItems` | Id (int), PaymentRunId, TimesheetId, ClinicianId, Hours, Rate, Gross, Fee, Net |

**Why a `Roles` table instead of an enum/string on `Users`:**
- New roles (e.g. future "Auditor" or "Billing" role) are a data insert, not a code change + redeploy.
- `[Authorize(Roles = "...")]` still works fine against a DB-backed role — the JWT's role claim is populated from `Roles.Name` at login, so the attribute-based checks in ASP.NET Core don't need to change.
- Keep it simple: a straight `Users.RoleId → Roles.Id` (one role per user) is enough for this brief — no need for a many-to-many `UserRoles` join table unless multi-role users become a real requirement. Worth a one-line note in SOLUTION.md that this was a deliberate scope call.
- Seed `Roles` first (Admin, PracticeManager, Clinician), then seed `Users` referencing those rows.

Constraints worth calling out in the plan:
- Unique index on `Timesheets.ShiftId` → enforces exactly one timesheet per shift at the DB level, not just app logic.
- Unique index on `Timesheets.BusinessReference` and `PaymentRuns.BusinessReference` → backstop for idempotency even under concurrent requests.
- Unique index on `Roles.Name`, `Users.Email`, `Users.PublicId`, `Timesheets.PublicId`, and `PaymentRuns.PublicId`.
- FK cascade rules: restrict deletes (financial data shouldn't cascade-delete silently).

## 4. API surface

| Endpoint | Method | Role | Notes |
|---|---|---|---|
| `/api/auth/login` | POST | public | returns JWT |
| `/api/practices` | GET | Admin | list practices |
| `/api/practices/{id}/shifts` | GET | Admin, PracticeManager | scoped to own practice |
| `/api/practices/{id}/shifts` | POST | Admin, PracticeManager | create shift |
| `/api/shifts/{id}` | PUT | Admin, PracticeManager | update shift |
| `/api/shifts/{id}/timesheets` | POST | Clinician | submit timesheet, idempotent via reference |
| `/api/timesheets/{publicId}` | GET | Admin, PracticeManager, owning Clinician | `{publicId}` = Timesheet.PublicId (GUID) |
| `/api/practices/{id}/payment-runs` | POST | Admin, PracticeManager | idempotent via reference; body = date range + fee rules |
| `/api/payment-runs/{publicId}` | GET | Admin, PracticeManager | `{publicId}` = PaymentRun.PublicId (GUID); summary + line items |

Error responses: consistent `ProblemDetails` (RFC 7807) shape via a global exception-handling middleware — no stack traces, no internal exception messages leaked, just a stable error code + message.

## 5. Implementation order (Task 1)

1. Solution scaffold, EF Core DbContext, migrations, connection string via config.
2. Entities + relationships + constraints (unique indexes above).
3. Seed data: 3 roles (Admin/PracticeManager/Clinician), 1 practice, 3 users (one per role), 2+ shifts (one open, one ready to be timesheeted).
4. Shift CRUD (create/list/update) scoped by practice.
5. Timesheet submission endpoint:
   - validate shift belongs to clinician and is Open
   - compute hours = (WorkedEnd − WorkedStart − breaks), 2dp
   - on duplicate `BusinessReference`: return existing record, don't insert
   - on submit: flip shift status to Completed
6. Payment run endpoint:
   - pull all timesheets for practice within UTC-inclusive date range
   - per line: gross = hours × rate; fee = gross × pct + fixed; net = gross − fee (rounding: `MidpointRounding.AwayFromZero` at 2dp, applied at the line level then summed — decide and document this explicitly since it affects totals)
   - on duplicate `BusinessReference`: return existing payment run, don't recompute/duplicate
7. Unit tests for the calculation logic (hours, rounding edge cases like exact .005, fee application order).

## 6. Task 2 — security & config

- **Auth**: JWT bearer, seeded users with `PasswordHasher<T>` (ASP.NET Core Identity's hasher, PBKDF2) — no plaintext, no custom crypto.
- **Authorization**: `[Authorize(Roles = "...")]` per endpoint plus manual practice-scoping checks in the service layer (role alone isn't enough — a PracticeManager must not see another practice's data).
- **Secrets**: local dev via `dotnet user-secrets` / `appsettings.Development.json` (gitignored), never committed. Document `appsettings.json` holds only non-secret defaults.
- **Azure deployment** (for SOLUTION.md):
  - App Service (Linux, .NET 8 runtime) hosting the API
  - Azure SQL Database for persistence
  - Secrets (JWT signing key, SQL connection string) in **Azure Key Vault**, referenced from App Service via **Key Vault references** in Application Settings (`@Microsoft.KeyVault(...)`), or pulled at startup via `Azure.Identity` + **Managed Identity** (no credentials stored anywhere)
  - App Service Configuration → environment-specific settings (`ASPNETCORE_ENVIRONMENT=Production`, connection strings under "Connection Strings" blade, not appsettings)
  - CI/CD: GitHub Actions → build, test, `dotnet publish`, deploy via `azure/webapps-deploy`
  - App Service enforces HTTPS-only, TLS 1.2+; enable diagnostic logging to App Insights, no sensitive data in logs

## 7. Error handling & data protection (SOLUTION.md talking points)

- Global exception middleware → generic `ProblemDetails` for 500s, specific validation errors for 400s, no internal details exposed.
- PII/financial fields not logged; structured logging with field redaction where needed.
- All times persisted/compared in UTC; date-range filters use UTC day boundaries, inclusive both ends (`>= startOfDayUtc AND < endOfNextDayUtc`).
- Idempotency keys prevent duplicate financial records under retry/network-failure conditions — call this out as a data-integrity feature, not just a REST nicety.
- **Considered and rejected**: moving resource identifiers out of the URL and into the JSON body for GET requests (to reduce ID exposure in access logs). Rejected because GET-with-body isn't reliably supported across HTTP clients/proxies/OpenAPI tooling, and it doesn't close a real gap — the `PublicId` GUIDs are already non-enumerable wherever they sit, and the actual IDOR defense is per-request authorization scoping (role + practice/clinician ownership checks), not where the identifier appears in the request. Kept IDs as conventional route parameters.

## 8. Suggested time allocation

| Phase | Focus |
|---|---|
| 1 | Scaffold + data model + migrations + seed data |
| 2 | Shifts + timesheets endpoints incl. idempotency + hours calc |
| 3 | Payment run endpoint incl. fee calc + rounding + idempotency |
| 4 | Auth (JWT + seeded users) + role/practice-scoped authorization |
| 5 | Error handling middleware, config/secrets cleanup, Azure config docs |
| 6 | Unit tests for calculations, quick integration test of happy path, write SOLUTION.md |

## 9. Verification before submitting

- Run full happy path via Postman/curl script: login as each role → create shift → submit timesheet → run payment run → confirm totals match manual calculation.
- Re-submit same timesheet/payment-run reference → confirm no duplicate rows, same response returned.
- Attempt cross-practice access as PracticeManager → confirm 403.
- Confirm no secrets in repo (`git grep` for connection strings/keys).
- Confirm rounding on a deliberately exact-.005 test case.
