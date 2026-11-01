# Operations

## Local services

`docker-compose.yml` provides everything PreDevelopment and Testing need:

| Service | Container | Ports | Purpose |
| --- | --- | --- | --- |
| SQL Server 2022 | `rest-sqlserver` | `1433` | Local database |
| LocalStack | `rest-localstack` | `4566` | S3 API |
| LocalStack init | `rest-localstack-init` | — | Creates `rest-api-testing` bucket |
| Mailpit | `rest-mailpit` | `1025` SMTP, `8025` UI | Captures email |
| ClamAV | `rest-clamav` | `3310` | Malware scanning |

```bash
cp .env.example .env
docker compose up -d
```

Start only what you need:

```bash
docker compose up -d sqlserver
docker compose up -d sqlserver localstack localstack-init mailpit clamav
```

`MSSQL_SA_PASSWORD` is required and must satisfy SQL Server's complexity rules.
`.env.example` ships with an empty value; set your own password in `.env`.
Docker Compose refuses to start the database container while it is empty.

### Database

The PreDevelopment connection string in the committed appsettings uses
`YOUR_SA_PASSWORD` as a placeholder. Set the real value in your git-ignored
`appsettings.PreDevelopment.local.json` (see the README quick start) or override
`Database__ConnectionString`. Keep the `.env`, the Docker environment and the API
configuration in sync. For a locally installed SQL Server using Windows
authentication, use `Trusted_Connection=True;TrustServerCertificate=True;`.

The SQL Server container starts the server, not your database. Create it once:

```bash
docker exec -it rest-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YOUR_SA_PASSWORD' -C \
  -Q "IF DB_ID('RestDb_PreDevelopment') IS NULL CREATE DATABASE RestDb_PreDevelopment;"
```

Schema, tables and stored procedures are **not published in this repository**.
Keep your script at `db/sqlserver/001_Schema.sql` locally (the folder is
git-ignored). With `Database:InitializeOnStartup=true` (PreDevelopment), the API
embeds and applies it at startup when the file exists. To apply it manually:

```bash
docker exec -i rest-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YOUR_SA_PASSWORD' -C -d RestDb_PreDevelopment \
  < db/sqlserver/001_Schema.sql
```

If no schema is embedded, the bootstrapper logs a warning and skips initialization
instead of failing startup.

`GO` is a sqlcmd batch separator, not T-SQL, so do not run the file through a
driver that does not understand it. `DatabaseBootstrapper` splits batches on
`GO` when it applies the embedded resource.

### LocalStack S3

The init container creates `rest-api-testing` with versioning and public access
blocked. Application settings for Testing already point at it:

```json
"Storage": {
  "Provider": "S3",
  "BucketName": "rest-api-testing",
  "ServiceUrl": "http://localhost:4566",
  "ForcePathStyle": true,
  "Region": "us-east-1"
}
```

LocalStack accepts `test`/`test` credentials. If you run the AWS CLI against it
from the host:

```bash
AWS_ENDPOINT_URL=http://localhost:4566 \
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test \
AWS_DEFAULT_REGION=us-east-1 \
PROJECT_NAME=rest-api CORS_ORIGINS="http://localhost:3000" \
./scripts/create-s3-buckets.sh testing
```

The S3 service uses the default AWS credential chain when `Storage:AccessKey` and
`SecretKey` are empty. LocalStack picks up the credentials from the environment
of the API process.

### Mailpit

- SMTP: `localhost:1025`
- Web UI: <http://localhost:8025>

Testing already uses `Email:Provider=Smtp` with this host. In PreDevelopment the
provider is `Log`, so emails appear in the text logs instead.

### ClamAV

ClamAV downloads signatures on first start and can take a couple of minutes to
become healthy. The API fails closed: if the scanner is unreachable, uploads are
rejected. Use `VirusScan:Provider=None` for local work that does not need
scanning.

## Running the hosts

```bash
# API, one profile per environment
dotnet run --project src/Rest.API --launch-profile PreDevelopment
dotnet run --project src/Rest.API --launch-profile Testing

# API with an environment set yourself
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/Rest.API --no-launch-profile

# Worker host
dotnet run --project runner/App.Runner --launch-profile PreDevelopment
DOTNET_ENVIRONMENT=Production dotnet run --project runner/App.Runner --no-launch-profile
```

Remember: a launch profile sets the environment and wins over your shell variable.
Use `--no-launch-profile` when you want the environment variable to apply.

## Runner utilities

```bash
dotnet run --project runner/App.Runner -- help
dotnet run --project runner/App.Runner -- generate-key 32
dotnet run --project runner/App.Runner -- encrypt "plaintext secret"
dotnet run --project runner/App.Runner -- hash "YOUR_ADMIN_PASSWORD"
```

Utilities read `appsettings.json`, the active environment file, and environment
variables from the runner output directory. Set `DOTNET_ENVIRONMENT` to pick the
master key used for encryption.

When run without a command, the runner starts these workers:

- `NotificationDispatchWorker` — claims and sends queued emails.
- `AuditRetentionWorker` — deletes audit rows older than retention.

The runner forces `RunAuditWriter=false` and `RunDashboardBroadcast=false`;
the API keeps the inverse. Database claim locks make the notification outbox safe
even if you run more than one runner, but running a single instance is simpler.

## Logs and health

| Host | Log location |
| --- | --- |
| API | `src/Rest.API/logs/rest-api-YYYYMMDD.log` (relative to the working directory) |
| Runner | `runner/App.Runner/logs/rest-runner-YYYYMMDD.log` |

Serilog also writes to the console. Files roll daily, retain 30 days and roll at
50 MB. Ship the files or stdout to your platform's log service; local files on an
ephemeral container disappear on restart.

- `GET /health` returns `Healthy` (anonymous, rate-limit exempt).
- `GET /` returns service name, environment and server time.
- Every response includes `X-Request-Id`. Quote it when investigating; the same id
  is in the audit row and the Serilog request log.

## AWS setup

### Credentials

Use an IAM role or instance profile for the API. Static keys are supported but
must be encrypted and rotated; prefer roles.

An example least-privilege policy for the S3 storage:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "ListBucket",
      "Effect": "Allow",
      "Action": ["s3:ListBucket"],
      "Resource": "arn:aws:s3:::rest-api-production-documents"
    },
    {
      "Sid": "ObjectAccess",
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject",
        "s3:AbortMultipartUpload"
      ],
      "Resource": "arn:aws:s3:::rest-api-production-documents/documents/*"
    },
    {
      "Sid": "KmsForSseKms",
      "Effect": "Allow",
      "Action": ["kms:Encrypt", "kms:Decrypt", "kms:GenerateDataKey"],
      "Resource": "arn:aws:kms:ap-south-1:123456789012:key/your-key-id"
    }
  ]
}
```

The KMS statement is only needed when `Storage:KmsKeyId` is set. Presigned URL
generation is local signing; the party consuming the URL needs `s3:GetObject`.

### S3 buckets

Terraform:

```bash
cd infra/terraform
cp terraform.tfvars.example terraform.tfvars
# edit project_name, region, CORS origins
terraform init
terraform plan
terraform apply
terraform output
```

It creates one bucket per environment with:

- Public access fully blocked
- Bucket-owner-enforced ownership
- Versioning enabled
- SSE-S3 by default, SSE-KMS when `kms_key_arn` is set
- Lifecycle: abort incomplete multipart uploads after 7 days, expire noncurrent
  versions after 90 days
- CORS limited to configured origins
- A bucket policy that denies non-TLS requests

Set `Storage:BucketName` to the matching output for each environment.

AWS CLI alternative:

```bash
export AWS_PROFILE=your-profile
PROJECT_NAME=rest-api AWS_REGION=ap-south-1 \
CORS_ORIGINS="https://app.example.com" \
./scripts/create-s3-buckets.sh development testing production
```

### RDS SQL Server

Terraform can create RDS when `create_rds=true`; you must supply
`rds_subnet_ids` and `rds_security_group_ids` for private subnets. The instance
is not publicly accessible, storage is encrypted, backups are retained for 7
days, and deletion protection is on.

After creation:

```json
"Database": {
  "Provider": "AwsRds",
  "ConnectionString": "enc:v1:...",
  "InitializeOnStartup": false,
  "SeedOnStartup": false
}
```

Apply your schema (kept outside this repository) from a CI job, a bastion, or a one-off task
inside the VPC. The API does not auto-create schema outside PreDevelopment.

Never commit Terraform state (`*.tfstate` is ignored). For team use, configure a
remote backend (S3 + DynamoDB locking) in `versions.tf`.

## Deployment

Recommended shape:

```
Internet → load balancer / ingress (TLS)
             ├── API instances (2+) ─── RDS SQL Server
             │        └── S3, ClamAV, SMTP
             └── Runner instance (1) ── RDS SQL Server
```

- **API**: stateless, scale horizontally behind a load balancer. Set
  `ASPNETCORE_ENVIRONMENT=Production`, `Security__MasterKey` and the encrypted
  configuration values. If you scale beyond one instance, add a SignalR backplane
  (Azure SignalR Service or Redis) and replace the in-memory dashboard buffer.
- **Runner**: one always-on instance (container/app service) with the same
  configuration as the API. It needs database access and SMTP access.
- **Database**: provision RDS, apply schema before deploying the API, keep
  `InitializeOnStartup=false`.
- **Storage**: one bucket per environment; grant the instance role access.
- **Virus scanning**: run clamd in the VPC or on the host. `VirusScan:Host` must
  be reachable from the API.
- **Logs**: mount a volume for `logs/` or collect stdout. Do not rely on
  container-local files.
- **Health**: point the load balancer at `/health`; it is anonymous and exempt
  from rate limiting.

The API listens on the URLs from `ASPNETCORE_URLS`/`ASPNETCORE_HTTP_PORTS`. In a
container, set `ASPNETCORE_HTTP_PORTS=8080` and expose that port.

Production starts with HSTS and HTTPS redirection. Terminate TLS at the load
balancer and forward `X-Forwarded-Proto`; `UseForwardedHeaders` is already
enabled.

## CI

`.github/workflows/ci.yml` runs on pushes to `main`/`master` and on pull requests:

1. Checkout
2. Install .NET `10.0.x`
3. `dotnet restore Rest.slnx`
4. `dotnet build Rest.slnx --no-restore --configuration Release`
5. `dotnet test Rest.slnx --no-build --configuration Release`
6. Upload `test-results.trx` as an artifact

The tests use in-memory configuration and do not need SQL Server or AWS. Add
services to the workflow only when you add integration tests that require them.

## GitHub and credential management

This template is published at
<https://github.com/sachindhole789-maker/projectQ>. The `origin` remote already
points there, so publishing your own copy means either pushing to that repository
(if you own it) or pointing `origin` at a new one.

To publish a fresh copy:

```bash
git init -b main
git add .
git status                 # review; ensure no secrets or keys are staged
git commit -m "Initial API template"
```

Create an empty repository in the GitHub UI, then choose one of:

**HTTPS with Git Credential Manager (recommended on Windows/macOS)**

```bash
git remote add origin https://github.com/<you>/<repo>.git
git push -u origin main
```

GCM opens a browser/token prompt the first time and stores the credential in the
OS credential store. Do not embed the token in the remote URL.

**SSH**

```bash
ssh-keygen -t ed25519 -C "you@example.com"
# add the public key to GitHub → Settings → SSH and GPG keys
git remote add origin git@github.com:<you>/<repo>.git
git push -u origin main
```

**gh CLI (optional)**

```bash
gh auth login
gh repo create <repo> --private --source . --remote origin --push
```

Use a fine-grained PAT with only the scopes you need if you use HTTPS tokens.
Never commit `.env`, `appsettings.Production.json`, `*.pfx`, `*.pem`, Terraform
state or the master key. They are covered by `.gitignore`, but review
`git status` before every push.

When a push fails with `403 Permission to <owner>/<repo> denied to <account>`,
the cached credential belongs to a different GitHub account. Inspect and clear it
before retrying:

```bash
git credential-manager github list
git credential-manager github logout <wrong-account>
git push -u origin main
```

If the repository belongs to another account, either log in as that account or
switch `origin` to a repository you own. Never force-push over someone else's
history to work around a credential mismatch.

After the first push, enable branch protection for `main`: require the CI check,
disallow force pushes, and require a pull request for changes.

## Scaling and reliability notes

- **SignalR**: add a backplane before running multiple API instances, otherwise a
  client connected to instance A will not receive events published on instance B.
- **Dashboard buffer**: `InMemoryRealtimeDashboardService` is per process. Replace
  it with a Redis/DB-backed implementation for a shared latest update.
- **Audit queue**: a bounded in-memory channel. A crash can lose the current
  batch; a sustained database outage drops rows after logging. Use a durable
  queue if audit completeness is mandatory.
- **Refresh tokens**: expired/revoked rows accumulate. Add a periodic delete job
  (`DELETE FROM RefreshTokens WHERE ExpiresAtUtc < ...` and revoked rows) to your
  maintenance routine.
- **Notifications**: the outbox uses claim leases (`LockMinutes`) and
  `MaxAttempts`; failed messages are retried until the limit and then marked
  failed. Alert on sustained failure counts.
- **Retention**: `AuditRetentionWorker` runs on the runner with
  `RetentionSweepHours`. `Database:AuditRetentionDays` and
  `AuditLog:RetentionDays` should agree.

## Troubleshooting

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| API starts in the wrong environment | launch profile sets `ASPNETCORE_ENVIRONMENT` | pass `--no-launch-profile` or change the profile |
| `MasterKey is required` / decryption fails | missing key, wrong key, or a value encrypted with another environment's key | export `Security__MasterKey`, re-encrypt with the matching key |
| SQL login fails | database not created, wrong password, TLS | create the DB, check `.env`, local connections need `TrustServerCertificate=True` |
| `Invalid object name` / missing procedure | schema not applied | apply your local schema file or enable `InitializeOnStartup` outside Production |
| Every call returns `401` | expired token, issuer/audience mismatch, signing key changed | refresh/login, compare `Jwt:*` values across hosts |
| `429` under normal load | limiter too tight or a shared NAT IP | raise `GlobalPermitLimit`/`UserPermitLimit`, inspect `Retry-After` |
| Browser CORS error | origin is not allowed in Production | add the exact scheme/host/port to `Cors:AllowedOrigins` |
| S3 uploads fail locally | wrong endpoint/path style/credentials | use `ServiceUrl=http://localhost:4566`, `ForcePathStyle=true`, test/test credentials |
| Uploads fail with a scan error | ClamAV is starting or unreachable | wait for the health check, verify host/port, or use `Provider=None` locally |
| Emails never arrive | runner not running or provider is `Log` | start the runner, set `Email:Provider=Smtp`, check Mailpit at :8025 |
| Upload returns 413 | size limit mismatch | keep `Storage:MaxFileSizeBytes`, Kestrel, form limits and the controller limit aligned |
| No audit rows | audit disabled, writer off, DB down, or queue full | check `AuditLog:Enabled`, `RunAuditWriter`, logs for "queue is full" |
| SignalR 401 | token missing on the hub URL | append `?access_token=<jwt>` for `/hubs/*` |
| Dashboard clients miss updates across instances | no backplane | add Azure SignalR/Redis and a shared dashboard store |
| `dotnet run` ignores my environment | launch profile precedence | `--no-launch-profile` |
| Tests fail with SQL connection errors | host is using PreDevelopment instead of Testing | tests set `UseEnvironment("Testing")`; check your run configuration |
