# FriendPeanut Backend — Sprint 1

Target: **.NET 10 LTS** · ASP.NET Core · EF Core 10 · Npgsql · PostgreSQL 17.

## Scope

- **S0**: solution skeleton, `Directory.Build.props`, `Directory.Packages.props`.
- **S1**: **UI-01..09**, **AUTH-01..11**, **MD-01..13**.
- Policy-gated features return `POLICY_PENDING` — never a fake success:
  AUTH-09 (factory operators), AUTH-10 (main-user matrix), MD-06 (Rojekt grades), MD-10 (packaging units).
- Not enabled: INV-01..20, F1-*, F2-*, AGR-*, FIN-*, PS-*, REP-*, SYS-*, OPS-*.

## Run

    dotnet restore
    dotnet build
    dotnet test
    dotnet run --project src/FriendPeanut.WebApi

`GET /api/v1/health/live` returns `{ "status": "healthy" }` — no DB needed.
`GET /api/v1/health/ready` returns `{ "status": "ready" }` — requires PostgreSQL reachable.

## Database

The four SQL files you provided in `backend/database/` are the authoritative schema.
Apply them with DBA supervision; the script **does not** rewrite, convert or invent DDL.
After applying them, `app_user` must be granted per `004_permissions.example.sql`.

Connection: set `ConnectionStrings:Default` in `appsettings.json`.
The password shown (`CHANGE_ME`) must be replaced before any run that touches PostgreSQL.

## Decisions enforced

| Decision | Where |
|---|---|
| .NET 10 LTS | `Directory.Build.props` |
| Domain has no NuGet | `Directory.Build.props` (`PackageReference Remove="*"` for Domain) |
| Cookie + CSRF | `AuthEndpoints` + `Program.cs` `OnValidatePrincipal` re-checks `auth_sessions` on every request |
| PBKDF2 (ASP.NET Identity) | `AspNetCorePasswordHasher` — ADR-019 |
| TOTP MFA + replay prevention | `TotpService` + `VerifyMfaHandler` rejects reused time steps |
| Decimal-only | `Money`, `Quantity`, Npgsql `numeric` |
| No fake success | `ProblemDetailsMiddleware` never returns 200 on failure |
| Arabic `messageAr` | Every `ProblemDetails` response |
| Policy gates fail closed | `HoldRojektGrades`, `HoldPendingUnits`, `AssignFactoryOperators`, `ConfigureMainUsers` |
| xUnit v2 | Test `.csproj` pinned to 2.9.2 (SDK templates default to v3) |

## What is not here yet (honest)

- No `PostStockDocument` / `PostingService` — Sprint 2 (`stock_ledger_entries`, `posting_batches` unwritten).
- No `IIdempotencyStore` implementation against PostgreSQL — port exists; table exists; implementation is Sprint 2.
- No `MFA_VERIFY` → `fp_session` upgrade — Sprint 1 does not require it; wired in Sprint 2.
- No chart-of-accounts, valuation, allocation, tax rules — all gated in `policy_decisions` as `PENDING`.
- No `INV-01` opening-balance document — Sprint 2, and its gate `opening_approval` is `PENDING`.
- All source is **PLANNED** per `DEFINITION_OF_DONE_EN.md`; it becomes *Done* only after the schema is applied in staging, acceptance tests recorded per AT-ID, and a second engineer reviews.

## Known NuGet pins

`Microsoft.EntityFrameworkCore 10.0.0`, `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0`,
`FluentValidation 11.11.0`, `Serilog.AspNetCore 9.0.0`, `xunit 2.9.2`.
If the restore output shows a newer patch (e.g. `10.0.1`, `11.11.1`) that is acceptable — bump
`Directory.Packages.props` and record the change in a commit message.

`Testcontainers.PostgreSql 4.1.0` is present for Sprint 2 integration tests; not used in Sprint 1.
## Sprint 1 — DoD status per feature (2026-11-19)

No feature is fully "Done" per DEFINITION_OF_DONE_EN.md — staging evidence, peer review,
CI pipeline, and per-AT acceptance records are process gates not achievable within this session.

Compiled and tested: every S1 feature (AUTH-01..11, MD-01..13, UI-01..09 where backend-relevant)
compiles, has a reachable endpoint, and is covered by the test suite to the extent achievable
without PostgreSQL.

Policy-gated (422 POLICY_PENDING until management decides):
AUTH-09, AUTH-10, MD-06, MD-10, INV-01 (Sprint 2).

Explicitly NOT done: INV-01..20, F1-*, F2-*, AGR-*, FIN-*, PS-*, REP-*, SYS-*, OPS-*;
real PostgreSQL store integration tests; peer review; staging verification; per-AT acceptance records.
