# Backend architecture

The API uses a service-layer pattern with dependency injection.

```text
HTTP request -> Controller -> Feature service -> ApplicationDbContext -> SQL Server
                    |
                    +-- maps service outcomes into HTTP responses
```

## Responsibilities

- **Controllers** retain routes, model binding and authorization attributes. Each action calls an injected service and maps its result to the existing HTTP response.
- **Services** own feature validation, data queries, business rules and transactions. There is one service/interface per feature: authentication, employees, departments, designations, attendance, leave requests, leave types and dashboard.
- **ApplicationDbContext** owns EF Core persistence. A generic repository/unit-of-work wrapper is unnecessary here: DbContext already tracks changes and commits them, and DbSet provides query access. Keeping EF Core in services also preserves the existing transaction boundaries.
- **ServiceResult** represents success, creation, invalid input, missing data, conflicts and authentication/access failures. It does not reference MVC or HTTP status codes. Controllers map these outcomes through `ServiceResultExtensions`; created employees retain their `GetEmployee` location response.
- **ICurrentUser** exposes the authenticated subject, role membership and session version. `HttpCurrentUser` is the web adapter. Services use it to enforce ownership and prevent self-review; caller IDs do not come from request payloads.
- **ITokenGenerator / JwtTokenGenerator** isolate JWT creation from authentication business logic.
- **LeaveBalanceCalculator** retains inclusive calendar-day calculations across years.

Services are scoped so they share the request's scoped DbContext. `AddHrmsServices` is the dependency-injection registration entry point in `Program.cs`.

## Compatibility

This refactor preserves the existing 39 routes, role attributes, request models, response payloads, error messages, password/session behavior and serializable transaction boundaries. No database migration is required. API callers and the frontend do not require changes.

Role admission remains at the HTTP boundary. Services are intended to be invoked by authorized controllers; a future background job must establish its own trusted access context. Service-level ownership checks remain in place.

Existing response projections use anonymous objects and `ServiceResult.Value` is an object to preserve the current JSON contracts. Dedicated typed response DTOs are a possible separate improvement. The refactor does not add CQRS, MediatR or a multi-project clean-architecture structure.

## Tests

From the backend repository root:

```powershell
dotnet test HRMS.API.Tests/HRMS.API.Tests.csproj --configuration Release
dotnet build HRMS.API.sln --configuration Release
```

The suite includes 39 route/role contract cases and regression tests for leave allowances, overlap and self-review, year boundaries, attendance ownership, onboarding rollback, password/session changes, safe employee responses, duplicate names, HTTP outcome mapping and dependency injection.

Tests use an isolated in-memory **SQLite relational database**, never the application's connection string or production database. SQLite verifies ordinary persistence and rollback behavior; it does not prove SQL Server-specific collation, locking or concurrent serializable transaction behavior. A SQL Server integration suite and authenticated HTTP smoke tests remain worthwhile follow-up work.

The Azure pipeline runs the suite before publishing the deployment artifact and publishes TRX results. Existing production deployment conditions remain unchanged.
