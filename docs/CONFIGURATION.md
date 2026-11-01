# Configuration

## How configuration is loaded

Both hosts build configuration in the standard .NET order:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User secrets (Development only, if configured)
4. Environment variables
5. Command-line arguments

The API reads `ASPNETCORE_ENVIRONMENT`; the runner reads `DOTNET_ENVIRONMENT`
(falling back to `ASPNETCORE_ENVIRONMENT`). The four supported values are
`PreDevelopment`, `Development`, `Testing` and `Production`.

Environment variables use `__` for section nesting:

```bash
export Database__Provider=SqlServer
export Database__ConnectionString="Server=...;"
export Security__MasterKey="BASE64_32_BYTE_KEY"
export Jwt__SigningKey="enc:v1:..."
```

The API logs a warning at startup if the environment is not one of the four known
names.

### Launch profiles

`src/Rest.API/Properties/launchSettings.json` defines one profile per environment:

| Profile | URL |
| --- | --- |
| `PreDevelopment` | `http://localhost:5142` |
| `Development` | `http://localhost:5143` |
| `Testing` | `http://localhost:5144` |
| `Production` | `https://localhost:7145` / `http://localhost:5145` |

A launch profile sets `ASPNETCORE_ENVIRONMENT`, which overrides an environment
variable exported in your shell for `dotnet run`. If you want to set the
environment yourself, pass `--no-launch-profile`:

```bash
ASPNETCORE_ENVIRONMENT=Testing dotnet run --project src/Rest.API --no-launch-profile
```

The runner has equivalent profiles using `DOTNET_ENVIRONMENT`.

## Environment files

| File | Committed | Purpose |
| --- | --- | --- |
| `appsettings.json` | yes | Shared defaults and safe placeholders |
| `appsettings.PreDevelopment.json` | yes | Local SQL Server placeholders, local storage, log email, no AV |
| `appsettings.Development.json` | yes | RDS/S3 placeholders; keys are empty |
| `appsettings.Testing.json` | yes | LocalStack S3, Mailpit SMTP, ClamAV; keys are empty |
| `appsettings.Production.json.example` | yes | Template with empty key placeholders |
| `appsettings.{Environment}.local.json` | **no** | Developer overrides; loaded last |
| `appsettings.Production.json` | **no** | Real production values, server-only |

`.gitignore` excludes `appsettings.Production.json`, `appsettings.*.local.json`,
`.env`, `db/`, Terraform state and common key formats.

> **No master keys or signing keys are committed.** Generate your own with
> `generate-key` and keep them in the local override, environment variables or a
> secret manager. Startup fails fast until `Security:MasterKey` and
> `Jwt:SigningKey` are set.

## Option reference

### Jwt

| Key | Default | Notes |
| --- | --- | --- |
| `Issuer` | `Rest.API` | Validated on every token |
| `Audience` | `Rest.Client` | Validated on every token |
| `SigningKey` | placeholder | Plaintext or `enc:v1:`; must resolve to at least 32 characters for HS256 |
| `AccessTokenMinutes` | `20` | Keep 15–30 minutes |
| `RefreshTokenDays` | `14` | Refresh tokens rotate on use |
| `ClockSkewSeconds` | `30` | Deliberately tighter than the 5-minute framework default |

Tune the access token lifetime by balancing revocation latency against refresh
traffic. Shortening it increases how often clients call `/api/v1/auth/refresh`.

### Security

| Key | Default | Notes |
| --- | --- | --- |
| `MasterKey` | empty | Required. Base64 16/24/32-byte AES key; supply via `Security__MasterKey` or a local override |
| `Pbkdf2Iterations` | `210000` | Values below 100,000 are clamped up |

### Database

| Key | Default | Notes |
| --- | --- | --- |
| `Provider` | `SqlServer` | `SqlServer`, `AwsRds` or `Oracle` |
| `ConnectionString` | local SQL Server | May be an `enc:v1:` value. PreDevelopment points at the Docker `sa` account; change it if you use Windows auth or a different container password |
| `CommandTimeoutSeconds` | `60` | Applied to every Dapper command |
| `InitializeOnStartup` | `false` | Applies the embedded SQL Server schema; never in Production |
| `SeedOnStartup` | `false` | Creates the admin user when `Seed:AdminPassword` is set |
| `AuditRetentionDays` | `90` | Used by the retention worker |

`AwsRds` uses the SQL Server driver; point the connection string at the RDS
endpoint. Oracle uses `Oracle.ManagedDataAccess.Core` and does not support
automatic schema creation.

### Storage

| Key | Default | Notes |
| --- | --- | --- |
| `Provider` | `Local` | `S3` or `Local` |
| `BucketName` | empty | Required for S3 |
| `Region` | `ap-south-1` | AWS region |
| `ServiceUrl` | null | Set to `http://localhost:4566` for LocalStack |
| `ForcePathStyle` | `false` | `true` for LocalStack/MinIO |
| `AccessKey` / `SecretKey` | null | Prefer IAM roles in AWS; values may be encrypted |
| `Prefix` | `documents` | Object-key prefix |
| `UseServerSideEncryption` | `true` | SSE-S3 by default |
| `KmsKeyId` | null | Enables SSE-KMS when set |
| `PresignedUrlMinutes` | `15` | Read URL lifetime |
| `LocalRootPath` | `storage` | Local provider root directory |
| `LocalDownloadBasePath` | `/api/v1/files` | Local provider URL base |
| `MaxFileSizeBytes` | `26214400` (25 MB) | Kestrel is configured from this value |
| `AllowedExtensions` | pdf/docx/xlsx/pptx/doc/xls/ppt/txt/csv | Extension allow-list |

Uploads are also limited to 50 MB per request by the controller attribute, and
Kestrel is set to `MaxFileSizeBytes + 1 MB` for multipart overhead.

### Email

| Key | Default | Notes |
| --- | --- | --- |
| `Provider` | `Log` | `Smtp` or `Log` |
| `Host` / `Port` | `localhost` / `1025` | Mailpit in Testing |
| `UseStartTls` / `UseSsl` | `false` | Port 587 usually uses STARTTLS |
| `Username` / `Password` | null | Password may be `enc:v1:` |
| `FromEmail` / `FromName` | sample values | Sender identity |
| `TimeoutSeconds` | `30` | SMTP timeout |

The recommendation for a fast, free and safe setup is provider-agnostic SMTP via
MailKit: Mailpit locally, a free transactional provider (Brevo, Resend, Mailtrap)
or your corporate SMTP in production. There is no vendor SDK to swap if you
change providers.

### AuditLog

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | `true` | Master switch |
| `CaptureRequestBody` / `CaptureResponseBody` | `true` | Text/JSON only |
| `MaxBodyCharacters` | `8000` | Truncation limit |
| `QueueCapacity` | `10000` | Bounded channel; full queue drops with a warning |
| `BatchSize` / `FlushSeconds` | `50` / `2` | Background writer batching |
| `RetentionDays` | `90` | Retention worker |
| `ExcludedPaths` | `/health`, `/openapi`, `/scalar`, `/hubs` | Not audited |
| `SensitiveJsonKeys` | password/token/secret variants | Masked to `"***"` |

Audit bodies are never written for binary streams (`/content`), and sensitive keys
are masked recursively before enqueueing.

### RateLimiting

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Adds `UseRateLimiter` and the policies |
| `GlobalPermitLimit` | `100` | Per-IP requests per window (burst guard) |
| `GlobalWindowSeconds` | `60` | Fixed window length |
| `GlobalQueueLimit` | `0` | `0` rejects immediately; `>0` waits |
| `UserPermitLimit` | `300` | Per authenticated user per window |
| `UserWindowSeconds` | `60` | Sliding window length |
| `AuthPermitLimit` | `8` | Login/refresh attempts per IP per window |
| `AuthWindowSeconds` | `60` | Auth policy window |

How to tune:

- Start with the defaults, watch `429` counts and the `Retry-After` header, then
  raise `GlobalPermitLimit`/`UserPermitLimit` for legitimate traffic peaks.
- Keep `AuthPermitLimit` low (5–10) to blunt credential stuffing.
- Set `QueueLimit` only when clients can tolerate waiting; an API should usually
  fail fast.
- Rejections are returned as HTTP 200 with `StatusCode: "429"` and a
  `Retry-After` header, matching the rest of the API.
- Exempt paths: `OPTIONS`, `/health` and `/hubs`.

### Cors

| Key | Default | Notes |
| --- | --- | --- |
| `AllowedOrigins` | `[]` | Explicit allow-list |
| `AllowLocalhostAnyPort` | `true` | Any localhost port outside Production |
| `AllowCredentials` | `true` | Needed for cookie/credential flows |

Outside Production, `http://localhost:<any>`, `http://127.0.0.1:<any>` and
`http://[::1]:<any>` are allowed in addition to `AllowedOrigins`. This covers
Flutter web/desktop debug servers that pick random ports. Production ignores the
localhost rule and requires explicit origins.

### VirusScan

| Key | Default | Notes |
| --- | --- | --- |
| `Provider` | `None` | `ClamAV` or `None` |
| `Host` / `Port` | `localhost` / `3310` | clamd INSTREAM endpoint |
| `TimeoutSeconds` | `60` | Scan timeout |

`ClamAV` fails closed: if the scanner cannot be reached, the upload is rejected.

### Notifications

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | `true` | Enables the outbox dispatcher |
| `PollIntervalSeconds` | `15` | Runner polling interval |
| `BatchSize` | `25` | Rows claimed per cycle |
| `LockMinutes` | `5` | Claim lease |
| `MaxAttempts` | `5` | Retry limit before failure |

### BackgroundJobs

| Key | Default | API | Runner |
| --- | --- | --- | --- |
| `RunAuditWriter` | `true` | on | forced off |
| `RunNotificationDispatcher` | `false` | off | forced on |
| `RunAuditRetention` | `false` | off | forced on |
| `RunDashboardBroadcast` | `true` | on | forced off |
| `DashboardIntervalSeconds` | `5` | heartbeat interval | n/a |
| `RetentionSweepHours` | `24` | retention sweep | retention sweep |

The runner overrides these in code so the two hosts never compete.

### Seed

| Key | Default | Notes |
| --- | --- | --- |
| `AdminEmail` | `admin@example.com` | Lower-cased before insert |
| `AdminPassword` | empty | Skipped when empty; may be `enc:v1:` |
| `AdminDisplayName` | `Administrator` | Display name |

Seed only runs when `Database:SeedOnStartup` is `true` and the host is not
Production. Set the password through `Seed__AdminPassword`:

```bash
Seed__AdminPassword='YOUR_ADMIN_PASSWORD' \
dotnet run --project src/Rest.API --launch-profile PreDevelopment
```

## Secrets workflow

### 1. Generate a master key

```bash
dotnet run --project runner/App.Runner -- generate-key 32
```

Store it as `Security:MasterKey` for the environment, or use
`Security__MasterKey` in the environment.

### 2. Encrypt the secret with that environment's key

```bash
DOTNET_ENVIRONMENT=Production \
dotnet run --project runner/App.Runner -- encrypt "Server=...;Password=..."
```

The runner reads `appsettings.json` + `appsettings.Production.json` +
environment variables from its own output directory. Run it on the server or with
the production master key exported; otherwise you will encrypt with the wrong key.

### 3. Paste the `enc:v1:` value

```json
{
  "Database": {
    "ConnectionString": "enc:v1:..."
  },
  "Jwt": {
    "SigningKey": "enc:v1:..."
  }
}
```

`ISecretResolver` decrypts values at startup for the connection factory, JWT
handler, token service, S3 client, SMTP sender and seed password.

### 4. Hash user passwords

```bash
dotnet run --project runner/App.Runner -- hash "YOUR_ADMIN_PASSWORD"
```

Users registered through the API are hashed automatically; this command is for
manual seeding or support tooling.

### Rotation

1. Keep the old master key available while re-encrypting values.
2. Generate a new key.
3. Re-encrypt every `enc:v1:` value with the new key.
4. Update `Security:MasterKey` and restart the API and runner.
5. After the JWT signing key changes, existing access tokens become invalid;
   expect a wave of re-logins and keep refresh-token rows until they expire.

There is no key registry in `enc:v1:`, so rotation is a deliberate, coordinated
operation rather than transparent. Plan it for a maintenance window.

## Production checklist

- `Security__MasterKey` supplied by the platform, not a file in Git.
- `appsettings.Production.json` present on the server or all values supplied as
  environment variables.
- `Database:InitializeOnStartup=false` and `SeedOnStartup=false`.
- `Cors:AllowLocalhostAnyPort=false` and explicit `AllowedOrigins`.
- `RateLimiting:Enabled=true`, `AuthPermitLimit` low.
- `AuditLog:RetentionDays` matches your compliance policy.
- `VirusScan:Provider=ClamAV` and the scanner reachable from the API host.
- `Storage:Provider=S3`, bucket policy denies insecure transport, versioning on.
- HSTS and HTTPS redirection are active (automatic in Production).
- Logs are collected from `logs/rest-api-*.log`, not only the local disk.
- Only one host runs the notification dispatcher and audit retention (the runner).
