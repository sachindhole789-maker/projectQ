# Architecture

## Layers and dependency direction

```
Rest.API  ──────────────▶ App.Application ──────────────▶ App.Domain
    │                          ▲                              ▲
    └──────────────────▶ App.Infrastructure ─────────────────┘
```

| Project | References | Owns |
| --- | --- | --- |
| `App.Domain` | none | Entities, DTOs, enums, constants, repository and infrastructure interfaces |
| `App.Application` | Domain | Features (query/command, response, validator, handler), dispatcher, result types, cross-cutting options, application services |
| `App.Infrastructure` | Application, Domain | Dapper, repositories, S3/local storage, SMTP, ClamAV, JWT, crypto, hashing, workers, SignalR publisher |
| `Rest.API` | Application, Infrastructure | Controllers, middleware, auth configuration, CORS, rate limiting, SignalR hubs, host composition |
| `App.Runner` | Application, Infrastructure | Worker host and key/encryption/hash utilities |
| `App.Tests` | All | Unit and API smoke tests |

The API never talks to a database, S3 or SMTP directly. The Application layer never
references a concrete library; it depends on interfaces such as
`IDbManager`, `IObjectStorageService`, `IEmailSender`, `IVirusScanner`,
`IEncryptionService`, `IPasswordHasher`, `IJwtTokenService` and
`IRealtimePublisher`.

## Folder conventions

```
src/App.Domain/
  Common/            PagedResult, DatabaseProvider
  Constants/         EnvironmentNames
  Dtos/              request/response DTOs shared with Infrastructure
  Entities/          database-backed models
  Interfaces/
    Persistence/     repositories + IDbManager
    Infrastructure/  email, storage, virus scan, crypto, hashing, user info

src/App.Application/
  Abstractions/      CQRS marker/handler interfaces, dispatcher, ambient services
  Common/
    Exceptions/      AppException
    Options/         strongly typed configuration sections
    Responses/       Result<T>, ApiResponse<T>, PaginationMeta, status codes
  Features/V1/<Area>/ one file per use case
  Services/          dispatcher, document validation, notifications, dashboard buffer

src/App.Infrastructure/
  Audit/             bounded audit queue + database writer
  BackgroundJobs/    notification dispatch, audit retention, dashboard heartbeat
  Extensions/        AddInfrastructure
  Notifications/     SMTP and log email senders
  Persistence/       connection factory, DbManager, repositories, bootstrapper
  Security/          AES-GCM, secret resolver, PBKDF2, JWT
  Storage/           S3 and local object storage
  VirusScanning/     ClamAV and no-op scanners

src/Rest.API/
  Configuration/     environment info adapter
  Controllers/       thin HTTP adapters
  Extensions/        DI, ResponseType attribute, ToActionResult
  Http/              request id, request context, audit middleware, envelope writer
  Middleware/        global exception handler
  Realtime/          SignalR hubs, publisher, dashboard relay
  Security/          claims provider, authorization result handler
```

## Request flow

```
HTTP request
  ↓ ForwardedHeaders
  ↓ Serilog request logging
  ↓ RequestIdMiddleware          (X-Request-Id or TraceIdentifier)
  ↓ AuditLogMiddleware           (buffer request/response, enqueue audit row)
  ↓ ExceptionHandler             (envelope for unhandled exceptions)
  ↓ Production HTTPS/HSTS (only in Production)
  ↓ CORS
  ↓ Authentication (JWT)
  ↓ RateLimiter (chained per-IP + per-user; "auth" policy on login/refresh)
  ↓ Authorization (custom 200-envelope handler)
  ↓ Controller
      ↓ IDispatcher.ExecuteQuery/ExecuteCommand
          ↓ FluentValidation
          ↓ IQueryHandler/ICommandHandler
              ↓ repository / storage / email / crypto
          ↓ Result<T>
      ↓ result.ToActionResult()  → HTTP 200 + ApiResponse<T>
  ↓ Audit middleware copies the buffered body and completes the audit row
```

Because audit runs before the exception handler and before the controller, it sees
the final response body for both success and handled failures. Audit writes are
asynchronous so a slow database does not block the request.

## Response model

- `Result<T>` is the Application outcome: success, failure or error, with an
  optional second grid and pagination metadata.
- `ApiResponse<T>` is the wire shape. `Result.ToApiResponse()` maps one to the
  other.
- `ToActionResult()` always returns `OkObjectResult`.
- `AppStatusCodes` holds string codes (`"200"`, `"400"`, `"401"`, `"403"`,
  `"404"`, `"409"`, `"422"`, `"429"`, `"500"`).
- Model binding failures are rewritten by `InvalidModelStateResponseFactory` to a
  200/422 envelope.
- Authentication challenges and forbidden results are rewritten by
  `ApiAuthorizationResultHandler` for non-hub paths.

The only intentional exceptions are SignalR hub negotiation (protocol-level
status codes) and the binary file download endpoint.

## CQRS without MediatR

`IDispatcher` is a tiny reflection-based dispatcher:

1. Finds `IValidator<TRequest>` in DI and validates.
2. Builds `IQueryHandler<TRequest, TResponse>` or
   `ICommandHandler<TRequest, TResponse>`.
3. Invokes `Handle` and returns `Result<TResponse>`.
4. Attaches the current `RequestId`.

Queries and commands are immutable records. Handlers are registered by assembly
scanning in `App.Application/DependencyInjection.cs`, so a new feature file needs
no DI change. Handlers return failures instead of throwing for expected outcomes;
the exception handler is reserved for bugs and infrastructure faults.

## Data access

`IDbManager` is provider-agnostic and opens/disposes a connection per call:

```csharp
Task<int> ExecuteAsync(string procedureName, object? parameters = null, ...);
Task<T?> QuerySingleOrDefaultAsync<T>(string procedureName, object? parameters = null, ...);
Task<IReadOnlyList<T>> QueryAsync<T>(string procedureName, object? parameters = null, ...);
Task<T?> ExecuteScalarAsync<T>(string procedureName, object? parameters = null, ...);
Task<PagedResult<T>> QueryPagedAsync<T>(
    string procedureName, object? parameters, int pageNumber, int pageSize,
    string? countProcedureName = null, ...);
Task<T> ExecuteInTransactionAsync<T>(Func<IDbConnection, IDbTransaction, CancellationToken, Task<T>> action, ...);
Task<bool> CanConnectAsync(...);
```

`DbConnectionFactory` maps `Database:Provider`:

| Value | Driver |
| --- | --- |
| `SqlServer` / `mssql` | `Microsoft.Data.SqlClient` |
| `AwsRds` / `rds` | `Microsoft.Data.SqlClient` (point the connection string at the RDS endpoint) |
| `Oracle` | `Oracle.ManagedDataAccess.Core` |

The whole connection string can be an `enc:v1:` value. `IDbManager` methods take
stored procedure names and anonymous objects whose property names match the SP
parameters.

### Pagination contract

`QueryPagedAsync` adds `@PageNumber` and `@PageSize` automatically and supports
two shapes:

1. **Two grids** (default): the procedure returns page rows first and a single
   total-count value second. The sample student procedure follows this shape.
2. **Separate count procedure**: pass `countProcedureName` and the manager runs
   the count and page procedures separately.

The result is `PagedResult<T>` and is exposed to clients through
`Result<T>.SuccessPaginated(...)`, which fills `Pagination` and optionally
`Pagination2` for a second grid.

## Security architecture

- `ISecretResolver` transparently decrypts `enc:v1:` configuration values.
- `AesGcmEncryptionService` implements authenticated encryption:
  `enc:v1:{nonce}:{tag}:{cipher}` (Base64 segments, 12-byte nonce, 16-byte tag).
- `Pbkdf2PasswordHasher` stores `PBKDF2-SHA256$iterations$salt$hash` with at
  least 100,000 iterations and a 16-byte salt; 210,000 is the default.
- Refresh tokens are random 64-byte values; only their SHA-256 hex hash is stored.
- The JWT handler validates issuer, audience, lifetime, signing key and HS256
  only, with a configurable clock skew.
- CORS allows any `localhost`/`127.0.0.1` port outside Production (Flutter web/dev)
  and an explicit allow-list in Production.
- Rate limiting is chained: a fixed per-IP burst guard plus a sliding per-user
  window, with a strict fixed "auth" policy on login/refresh. OPTIONS, `/hubs` and
  `/health` are exempt.
- File uploads are validated by extension allow-list, size, magic bytes/OOXML
  package structure/OLE signature/UTF-8 text decoding and SHA-256; then scanned
  by ClamAV (fail-closed) before storage.

See [SECURITY.md](SECURITY.md) for tuning and production hardening.

## Realtime architecture

- `IRealtimePublisher` is the Application-facing abstraction.
- `SignalRRealtimePublisher` (API) sends `notification.received` to a user or all
  clients, and `dashboard.update` to all dashboard clients.
- `SignalRDashboardRelay` (API) forwards updates from the Application dashboard
  buffer to the hub in the background.
- `DashboardHub` replays the latest buffered update on connect; `NotificationHub`
  is a per-user channel.
- Hubs authenticate with the JWT in the `access_token` query string because
  browser/Flutter web SignalR clients cannot set an `Authorization` header.

The dashboard buffer is in-memory and per instance. Add a backplane and shared
buffer before scaling the API horizontally.

## Background architecture

`PeriodicBackgroundService` gives workers a safe loop that catches and logs
exceptions and supports graceful shutdown. The API host and the `App.Runner` host
share the same Infrastructure registration; each turns workers on/off through
`BackgroundJobs:*` so responsibilities do not overlap:

| Worker | API | Runner |
| --- | --- | --- |
| Audit writer | on | off |
| Dashboard relay | on | not registered (API-only) |
| Dashboard heartbeat | on (configurable) | off |
| Notification dispatch | off | on |
| Audit retention | off | on |
| Database bootstrapper | on (when enabled) | off by default |

## Design decisions

- **No MediatR.** A single dispatcher keeps behaviour explicit and removes a
  dependency that would be hard to replace in a reusable template.
- **HTTP 200 only.** Mobile and Flutter clients often treat non-2xx responses as
  transport failures; the envelope makes outcomes uniform.
- **Stored procedures.** Teams can tune queries without redeploying application
  code, and the Dapper adapter stays provider-agnostic.
- **Provider selection by configuration.** SQL Server, RDS and Oracle are chosen
  per environment without code changes (RDS uses the SQL Server driver).
- **Local-first development.** PreDevelopment needs no AWS account; Testing and
  Development can use LocalStack for S3.
- **Secrets encrypted at rest.** Connection strings and keys in configuration are
  `enc:v1:` values so configuration backups are not credential leaks.
