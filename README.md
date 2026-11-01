# Rest API Template (.NET 10, Clean Architecture)

[![CI](https://github.com/sachindhole789-maker/projectQ/actions/workflows/ci.yml/badge.svg)](https://github.com/sachindhole789-maker/projectQ/actions/workflows/ci.yml)

**Repository:** <https://github.com/sachindhole789-maker/projectQ>

> **No credentials or database scripts are published here.** All keys,
> passwords, connection strings, database schemas, stored procedures and queries
> are supplied locally or through environment variables. Committed appsettings
> files contain empty values and placeholders only. See
> [Configuration you need to do](#7-configuration-you-need-to-do).

A reusable ASP.NET Core API template built with Clean Architecture, Dapper and a
custom CQRS dispatcher. It includes everything needed to start a production-shaped
API: four environments, stored-procedure data access, JWT auth, encrypted
configuration, file uploads with virus scanning, audit logging, notifications,
SignalR realtime hubs, background workers, tests, Docker/AWS infrastructure and
documentation. Copy the structure into any future project and only change the
configuration and your feature files.

---

## Table of contents

1. [What you get](#1-what-you-get)
2. [Architecture at a glance](#2-architecture-at-a-glance)
3. [Repository structure](#3-repository-structure)
4. [Prerequisites](#4-prerequisites)
5. [Quick start (PreDevelopment)](#5-quick-start-predevelopment)
6. [Environments](#6-environments)
7. [Configuration you need to do](#7-configuration-you-need-to-do)
8. [Secrets and passwords](#8-secrets-and-passwords)
9. [API contract in one minute](#9-api-contract-in-one-minute)
10. [Adding a feature](#10-adding-a-feature)
11. [Files, realtime, audit and background jobs](#11-files-realtime-audit-and-background-jobs)
12. [Testing](#12-testing)
13. [AWS infrastructure](#13-aws-infrastructure)
14. [GitHub and CI](#14-github-and-ci)
15. [Use this structure in a future project — full plan](#15-use-this-structure-in-a-future-project--full-plan)
16. [Production go-live checklist](#16-production-go-live-checklist)
17. [Common commands](#17-common-commands)
18. [Troubleshooting](#18-troubleshooting)
19. [Known limits](#19-known-limits)
20. [Documentation index](#20-documentation-index)

---

## 1. What you get

| Area | Implementation |
| --- | --- |
| Layers | `Rest.API` → `App.Application` → `App.Infrastructure`; `App.Domain` shared by all |
| Environments | `PreDevelopment`, `Development`, `Testing`, `Production` |
| Data access | `IDbManager` over Dapper; SQL Server, AWS RDS (SQL Server), Oracle |
| Paging | Stored-procedure paging with two-grid or separate count procedure |
| Responses | Single envelope, HTTP 200 only, `StatusCode` carried as a string |
| Validation | FluentValidation executed by the dispatcher |
| Auth | JWT access + rotating refresh tokens; refresh tokens stored hashed |
| Crypto | AES-256-GCM `enc:v1:` configuration values; PBKDF2-SHA256 passwords |
| Realtime | SignalR dashboard and per-user notification hubs |
| Files | Content-signature validation, ClamAV scan, S3/Local storage, SHA-256 metadata |
| Audit | Middleware + bounded queue + background DB writer + retention worker |
| Notifications | DB outbox + SMTP (MailKit) or log sender; in-app via SignalR |
| Resilience | Chained per-IP/per-user rate limiting, global exception envelope |
| Observability | Serilog rolling text logs, request logging, `/health` |
| Background work | API-hosted workers plus a dedicated `App.Runner` host |
| Infrastructure | Docker Compose, Terraform (S3 + optional RDS), AWS CLI script, GitHub Actions |
| Tests | xUnit unit tests + API smoke tests with `WebApplicationFactory` |

## 2. Architecture at a glance

```
                    ┌──────────────────────────────┐
 HTTP / SignalR ───▶│  Rest.API                    │
                    │  controllers, middleware,    │
                    │  auth, realtime, CORS        │
                    └──────────────┬───────────────┘
                                   │ IDispatcher
                    ┌──────────────▼───────────────┐
                    │  App.Application             │
                    │  features (query/command,    │
                    │  response, validator,        │
                    │  handler), services, options │
                    └──────────────┬───────────────┘
                                   │ interfaces
                    ┌──────────────▼───────────────┐
                    │  App.Infrastructure          │
                    │  Dapper, repositories, S3,   │
                    │  SMTP, ClamAV, JWT, crypto,  │
                    │  workers, SignalR publisher  │
                    └──────────────┬───────────────┘
                                   │
                    ┌──────────────▼───────────────┐
                    │  App.Domain                  │
                    │  entities, DTOs, enums,      │
                    │  interfaces, constants       │
                    └──────────────────────────────┘
```

Dependencies point inward:

- `App.Domain` has no project references.
- `App.Application` references Domain.
- `App.Infrastructure` references Application and Domain.
- `Rest.API` references Application and Infrastructure.

Interfaces live in Domain/Application. Implementations live in Infrastructure. The
API never talks to a database, S3 or SMTP directly.

## 3. Repository structure

```
projectQ/
├── Rest.slnx                         solution (6 projects)
├── README.md                         this guide
├── Directory.Build.props             shared compiler/analyzer settings
├── .editorconfig
├── .env.example                      local Docker settings template (no values)
├── docker-compose.yml                SQL Server, LocalStack, Mailpit, ClamAV
├── src/
│   ├── Rest.API/                     HTTP host
│   │   ├── Controllers/              thin HTTP adapters
│   │   ├── Http/                     request id, audit middleware, envelope writer
│   │   ├── Middleware/               global exception handler
│   │   ├── Realtime/                 SignalR hubs, publisher, dashboard relay
│   │   ├── Security/                 claims provider, authorization handler
│   │   ├── Extensions/               DI, ResponseType attribute, ToActionResult
│   │   ├── appsettings*.json         environment configuration
│   │   └── Program.cs                host composition
│   ├── App.Application/              business logic
│   │   ├── Abstractions/             CQRS, dispatcher, ambient interfaces
│   │   ├── Common/Options/           strongly typed configuration
│   │   ├── Common/Responses/         Result<T>, ApiResponse<T>, pagination
│   │   ├── Features/V1/<Area>/       one file per use case
│   │   └── Services/                 dispatcher, validation, notifications, dashboard
│   ├── App.Domain/                   core model and contracts
│   │   ├── Entities/                 database models
│   │   ├── Dtos/                     query shapes
│   │   ├── Interfaces/Persistence/   repositories + IDbManager
│   │   └── Interfaces/Infrastructure/ email, storage, scan, crypto, user info
│   └── App.Infrastructure/           concrete implementations
│       ├── Persistence/              DbManager, repositories, bootstrapper
│       ├── Security/                 AES-GCM, secret resolver, PBKDF2, JWT
│       ├── Storage/                  S3 and local storage
│       ├── Notifications/            SMTP and log email senders
│       ├── VirusScanning/            ClamAV and no-op scanners
│       ├── Audit/                    bounded queue and DB writer
│       └── BackgroundJobs/           notification, retention, dashboard workers
├── runner/App.Runner/                worker host + key/encryption/hash utilities
├── tests/App.Tests/                  unit + API smoke tests
├── db/                               local-only schema and procedures (git-ignored, not published)
├── infra/terraform/                  S3 per environment + optional RDS SQL Server
├── scripts/create-s3-buckets.sh      AWS CLI bucket bootstrap (also LocalStack)
├── docs/                             deeper reference documents
└── .github/workflows/ci.yml          restore, build, test
```

## 4. Prerequisites

| Tool | Required | Purpose |
| --- | --- | --- |
| .NET SDK 10.0.4xx | Yes | Build and run (`net10.0`) |
| Docker Desktop | Recommended | SQL Server, LocalStack, Mailpit, ClamAV |
| AWS CLI | For AWS setup | Run the bucket script; optional with Terraform |
| Terraform | For AWS setup | Provision S3 and optional RDS |
| Git | Yes | Clone/push |
| `gh` CLI | Optional | Create repositories from the terminal |

Check your SDK:

```bash
dotnet --version    # must be 10.0.x
```

## 5. Quick start (PreDevelopment)

PreDevelopment runs entirely locally: SQL Server, local disk storage, email
written to the log, and virus scanning disabled.

### 5.1 Clone the repository

```bash
git clone https://github.com/sachindhole789-maker/projectQ.git
cd projectQ
```

### 5.2 Configure your local secrets

Nothing sensitive is committed. Generate two keys, one for the AES-GCM master
key and one for JWT signing:

```bash
dotnet run --project runner/App.Runner -- generate-key 32   # Security:MasterKey
dotnet run --project runner/App.Runner -- generate-key 32   # Jwt:SigningKey
```

Create your local, git-ignored overrides from the examples:

```bash
cp src/Rest.API/appsettings.PreDevelopment.local.json.example \
   src/Rest.API/appsettings.PreDevelopment.local.json

cp runner/App.Runner/appsettings.PreDevelopment.local.json.example \
   runner/App.Runner/appsettings.PreDevelopment.local.json
```

Fill both files:

```json
{
  "Security": { "MasterKey": "<first generated key>" },
  "Jwt": { "SigningKey": "<second generated key>" },
  "Database": {
    "ConnectionString": "Server=localhost,1433;Database=RestDb_PreDevelopment;User Id=sa;Password=<your .env password>;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

`appsettings.*.local.json` is git-ignored and is loaded **after** the normal
configuration sources for local development. Do not create it on a server; use
`Security__MasterKey`, `Jwt__SigningKey` and `Database__ConnectionString`
environment variables there.

> `generate-key` works before any key is configured. `encrypt` and the API host
> require `Security:MasterKey`.

If you use a locally installed SQL Server with Windows authentication instead,
replace the connection string with:

```
Server=localhost;Database=RestDb_PreDevelopment;Trusted_Connection=True;TrustServerCertificate=True;
```

### 5.3 Start SQL Server

```bash
cp .env.example .env
# edit .env and set MSSQL_SA_PASSWORD to your own strong local password
docker compose up -d sqlserver
```

`docker-compose.yml` refuses to start when `MSSQL_SA_PASSWORD` is empty. Use that
same password in both local override files. If you change the password after the
container was created, recreate it with
`docker compose down -v && docker compose up -d sqlserver`.

### 5.4 Create the database once

Docker starts the SQL Server instance, not the database:

```bash
docker exec -it rest-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YOUR_SA_PASSWORD' -C \
  -Q "IF DB_ID('RestDb_PreDevelopment') IS NULL CREATE DATABASE RestDb_PreDevelopment;"
```

### 5.5 Add your schema locally

Database schemas, tables, stored procedures and queries are **not part of this
repository**. Put your SQL in `db/sqlserver/001_Schema.sql` locally. The `db/`
folder is git-ignored, and `App.Infrastructure.csproj` embeds the file only when
it exists. With `Database:InitializeOnStartup=true`, the API applies it at
startup; if the file is absent, it logs a warning and continues.

To apply it manually instead, keep `InitializeOnStartup=false` and run:

```bash
docker exec -i rest-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YOUR_SA_PASSWORD' -C -d RestDb_PreDevelopment \
  < db/sqlserver/001_Schema.sql
```

### 5.6 Create the first admin user

Either enable seeding for one run:

```bash
# Bash
export Seed__AdminPassword='YOUR_ADMIN_PASSWORD'

# PowerShell
$env:Seed__AdminPassword='YOUR_ADMIN_PASSWORD'

# cmd.exe
set Seed__AdminPassword=YOUR_ADMIN_PASSWORD
```

Then set `Database:SeedOnStartup` to `true` in your local override file for one
run, or insert a user with the runner `hash` command. Disable seeding afterwards.

### 5.7 Run the API

```bash
dotnet run --project src/Rest.API --launch-profile PreDevelopment
```

| URL | Purpose |
| --- | --- |
| <http://localhost:5142> | Root endpoint |
| <http://localhost:5142/health> | Health check |
| <http://localhost:5142/openapi/v1.json> | OpenAPI document |
| <http://localhost:5142/scalar/v1> | Scalar API UI |

### 5.8 Log in and call a protected endpoint

```bash
curl -s http://localhost:5142/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"Email":"admin@example.com","Password":"YOUR_ADMIN_PASSWORD"}'
```

Copy `Data.AccessToken` from the response and call:

```bash
curl -s http://localhost:5142/api/v1/students/CUST-1001 \
  -H "Authorization: Bearer <accessToken>"
```

> **Launch profile precedence:** the profiles set `ASPNETCORE_ENVIRONMENT`. If you
> export the variable yourself, run with `--no-launch-profile`.

## 6. Environments

| Environment | Purpose | Database | Storage | Email | Virus scan |
| --- | --- | --- | --- | --- | --- |
| `PreDevelopment` | Local dev, schema auto-apply | SQL Server `localhost` | Local disk | Log | None |
| `Development` | Shared AWS development | AWS RDS SQL Server | S3 | Log (or SMTP) | None |
| `Testing` | Automated/integration testing | AWS RDS SQL Server | S3 / LocalStack | SMTP Mailpit | ClamAV |
| `Production` | Live | AWS RDS SQL Server | S3 + SSE/KMS | SMTP | ClamAV |

The API reads `ASPNETCORE_ENVIRONMENT`; the runner reads `DOTNET_ENVIRONMENT`
(falling back to `ASPNETCORE_ENVIRONMENT`). Valid values are exactly the four
names above.

Configuration precedence (later wins):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User secrets (Development only, if configured)
4. Environment variables
5. Command-line arguments
6. `appsettings.{Environment}.local.json` (local development only, git-ignored)

The committed environment files contain **no keys**: `Security:MasterKey` and
`Jwt:SigningKey` are empty and validation fails fast until you supply them.
Production settings are **not committed**: copy
`appsettings.Production.json.example` to the server or supply every value as an
environment variable. Never place a `.local.json` file on a server.

## 7. Configuration you need to do

This is the practical checklist. Full option-by-option tables are in
[docs/CONFIGURATION.md](docs/CONFIGURATION.md).

### 7.1 Must-change settings

| Priority | Setting | Where | What to do |
| --- | --- | --- | --- |
| P0 | `Security:MasterKey` | Local override, environment file or `Security__MasterKey` | Generate a new 32-byte key per environment; store it in a secret manager. No keys are committed. |
| P0 | `Jwt:SigningKey` | `Jwt` section | Generate your own; optionally encrypt with that environment's master key. Must resolve to at least 32 characters |
| P0 | `Database:ConnectionString` | `Database` section | Point at SQL Server, RDS or Oracle; encrypt the string |
| P0 | `Database:Provider` | `Database` section | `SqlServer`, `AwsRds` or `Oracle` |
| P0 | `Storage:BucketName`, `Region` | `Storage` section | Set the bucket created for the environment |
| P0 | `Email:Host`, `Port`, `Username`, `Password` | `Email` section | Use SMTP in Testing/Production; `Log` for local |
| P1 | `VirusScan:Provider` | `VirusScan` section | `ClamAV` in Testing/Production; `None` locally |
| P1 | `Cors:AllowedOrigins` | `Cors` section | Explicit production origins; set `AllowLocalhostAnyPort=false` |
| P1 | `RateLimiting:*` | `RateLimiting` section | Tune burst/user/auth limits from real traffic |
| P1 | `AuditLog:RetentionDays`, `SensitiveJsonKeys` | `AuditLog` section | Match compliance policy; keep masking keys complete |
| P1 | `Seed:AdminEmail`, `Seed:AdminPassword` | `Seed` section | Create the first admin, then disable seeding |
| P2 | `BackgroundJobs:*` | `BackgroundJobs` section | Keep notification/retention on the runner only |
| P2 | `Notifications:*` | `Notifications` section | Polling interval, batch size, retries |
| P2 | `Storage:MaxFileSizeBytes` | `Storage` section | Keep Kestrel, form and controller limits aligned |

### 7.2 Environment variables

Environment variables override JSON. Use `__` between section and key:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export Security__MasterKey='BASE64_32_BYTE_KEY'
export Jwt__SigningKey='enc:v1:...'
export Database__Provider='AwsRds'
export Database__ConnectionString='enc:v1:...'
export Storage__BucketName='rest-api-production-documents'
export Storage__Region='ap-south-1'
export Email__Host='smtp.example.com'
export Email__Username='smtp-user'
export Email__Password='enc:v1:...'
export Seed__AdminPassword='enc:v1:...'
```

| Shell | Example |
| --- | --- |
| Bash | `export Key__Name='value'` |
| PowerShell | `$env:Key__Name='value'` |
| cmd.exe | `set Key__Name=value` |

The runner reads `DOTNET_ENVIRONMENT`, so utility commands encrypt with the
matching key.

### 7.3 JWT

| Key | Default | Guidance |
| --- | --- | --- |
| `Issuer` | `Rest.API` | Must match between token issuing and validation |
| `Audience` | `Rest.Client` | Must match on every host |
| `SigningKey` | placeholder | Encrypt it; at least 32 characters (256 bits) |
| `AccessTokenMinutes` | `20` | 15–30 minutes; shorter means faster revocation |
| `RefreshTokenDays` | `14` | Refresh tokens rotate on every use |
| `ClockSkewSeconds` | `30` | Kept tight on purpose |

Change `Issuer`/`Audience` for your product and update the client. If you change
the signing key, existing access tokens become invalid.

### 7.4 Rate limiting

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Turns the limiter on |
| `GlobalPermitLimit` | `100` | Per-IP burst guard per window |
| `GlobalWindowSeconds` | `60` | Fixed window |
| `GlobalQueueLimit` | `0` | `0` rejects immediately |
| `UserPermitLimit` | `300` | Per authenticated user per window |
| `UserWindowSeconds` | `60` | Sliding window |
| `AuthPermitLimit` | `8` | Login/refresh attempts per IP |
| `AuthWindowSeconds` | `60` | Auth window |

Start with defaults, watch `429` counts and `Retry-After`, then tune. Keep the auth
limit low (5–10). `OPTIONS`, `/health` and `/hubs` are exempt.

### 7.5 CORS for Flutter

| Key | Default | Guidance |
| --- | --- | --- |
| `AllowedOrigins` | `[]` | Exact production origins |
| `AllowLocalhostAnyPort` | `true` | Any localhost port outside Production |
| `AllowCredentials` | `true` | Set `false` if you do not use cookies |

Flutter mobile/desktop does not need CORS. Flutter web dev servers use random
localhost ports, which is why localhost is allowed outside Production. In
Production, list the real web origins and disable the localhost rule.

### 7.6 Database

| Key | Default | Guidance |
| --- | --- | --- |
| `Provider` | `SqlServer` | `SqlServer`, `AwsRds` or `Oracle` |
| `ConnectionString` | local SQL Server | May be `enc:v1:` |
| `CommandTimeoutSeconds` | `60` | Dapper command timeout |
| `InitializeOnStartup` | `false` | Applies your local `db/sqlserver/001_Schema.sql` when it exists; never Production |
| `SeedOnStartup` | `false` | Creates the admin when a seed password is set |
| `AuditRetentionDays` | `90` | Retention worker |

`AwsRds` uses the SQL Server driver. Oracle uses
`Oracle.ManagedDataAccess.Core`; automatic schema creation is SQL Server only, so
provide Oracle DDL separately.

`IDbManager.QueryPagedAsync` supports two paging contracts:

1. **Two grids:** first result set = page rows, second = total count (default).
2. **Separate count procedure:** pass `countProcedureName`.

Parameters `@PageNumber` and `@PageSize` are added automatically.

### 7.7 Storage and file uploads

| Key | Default | Guidance |
| --- | --- | --- |
| `Provider` | `Local` | `S3` or `Local` |
| `BucketName` | empty | Required for S3 |
| `Region` | `ap-south-1` | AWS region |
| `ServiceUrl` | null | `http://localhost:4566` for LocalStack |
| `ForcePathStyle` | `false` | `true` for LocalStack/MinIO |
| `AccessKey`, `SecretKey` | null | Prefer IAM roles; values may be encrypted |
| `Prefix` | `documents` | Object-key prefix |
| `UseServerSideEncryption` | `true` | SSE-S3 by default |
| `KmsKeyId` | null | Enables SSE-KMS |
| `PresignedUrlMinutes` | `15` | Read URL lifetime |
| `LocalRootPath` | `storage` | Local provider root |
| `MaxFileSizeBytes` | `26214400` | 25 MB; Kestrel derives from it |
| `AllowedExtensions` | pdf/docx/xlsx/pptx/doc/xls/ppt/txt/csv | Extension allow-list |

Uploads are validated by extension, size, magic bytes/OOXML structure/OLE
signature/UTF-8 decoding and SHA-256, then scanned by ClamAV (fail-closed), then
stored under a server-generated key. S3 objects stay private and are served via
short-lived presigned URLs. Local storage rejects path traversal.

### 7.8 Email and notifications

| Key | Default | Guidance |
| --- | --- | --- |
| `Email:Provider` | `Log` | `Smtp` or `Log` |
| `Email:Host`, `Port` | `localhost`, `1025` | Mailpit in Testing |
| `Email:UseStartTls`, `UseSsl` | `false` | Port 587 usually uses STARTTLS |
| `Email:Username`, `Password` | null | Password may be encrypted |
| `Email:FromEmail`, `FromName` | sample values | Sender identity |
| `Notifications:Enabled` | `true` | Enables the outbox dispatcher |
| `Notifications:PollIntervalSeconds` | `15` | Runner polling |
| `Notifications:BatchSize` | `25` | Rows per cycle |
| `Notifications:LockMinutes` | `5` | Claim lease |
| `Notifications:MaxAttempts` | `5` | Retry limit |

Recommendation: provider-agnostic SMTP through MailKit. Use Mailpit locally and a
free transactional provider (Brevo, Resend, Mailtrap) or corporate SMTP in
production. There is no vendor SDK to replace if you switch providers.

### 7.9 Virus scanning

| Key | Default | Guidance |
| --- | --- | --- |
| `Provider` | `None` | `ClamAV` or `None` |
| `Host`, `Port` | `localhost`, `3310` | clamd INSTREAM |
| `TimeoutSeconds` | `60` | Scan timeout |

ClamAV fails closed: if it is unreachable, the upload is rejected. Use `None` only
for local development.

### 7.10 Audit log

| Key | Default | Guidance |
| --- | --- | --- |
| `Enabled` | `true` | Master switch |
| `CaptureRequestBody`, `CaptureResponseBody` | `true` | Text/JSON only |
| `MaxBodyCharacters` | `8000` | Truncation |
| `QueueCapacity` | `10000` | Bounded channel |
| `BatchSize`, `FlushSeconds` | `50`, `2` | Writer batching |
| `RetentionDays` | `90` | Retention worker |
| `ExcludedPaths` | health/openapi/scalar/hubs | Not audited |
| `SensitiveJsonKeys` | password/token/secret variants | Masked to `"***"` |

Binary `/content` responses are never captured. A full queue drops rows with a
warning; a database outage drops the current batch. Use a durable queue if audit
completeness is a compliance requirement.

### 7.11 Seed user

| Key | Default | Guidance |
| --- | --- | --- |
| `AdminEmail` | `admin@example.com` | Lower-cased |
| `AdminPassword` | empty | Skipped when empty; encrypt or use an environment variable |
| `AdminDisplayName` | `Administrator` | Display name |

Seeding runs only when `Database:SeedOnStartup=true` and the host is not
Production. Disable it after the first admin exists.

## 8. Secrets and passwords

### 8.1 Nothing sensitive is committed

The repository deliberately contains **no master keys, JWT signing keys, SMTP
passwords, AWS keys, database passwords or SQL scripts**. Committed appsettings
files are empty or use obvious placeholders such as `YOUR_SA_PASSWORD` and
`CHANGE_ME`. Supply real values with one of:

- `appsettings.{Environment}.local.json` for local development (git-ignored),
- environment variables for containers and servers,
- a secret manager that injects environment variables.

The API and runner fail fast at startup when `Security:MasterKey` or
`Jwt:SigningKey` is missing.

### 8.2 Encrypted configuration values

All application-level secrets can be stored as AES-256-GCM values:

```
enc:v1:{base64 nonce}:{base64 tag}:{base64 ciphertext}
```

Supported values: `Database:ConnectionString`, `Jwt:SigningKey`,
`Storage:AccessKey`, `Storage:SecretKey`, `Email:Password`,
`Seed:AdminPassword`.

`ISecretResolver` decrypts them at startup. Plain values still work, so you can
migrate gradually.

### 8.3 Generate, encrypt and hash

```bash
# 1. Generate a master key for the environment
dotnet run --project runner/App.Runner -- generate-key 32

# 2. Make the runner use the same environment/master key
export DOTNET_ENVIRONMENT=Production
export Security__MasterKey='THE_KEY_FROM_STEP_1'

# 3. Encrypt a value with that key
dotnet run --project runner/App.Runner -- encrypt "Server=...;Password=..."

# 4. Hash a password for manual seeding
dotnet run --project runner/App.Runner -- hash "YOUR_ADMIN_PASSWORD"
```

Paste the output into the environment file or set it as an environment variable.
The runner output directory contains the copied `appsettings` files, so run it from
the repository as shown.

### 8.4 Password storage

- PBKDF2-HMAC-SHA256: `PBKDF2-SHA256$iterations$salt$hash`.
- 16-byte salt, 32-byte hash, 210,000 iterations by default.
- Constant-time comparison.
- Refresh tokens: only the SHA-256 hash is stored.
- User passwords are never encrypted or logged.

### 8.5 Rotation

1. Keep the old master key available.
2. Generate a new key.
3. Re-encrypt every `enc:v1:` value with the new key.
4. Update `Security:MasterKey` and restart API + runner together.
5. Changing the JWT signing key invalidates access tokens; clients re-login or
   refresh.

Full details: [docs/SECURITY.md](docs/SECURITY.md).

## 9. API contract in one minute

Every controller action returns HTTP 200. The real outcome is in the body:

```json
{
  "RequestId": "0HN8...",
  "Data": { "Name": "Asha", "Email": "asha@example.com", "Mobile": "9999999999", "City": "Pune" },
  "Data2": null,
  "Pagination": null,
  "Pagination2": null,
  "Message": "Retrieved successfully",
  "Status": "SUCCESS",
  "StatusCode": "200"
}
```

| `StatusCode` | Meaning |
| --- | --- |
| `"200"` | Success |
| `"400"` | Missing/invalid body |
| `"401"` | Missing/expired token or bad credentials |
| `"403"` | Not allowed |
| `"404"` | Not found |
| `"409"` | Duplicate |
| `"422"` | Validation or file-content failure |
| `"429"` | Rate limited (`Retry-After` header set) |
| `"500"` | Unexpected server error |

Controller pattern:

```csharp
[ResponseType<GetStudentResponse>]
public sealed class StudentsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetByCustomerId(string customerId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.ExecuteQuery(new GetStudentQuery(customerId), cancellationToken);
        return result.ToActionResult();   // always OkObjectResult
    }
}
```

### Endpoints

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/` | anonymous | Service name, environment, server time |
| GET | `/health` | anonymous | Health check |
| POST | `/api/v1/auth/login` | anonymous, strict rate limit | Login |
| POST | `/api/v1/auth/refresh` | anonymous, strict rate limit | Rotate refresh token |
| POST | `/api/v1/auth/register` | `Admin` | Create a user |
| GET | `/api/v1/students/{customerId}` | any user | Student by customer id |
| GET | `/api/v1/students` | any user | Paged/filtered students |
| POST | `/api/v1/students` | any user | Create a student |
| POST | `/api/v1/files` | any user | Upload a document |
| GET | `/api/v1/files/{fileId}` | owner or `Admin` | File metadata |
| GET | `/api/v1/files/{fileId}/content` | owner or `Admin` | Binary download |
| GET | `/api/v1/dashboard/snapshot` | any user | Metrics + latest update |
| POST | `/api/v1/dashboard/publish` | any user | Publish to dashboard clients |
| POST | `/api/v1/notifications` | any user | Queue email or send in-app |
| WS | `/hubs/dashboard` | JWT | Dashboard updates |
| WS | `/hubs/notifications` | JWT | Per-user notifications |

The only binary response is `GET /api/v1/files/{fileId}/content`. See
[docs/API-CONTRACT.md](docs/API-CONTRACT.md) for full request/response examples,
pagination and SignalR client code.

## 10. Adding a feature

A feature is one file under `src/App.Application/Features/V1/<Area>/` containing:

1. A `record` implementing `IQuery<TResponse>` or `ICommand<TResponse>`.
2. A response `record`.
3. A FluentValidation validator (recommended).
4. A handler implementing `IQueryHandler<,>` or `ICommandHandler<,>`.

Then:

1. Add the entity/DTO and repository interface in `App.Domain`.
2. Add the Dapper repository in `App.Infrastructure/Persistence/Repositories`.
3. Register the repository in `AddPersistence`.
4. Add the stored procedure to your local, git-ignored
   `db/sqlserver/001_Schema.sql`.
5. Add the controller action with `[ResponseType<T>]` and `ToActionResult()`.
6. Add a validator test and an API smoke test.

Validators and handlers are discovered by assembly scanning, so no DI registration
is needed for feature files.

Minimal handler shape:

```csharp
public sealed record GetInvoiceQuery(Guid InvoiceId) : IQuery<GetInvoiceResponse>;

public sealed record GetInvoiceResponse(Guid InvoiceId, string Number, decimal Amount);

public sealed class GetInvoiceQueryHandler(IInvoiceRepository repository)
    : IQueryHandler<GetInvoiceQuery, GetInvoiceResponse>
{
    public async Task<Result<GetInvoiceResponse>> Handle(
        GetInvoiceQuery query,
        CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(query.InvoiceId, cancellationToken);
        return invoice is null
            ? Result<GetInvoiceResponse>.Failure(AppStatusCodes.NotFound, "Invoice not found.")
            : Result<GetInvoiceResponse>.Success(
                new GetInvoiceResponse(invoice.InvoiceId, invoice.Number, invoice.Amount),
                "Invoice retrieved successfully");
    }
}
```

Full walkthrough with SQL, repository and controller:
[docs/ADDING-A-FEATURE.md](docs/ADDING-A-FEATURE.md).

## 11. Files, realtime, audit and background jobs

### File upload flow

```
multipart upload
  → name sanitisation
  → extension allow-list + size
  → content signature/OOXML/OLE/UTF-8 check
  → SHA-256
  → ClamAV (fail closed)
  → S3 (SSE, private) or local disk
  → dbo.UploadedFiles metadata
  → presigned URL or /api/v1/files/{id}/content
```

### Realtime

- `POST /api/v1/dashboard/publish` pushes any JSON payload to every client on
  `/hubs/dashboard` as `dashboard.update(eventName, payload, publishedAtUtc)`.
- The dashboard hub replays the latest buffered update on connect.
- `POST /api/v1/notifications` with `Channel=InApp` sends
  `notification.received` to the current user on `/hubs/notifications`.
- Hub auth uses `?access_token=<jwt>` because browser/Flutter web clients cannot
  set an Authorization header on the WebSocket handshake.
- The dashboard buffer is in-memory and per process. Add a SignalR backplane
  (Azure SignalR Service or Redis) before scaling the API horizontally.

### Background workers

| Worker | Runs in | Responsibility |
| --- | --- | --- |
| `AuditLogWriterService` | API | Flush the audit queue to the database |
| `SignalRDashboardRelay` | API | Relay dashboard updates to SignalR |
| `DashboardBroadcastWorker` | API (optional) | Periodic heartbeat snapshot |
| `NotificationDispatchWorker` | Runner | Claim and send queued email |
| `AuditRetentionWorker` | Runner | Delete audit rows past retention |
| `DatabaseBootstrapper` | API | Apply schema/seed outside Production when enabled |

The API keeps notification dispatch and audit retention off so only the runner
mutates the outbox/retention tables. Database claim locks make notification
dispatch safe with multiple runners, but one runner is simpler.

## 12. Testing

```bash
dotnet build Rest.slnx
dotnet test Rest.slnx
```

- `tests/App.Tests/Unit` covers result envelopes, AES-GCM, PBKDF2, document
  validation and FluentValidation rules.
- `tests/App.Tests/Api` boots the real host in `Testing` with
  `WebApplicationFactory<Program>` and verifies health, the 200-only contract,
  401/422 envelopes and PascalCase fields.

The tests use in-memory overrides and need no SQL Server, S3 or SMTP. The API test
factory disables rate limiting, audit and background workers for determinism.

## 13. AWS infrastructure

### Terraform

```bash
cd infra/terraform
cp terraform.tfvars.example terraform.tfvars
# edit project_name, aws_region and CORS origins
terraform init
terraform plan
terraform apply
terraform output
```

It creates one bucket per environment with public access blocked, ownership
enforced, versioning, SSE-S3 or SSE-KMS, lifecycle rules, CORS and a TLS-only
bucket policy. RDS SQL Server is optional (`create_rds=true`) and requires private
subnet IDs and a security group.

### AWS CLI alternative

```bash
export AWS_PROFILE=your-profile
PROJECT_NAME=rest-api AWS_REGION=ap-south-1 \
CORS_ORIGINS="https://app.example.com" \
./scripts/create-s3-buckets.sh development testing production
```

Both paths work against LocalStack by setting `AWS_ENDPOINT_URL`.

### After provisioning

1. Copy the bucket name into `Storage:BucketName` for each environment.
2. Put the RDS endpoint in `Database:ConnectionString` and encrypt it.
3. Apply your schema (kept outside this repository) from CI, a bastion or a
   one-off task.
4. Grant the API role `s3:GetObject`, `s3:PutObject`, `s3:DeleteObject`,
   `s3:ListBucket` and `s3:AbortMultipartUpload` on the bucket (plus KMS actions
   for SSE-KMS).
5. Keep `Database:InitializeOnStartup=false` and `SeedOnStartup=false`.

IAM examples, deployment shapes and troubleshooting:
[docs/OPERATIONS.md](docs/OPERATIONS.md).

## 14. GitHub and CI

**Repository:** <https://github.com/sachindhole789-maker/projectQ>

**Clone:**

```bash
git clone https://github.com/sachindhole789-maker/projectQ.git
cd projectQ
```

**Push changes:**

```bash
git add .
git status                 # review before committing
git commit -m "Describe the change"
git push
```

**CI:** `.github/workflows/ci.yml` runs on pushes to `main`/`master` and on pull
requests:

1. Checkout
2. Install .NET `10.0.x`
3. `dotnet restore Rest.slnx`
4. `dotnet build Rest.slnx --no-restore -c Release`
5. `dotnet test Rest.slnx --no-build -c Release`
6. Upload the TRX artifact

**Credential management:** use Git Credential Manager, a fine-grained personal
access token, or SSH. Never put a token in a URL that is committed or shared.
Never commit `.env`, `appsettings.Production.json`, `*.pfx`, `*.pem`, Terraform
state or any master key. They are covered by `.gitignore`.

After the first push, enable branch protection on `main` and require the CI check.

## 15. Use this structure in a future project — full plan

This is the repeatable plan for turning the template into a new product. Work top
to bottom; each phase has a definition of done.

### Phase 0 — Copy the template

```bash
git clone https://github.com/sachindhole789-maker/projectQ.git my-new-api
cd my-new-api
rm -rf .git
git init -b main
```

Definition of done: the solution builds and tests pass before you change anything.

### Phase 1 — Rename the product identity

Choose a short product prefix (example: `Acme`). Replace the template names:

| Template name | Replace with |
| --- | --- |
| `App.Domain` | `Acme.Domain` |
| `App.Application` | `Acme.Application` |
| `App.Infrastructure` | `Acme.Infrastructure` |
| `Rest.API` | `Acme.API` (or keep `Rest.API` if you prefer) |
| `Rest.slnx` | `Acme.slnx` |
| `rest-api` in Docker/Terraform/scripts | your project slug |

A global search/replace over `namespace`, `using`, `.csproj` filenames, folder
names, `Rest.slnx`, `docker-compose.yml`, `infra/terraform/*.tf`, `scripts/*.sh`
and `.github/workflows/ci.yml` covers it. Then:

```bash
dotnet build Rest.slnx
dotnet test Rest.slnx
```

Definition of done: no `App.*` or `Rest.API` identifiers remain, and the build and
tests pass.

### Phase 2 — Configure environments and secrets

1. Decide which environments you actually need.
2. Generate a separate master key per environment:
   ```bash
   dotnet run --project runner/App.Runner -- generate-key 32
   ```
3. Store the key in your secret manager or `Security__MasterKey`.
4. Fill the empty key placeholders in your own configuration; none are committed.
5. Encrypt the JWT signing key, connection strings and SMTP password.
6. Set `Jwt:Issuer` and `Jwt:Audience` for the product.
7. Create `appsettings.Production.json` on the server from the example; never
   commit it.

Definition of done: every environment starts with its own master key and no
production secret is in Git.

### Phase 3 — Prepare the database

1. Choose `SqlServer`, `AwsRds` or `Oracle`.
2. Create the database.
3. Apply your schema (kept outside this repository) before deploying.
4. Keep `InitializeOnStartup=false` and `SeedOnStartup=false` outside local dev.
5. Create the first admin with `Seed__AdminPassword` for one run, or with the
   runner `hash` command.
6. Add your new tables and stored procedures to the schema file, keeping it
   idempotent.

Definition of done: the API can connect, `/health` is healthy, and login works.

### Phase 4 — Configure storage, email and scanning

1. Create one S3 bucket per environment (Terraform or the script).
2. Set `Storage:Provider=S3`, `BucketName`, `Region` and encryption options.
3. Configure SMTP (`Email:Provider=Smtp`) and test with Mailpit or a real inbox.
4. Set `VirusScan:Provider=ClamAV` in Testing/Production and make clamd reachable.
5. Review `AllowedExtensions` and `MaxFileSizeBytes` for your document types.

Definition of done: upload a file, confirm the scan status, download it by id, and
receive a test notification.

### Phase 5 — Replace the example feature

1. Delete or keep the `Students` feature as a reference.
2. Add your entity/DTO and repository interface.
3. Add the stored procedure.
4. Add the repository implementation and register it.
5. Add the feature file(s) under `Features/V1/<Area>/`.
6. Add the controller with `[ResponseType<T>]`.
7. Add unit and API tests.

Definition of done: a full create/read/paged flow works through the HTTP envelope.

### Phase 6 — Review security and operations

1. Set `Cors:AllowedOrigins`, disable `AllowLocalhostAnyPort` in Production.
2. Tune `RateLimiting:*`.
3. Review `AuditLog` retention and sensitive keys.
4. Decide whether audit must be durable; if yes, replace the in-memory queue.
5. Add a SignalR backplane if you run more than one API instance.
6. Add a cleanup job for expired refresh tokens.
7. Configure log shipping and alerts.

Definition of done: the production checklist in the next section is complete.

### Phase 7 — Provision and deploy

1. Provision S3/RDS with Terraform and apply the schema.
2. Deploy the API (container, App Service, ECS, EC2, etc.) with the production
   environment variables and secret references.
3. Deploy exactly one `App.Runner` host for notification dispatch and retention.
4. Point the load balancer health check at `/health`.
5. Configure TLS at the ingress; the API enables HSTS and HTTPS redirect in
   Production.

Definition of done: the deployed API returns a login token, writes an audit row,
uploads a file to S3 and sends an email through the runner.

### Phase 8 — Enable CI/CD and monitoring

1. Keep `.github/workflows/ci.yml` or adapt it to your platform.
2. Add deployment steps and environment secrets to the pipeline.
3. Protect `main` and require the CI check.
4. Alert on 5xx rate, 429 spikes, scanner failures, audit queue drops and
   notification failures.
5. Document the runbook for secrets rotation, restore and rollback.

Definition of done: every merge is built and tested, and the team knows how to
operate and rotate the deployment.

## 16. Production go-live checklist

- [ ] New master key per environment, stored outside Git
- [ ] JWT signing key rotated and encrypted
- [ ] Database connection string encrypted or injected as a secret
- [ ] SMTP password encrypted; TLS enabled for non-localhost
- [ ] `Security:MasterKey` available to both API and runner
- [ ] `Database:InitializeOnStartup=false`, `SeedOnStartup=false`
- [ ] Schema applied and the first admin created
- [ ] `Cors:AllowedOrigins` set; `AllowLocalhostAnyPort=false`
- [ ] HTTPS with a valid certificate; HSTS and redirect active
- [ ] `RateLimiting:Enabled=true`; auth limit 5–10
- [ ] ClamAV reachable and updated
- [ ] S3 bucket private, versioned, encrypted, TLS-only
- [ ] IAM role scoped to the bucket and key
- [ ] Audit retention and log retention match policy
- [ ] One runner instance owns notification dispatch and retention
- [ ] Logs shipped off the host; alerts configured
- [ ] Backup and restore tested for RDS and S3 versioning
- [ ] SignalR backplane added if running more than one API instance
- [ ] Expired refresh-token cleanup scheduled

## 17. Common commands

| Goal | Command |
| --- | --- |
| Build | `dotnet build Rest.slnx` |
| Test | `dotnet test Rest.slnx` |
| Run API (PreDevelopment) | `dotnet run --project src/Rest.API --launch-profile PreDevelopment` |
| Run API (env variable) | `ASPNETCORE_ENVIRONMENT=Testing dotnet run --project src/Rest.API --no-launch-profile` |
| Run workers | `DOTNET_ENVIRONMENT=PreDevelopment dotnet run --project runner/App.Runner --no-launch-profile` |
| Generate master key | `dotnet run --project runner/App.Runner -- generate-key 32` |
| Encrypt a secret | `dotnet run --project runner/App.Runner -- encrypt "value"` |
| Hash a password | `dotnet run --project runner/App.Runner -- hash "password"` |
| Start local services | `docker compose up -d` |
| Stop local services | `docker compose down` |
| Apply SQL schema | `docker exec -i rest-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YOUR_SA_PASSWORD' -C -d RestDb_PreDevelopment < db/sqlserver/001_Schema.sql` (the file is local-only) |
| Create S3 buckets | `PROJECT_NAME=rest-api AWS_REGION=ap-south-1 ./scripts/create-s3-buckets.sh` |
| Provision AWS | `cd infra/terraform && terraform init && terraform apply` |

## 18. Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| API starts in the wrong environment | launch profile sets `ASPNETCORE_ENVIRONMENT` | use `--no-launch-profile` |
| `MasterKey is required` / decryption fails | missing/wrong key, or value encrypted with another environment's key | export `Security__MasterKey`; re-encrypt with the matching key |
| SQL login fails | database not created, password mismatch, TLS | create the DB, check `.env`, use `TrustServerCertificate=True` locally |
| Missing table/procedure | schema not applied | run your local schema file or enable `InitializeOnStartup` outside Production |
| Every call returns `401` | expired token, issuer/audience mismatch, signing key changed | refresh/login, compare `Jwt:*` across hosts |
| `429` under normal load | limiter too tight or shared NAT IP | raise limits, inspect `Retry-After` |
| Browser CORS error | origin not allowed | add the exact origin to `Cors:AllowedOrigins` |
| S3 uploads fail locally | wrong endpoint/path style/credentials | `ServiceUrl=http://localhost:4566`, `ForcePathStyle=true`, test/test |
| Uploads fail with a scan error | ClamAV starting or unreachable | wait for health, verify host/port, or use `Provider=None` locally |
| Emails never arrive | runner not running or provider is `Log` | start the runner, set `Provider=Smtp`, check Mailpit at :8025 |
| Upload returns 413 | size limits out of sync | align `MaxFileSizeBytes`, Kestrel, form and controller limits |
| No audit rows | audit disabled, writer off, DB down, queue full | check config and logs for "queue is full" |
| SignalR 401 | token missing on the hub URL | append `?access_token=<jwt>` |
| Dashboard misses updates across instances | no backplane | add Azure SignalR/Redis and a shared dashboard store |

More detail: [docs/OPERATIONS.md](docs/OPERATIONS.md).

## 19. Known limits

- `IDbManager` paging uses the SQL Server two-grid contract. Oracle callers should
  use `SYS_REFCURSOR` and adapt `DbManager` or a provider-specific implementation;
  automatic schema creation is SQL Server only.
- The dashboard buffer is per process. Add a backplane and shared buffer for
  horizontal scaling.
- Audit logs and dashboard updates are in memory between flushes; a hard crash can
  lose the last batch.
- ClamAV must be reachable when enabled; the scanner fails closed.
- There is no access-token denylist; revocation takes effect when the access token
  expires. Shorten `Jwt:AccessTokenMinutes` if you need faster revocation.
- Committed PreDevelopment/Development/Testing master keys are for local use only.
  Rotate them for any real environment.

## 20. Documentation index

| Document | Contents |
| --- | --- |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Layers, dependencies, request flow, folder conventions |
| [docs/CONFIGURATION.md](docs/CONFIGURATION.md) | Environment files, every options section, secrets, JWT, rate limits, CORS |
| [docs/API-CONTRACT.md](docs/API-CONTRACT.md) | Envelope, status codes, pagination, endpoints, SignalR, Flutter notes |
| [docs/ADDING-A-FEATURE.md](docs/ADDING-A-FEATURE.md) | End-to-end recipe for a new feature |
| [docs/SECURITY.md](docs/SECURITY.md) | Encryption, hashing, JWT tuning, file upload security, audit masking |
| [docs/OPERATIONS.md](docs/OPERATIONS.md) | Docker, runner, logs, AWS setup, CI, deployment, troubleshooting |
