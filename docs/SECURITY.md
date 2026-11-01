# Security

This document describes what the template does, what you must configure, and the
choices you should revisit for your deployment.

## 1. Secret encryption (`enc:v1:`)

Sensitive configuration values are encrypted with AES-256-GCM using a
per-environment master key.

Format:

```
enc:v1:{base64 nonce}:{base64 tag}:{base64 ciphertext}
```

- 12-byte random nonce per encryption (never reused).
- 16-byte authentication tag; tampered values fail to decrypt.
- The version prefix allows a future format migration.

`ISecretResolver` returns plaintext when a value is not encrypted, so you can
use mixed configuration during a migration.

Values that support encryption:

| Configuration | Where it is resolved |
| --- | --- |
| `Database:ConnectionString` | `DbConnectionFactory` |
| `Jwt:SigningKey` | `JwtTokenService`, JWT bearer options |
| `Storage:AccessKey`, `Storage:SecretKey` | `S3ObjectStorageService` |
| `Email:Password` | `SmtpEmailSender` |
| `Seed:AdminPassword` | `DatabaseBootstrapper` |

### Master key handling

- `Security:MasterKey` is **bootstrap** configuration. It cannot encrypt itself.
- No master key is committed. Supply a different generated key per environment
  through a local, git-ignored override or a secret manager.
- In Production supply it via `Security__MasterKey`, AWS Secrets Manager, Azure
  Key Vault or your platform's secret store. Never commit it.
- The key must decode to 16, 24 or 32 bytes; 32 bytes (AES-256) is recommended.
- The API validates the key shape at startup (`ValidateOnStart`).

### Rotation

There is no key id in `enc:v1:`; the active master key is implicit. Rotation is a
coordinated operation:

1. Generate a new key: `dotnet run --project runner/App.Runner -- generate-key 32`.
2. Re-encrypt every `enc:v1:` value with the new key:
   ```bash
   DOTNET_ENVIRONMENT=Production \
   dotnet run --project runner/App.Runner -- encrypt "<plaintext>"
   ```
3. Update the secrets store and restart API and runner together.
4. If the JWT signing key changed, all access tokens become invalid; clients
   refresh or log in again.

Keep the old key available (but unused) until every host is updated. Do not
rotate the master key and the signing key in separate uncoordinated steps unless
you can tolerate the invalidations.

## 2. Password storage

- Passwords are hashed with PBKDF2-HMAC-SHA256:
  `PBKDF2-SHA256$iterations$salt$hash`.
- 16-byte random salt per password; 32-byte derived key.
- 210,000 iterations by default; values below 100,000 are clamped.
- Comparison uses `CryptographicOperations.FixedTimeEquals`.
- `NeedsRehash` detects hashes created with fewer iterations; call it on login if
  you raise the iteration count in the future.
- Passwords are never encrypted, logged or stored in plaintext.

Password policy on registration: minimum 12 characters with upper, lower, digit
and symbol. Adjust `RegisterUserCommandValidator` to match your policy.

Seed passwords (`Seed:AdminPassword`) are read once, hashed and never persisted in
plaintext. Leave it empty to skip seeding.

## 3. Tokens

- Access tokens are JWT HS256, signed with a key of at least 32 characters.
- Validation checks issuer, audience, lifetime, signature and the HS256 algorithm
  only; `ValidateIssuerSigningKey` is on.
- Clock skew is 30 seconds (default framework behaviour is 5 minutes).
- `MapInboundClaims = false`; the API resolves both `sub`/`nameidentifier` and
  `role`/`roles` claim spellings.
- Refresh tokens are 64 random bytes, returned once. Only the SHA-256 hex hash is
  stored. Every refresh revokes the old token and issues a new pair (rotation).
- Login returns the same error for unknown users and wrong passwords.
- There is no server-side access-token denylist. Revocation takes effect when the
  access token expires (20 minutes by default). Shorten
  `Jwt:AccessTokenMinutes` if you need faster revocation.

If you need multi-device logout, add a `RevokedTokens` table keyed by `jti` and
check it in a JWT event or authorization handler.

## 4. Transport and host hardening

- Production enables HSTS and HTTPS redirection.
- `UseForwardedHeaders` honours `X-Forwarded-For`/`X-Forwarded-Proto` so client
  IPs and scheme are correct behind a load balancer. Restrict trusted proxies in
  your platform if you are not behind a controlled ingress.
- Kestrel hides the `Server` header, limits request body size to
  `Storage:MaxFileSizeBytes + 1 MB`, sets a 30-second header timeout and a
  2-minute keep-alive.
- The default request size limit is also enforced by the framework and by
  `[RequestSizeLimit]` on upload.
- `UseSerilogRequestLogging`, request ids and audit rows make incidents
  traceable without logging secrets.

## 5. Authorization

- All feature controllers use `[Authorize]`; only health, the root endpoint and
  login/refresh are anonymous.
- `Admin` policy requires the `Admin` role.
- File reads are restricted to the uploader or an `Admin` in `FileAccess.CanRead`.
  Add tenant/project scoping there if your data is multi-tenant.
- Authentication challenges and forbidden results are returned as HTTP 200
  envelopes (`401`/`403` in `StatusCode`). This is a client-contract decision, not
  a relaxation of the checks themselves.

## 6. Rate limiting

- Chained global limiter: fixed per-IP window (burst guard) + sliding per-user
  window (fairer for authenticated clients).
- Dedicated `auth` fixed window on login/refresh to slow credential stuffing.
- `QueueLimit = 0` by default: excess requests are rejected immediately rather
  than waiting and consuming server resources.
- OPTIONS, `/hubs` and `/health` are exempt.
- Rejections set `Retry-After` and return the standard envelope with `429`.

Tuning advice is in [CONFIGURATION.md](CONFIGURATION.md#ratelimiting).

## 7. CORS

- Non-Production: any localhost/loopback port plus explicit origins. This is what
  lets Flutter web dev servers with random ports call the API.
- Production: only `Cors:AllowedOrigins`; `AllowLocalhostAnyPort` is ignored.
- Credentials are allowed when `Cors:AllowCredentials=true`. If you do not use
  cookies, set it to `false` to reduce exposure.
- Never add a wildcard origin together with credentials; the browser rejects it
  and it is unsafe.

## 8. File upload security

Uploads go through these layers before the bytes are stored:

1. **Name sanitisation** — path components stripped, invalid characters replaced,
   length capped at 180 characters.
2. **Extension allow-list** — `Storage:AllowedExtensions`.
3. **Size** — `Storage:MaxFileSizeBytes` and the 50 MB request cap.
4. **Content detection** — the extension must match the bytes:
   - PDF: `%PDF-`
   - OOXML (`.docx/.xlsx/.pptx`): ZIP signature plus `[Content_Types].xml` and the
     expected part (`word/document.xml`, `xl/workbook.xml`,
     `ppt/presentation.xml`)
   - Legacy Office (`.doc/.xls/.ppt`): OLE compound-file signature
   - Text/CSV: valid UTF-8, no NUL bytes
5. **SHA-256** — computed and stored for integrity/dedup/forensics.
6. **Virus scan** — ClamAV INSTREAM, fail-closed. A scanner outage rejects the
   upload with a server error rather than accepting an unscanned file.
7. **Storage** — object key is server-generated
   (`{environment}/{yyyy}/{MM}/{guid}{ext}`); the client filename is never used as
   a path. S3 objects are private, SSE-S3/SSE-KMS by default, and delivered via
   short-lived presigned URLs. The local provider rejects path traversal and keys
   that escape the storage root.
8. **Metadata** — original name, detection result, size, hash, uploader,
   environment and scan status go to `dbo.UploadedFiles`.
9. **Access control** — download requires the uploader or `Admin`, and the scan
   status must be `Clean`.

Residual risks to consider:

- ClamAV signatures must be kept current (the Docker image updates itself; in
  production run a maintained clamd with freshclam).
- Content detection is signature-based, not a full sandbox. For high-risk
  documents, add a sandbox/CDR step.
- Presigned URLs are bearer capabilities until they expire; keep
  `PresignedUrlMinutes` short.

## 9. Audit logging

- Every audited request stores method, path, query string, user, IP, user agent,
  HTTP status, duration, request body and response body.
- Bodies are captured only for text/JSON content and truncated to
  `MaxBodyCharacters`.
- JSON keys listed in `SensitiveJsonKeys` are masked to `"***"` recursively,
  including nested objects and arrays. Invalid JSON is left untouched except for
  truncation.
- Binary responses (`/content`) are never captured.
- Excluded paths: `/health`, `/openapi`, `/scalar`, `/hubs`.
- Writes are queued in a bounded channel and persisted in batches. A full queue
  drops rows with a warning; a database outage logs and drops the batch rather
  than blocking requests. If audit completeness is a compliance requirement,
  ship the channel to a durable queue or write synchronously.
- Retention is enforced by `AuditRetentionWorker` using
  `AuditLog:RetentionDays` (also `Database:AuditRetentionDays` is available).

Never log tokens, passwords or full card numbers. The masker is a safety net, not
a license to put secrets in request bodies.

## 10. SQL injection and data access

- All data access uses Dapper parameters; stored procedure names are passed as
  command text with `CommandType.StoredProcedure`.
- Handlers never concatenate user input into SQL. If you add
  `ExecuteTextAsync`/`QueryTextAsync` calls, use parameters for every value.
- Do not build procedure names from user input.
- `ExecuteInTransactionAsync` is available for multi-statement operations; keep
  the transaction scope short.

## 11. Logging and error responses

- Serilog writes rolling daily text files (`logs/rest-api-*.log`, runner:
  `logs/rest-runner-*.log`) and the console.
- Request logging enriches with `RequestId`, `CorrelationId` and `ClientIp`.
- Unhandled exception details stay in logs; clients receive a generic `500`
  envelope.
- `AppException` messages are intentionally user-safe. Do not put credentials or
  internal paths in them.
- In Production, OpenAPI and Scalar are not mapped, reducing exposed surface.

## 12. Production checklist

- [ ] New master key per environment, stored outside Git
- [ ] Signing key rotated and encrypted
- [ ] Database connection string encrypted or injected as a secret
- [ ] SMTP password encrypted; TLS enabled for non-localhost
- [ ] `Security:MasterKey` available to both API and runner
- [ ] `Database:InitializeOnStartup=false`, `SeedOnStartup=false`
- [ ] `Cors:AllowLocalhostAnyPort=false`, explicit origins
- [ ] HTTPS terminated with a valid certificate; HSTS on
- [ ] `RateLimiting:Enabled=true`; auth limit 5–10
- [ ] ClamAV reachable and updated
- [ ] S3 bucket: public access blocked, versioning, SSE, deny insecure transport
- [ ] IAM role grants only the required bucket actions (no wildcard `s3:*`)
- [ ] Audit retention and log retention match policy
- [ ] Backups tested for RDS and S3 versioning
- [ ] Health checks and alerting on 5xx, 429 spikes, scanner failures and queue
      drops
- [ ] Refresh token rows purged after expiry (add a maintenance job if needed)

## Reporting a vulnerability

This is a template, not a hosted service. If you adapt it, add a `SECURITY.md`
policy with a private contact address and a response timeline for your users.
