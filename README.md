# Gekko Waybills

## 1) Project overview
Gekko Waybills is a multi-tenant .NET 8 Web API for managing waybills, projects, and suppliers. It supports CSV imports (sync and async), optimistic concurrency, caching, and structured logging. EF Core is used for persistence (SQL Server in runtime config, SQLite in integration tests). A minimal React (Vite) UI is included for manual testing.
This project was implemented with a strong focus on correctness, tenant isolation, observability, and testability rather than UI completeness.


## Quick start (5 minutes)
1. `docker compose up -d`
2. `dotnet run --project src/Gekko.Waybills.Api`
3. `cd client && npm install && npm run dev`
4. Open `http://localhost:5003/swagger`
5. Use `X-Tenant-ID` header (e.g. `TENANT001`)

## 2) Architecture overview (with folder structure)
Clean Architecture with four layers:

```
src/
  Gekko.Waybills.Domain/          # Entities, enums, domain rules
  Gekko.Waybills.Application/     # Use cases, import logic, query services
  Gekko.Waybills.Infrastructure/  # EF Core, RabbitMQ, migrations
  Gekko.Waybills.Api/             # Web API, middleware, controllers
client/                           # Minimal React UI (Vite)
tests/
  Gekko.Waybills.Api.Tests/       # Integration tests (WebApplicationFactory)
  Gekko.Waybills.Tests/           # Domain-focused unit tests
```

## 3) Multi-tenancy design
- Tenant is resolved from the HTTP header: `X-Tenant-ID`.
- A scoped `ITenantContext` stores the tenant for the request.
- Global query filters ensure all `ITenantOwned` entities are tenant-isolated.
- Tenant mismatch in CSV rows is rejected (`TENANT_MISMATCH`).

## 4) Database schema documentation (tables + purpose)
Core tables and their purposes:

| Table | Purpose |
|---|---|
| Projects | Master list of projects per tenant |
| Suppliers | Master list of suppliers per tenant |
| Waybills | Waybill records, uniqueness enforced per tenant and number |
| ImportAudits | Audit log of completed imports from RabbitMQ events |
| ImportJobs | Async import job tracking (queued/running/succeeded/failed) |
| ExecutionLocks | DB-backed locks for exclusive operations |

Key columns:
- `Waybills.RowVersion` (byte[]) for optimistic concurrency
- `Waybills.TenantId` for tenant isolation
- `AuditableEntity.IsDeleted` soft delete flag (global filter excludes deleted)

Key indexes:
- `Waybills(TenantId, WaybillNumber)` unique
- `Waybills(TenantId, DeliveryDate / Status / ProjectId / SupplierId)`

Relationships: Projects and Suppliers each have many Waybills (many-to-one), reflected in the EF Core model.

## 5) Business rules and validations
- `Quantity` must be between **0.5** and **50**.
- `DeliveryDate` must be **>= WaybillDate**.
- `TotalAmount` must equal `Quantity * UnitPrice` within **0.01**.
- Status transitions:
  - `PENDING` → `DELIVERED` or `CANCELLED` (or remain `PENDING`)
  - `DELIVERED` → `DISPUTED` (or remain `DELIVERED`)
  - `CANCELLED` is terminal
  - No backward transitions

## 6) Optimistic concurrency handling
- `Waybill.RowVersion` is configured as a concurrency token.
- API responses include `rowVersionBase64`.
- Updates require `rowVersionBase64` from the client.
- Stale row versions return `409 Conflict`.

## 7) Caching strategy and invalidation
- In-memory caching (`IMemoryCache`) for:
  - `/api/waybills/summary`
  - `/api/waybills` list
- Cache key includes `TenantId` + query string + per-tenant version.
- TTL configurable via `Cache:DefaultTtlSeconds`.
- Invalidation increments a per-tenant version on:
  - successful imports (sync and async)
  - successful updates (PUT)
You can validate caching by observing HIT/MISS/INVALIDATED logs and by calling the same GET endpoints repeatedly to compare response behavior before and after updates/imports.

## 8) Logging strategy and observability
Structured logging is used throughout:
- Request logging middleware logs method/path/tenant/status/elapsed time.
- Warnings for validation failures and business rule violations.
- Warnings for concurrency conflicts.
- Errors for unhandled exceptions, async job failures, and RabbitMQ failures.
- Cache HIT/MISS/INVALIDATED logs are emitted with tenant and key.

## 9) CSV import (sync + async jobs)
### Sync import
`POST /api/waybills/import`  
Returns import summary with counts, warnings, and rejected rows.

### Async import
`POST /api/waybills/import?async=true`  
Returns `202 Accepted` with `{ jobId }`.  
Poll: `GET /api/import-jobs/{id}`.
Job statuses:
- QUEUED
- RUNNING
- SUCCEEDED
- FAILED
Async failures mark the job as `FAILED`, persist the error message, and log the exception.

Import events:
- On completion, publishes `WaybillsImported` to RabbitMQ.
- Consumer writes to `ImportAudits`.

### Message Broker Integration
- RabbitMQ publishes a `WaybillsImported` event after CSV imports (sync + async completion).
- A consumer processes the event and writes an audit record to `ImportAudits`.
- This satisfies the assignment requirement "Message Broker Integration".

## 10) API documentation (Swagger)
Swagger UI is available at:
```
http://localhost:5003/swagger
```
The `X-Tenant-ID` header is shown on all endpoints.

## 11) Frontend usage (how to test via UI)
Minimal React UI is provided for manual testing:
- Tenant selection
- CSV import (sync + async polling)
- Waybills list with filters
- Waybill update with optimistic concurrency

## 12) Testing strategy and how to run tests
Two test projects:
- `tests/Gekko.Waybills.Api.Tests` (integration tests with WebApplicationFactory + SQLite)
- `tests/Gekko.Waybills.Tests` (domain unit tests)

Run tests:
```
dotnet test tests/Gekko.Waybills.Api.Tests
dotnet test tests/Gekko.Waybills.Tests
```

## 13) Setup & run instructions (backend + frontend)
### Backend
```
dotnet restore
dotnet run --project src/Gekko.Waybills.Api
```
API runs at: `http://localhost:5003`
Default connection strings are in:
- `src/Gekko.Waybills.Api/appsettings.json`
- `src/Gekko.Waybills.Api/appsettings.Development.json`

### Frontend
```
cd client
npm install
npm run dev
```
Frontend runs at: `http://localhost:5173`. The Vite proxy forwards `/api` to `http://localhost:5003` (see `client/vite.config.js`).

### Running infrastructure (RabbitMQ)
`docker-compose.yml` starts RabbitMQ and is required for async imports and audit events.  
Run:
```
docker compose up -d
```

### Required headers
All API requests must include:
```
X-Tenant-ID: <tenant>
```
Missing tenant headers return `400 Bad Request`.

Example request:
```
curl -H "X-Tenant-ID: TENANT001" "http://localhost:5003/api/waybills?page=1&pageSize=50"
```

## 14) Architecture decisions and tradeoffs
- **Clean Architecture** separates concerns but introduces more projects and DI wiring.
- **DB-backed locks** allow safe coordination across multiple instances.
- **In-memory cache** keeps latency low but is node-local; invalidation uses a per-tenant version.
- **RabbitMQ** provides reliable event delivery but adds infrastructure complexity.
- **SQLite for tests** keeps integration tests fast and isolated.

## 15) Assumptions made during development
- Tenant header is always provided by clients.
- SQLite is used only for tests; SQL Server is used in production.
- RowVersion is generated by the database in production.
- The minimal UI is for testing only, not production.
- Each async import job is treated as an independent, retry-safe unit of work.

## 16) Code documentation strategy (XML comments & inline comments)
- Public DTOs and controller actions include XML comments.
- Inline comments are used only for non-obvious logic (e.g., cache version invalidation, CSV row normalization/parsing, concurrency conflict handling).
- Swagger is configured to include XML docs.
