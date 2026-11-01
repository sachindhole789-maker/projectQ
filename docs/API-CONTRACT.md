# API contract

## Conventions

- Base URL in examples: `http://localhost:5142` (PreDevelopment).
- Controller and envelope JSON use **PascalCase** property names. SignalR payloads
  use the same serializer settings. The two exceptions are the minimal-API root
  endpoint (`/`, camelCase) and the plain-text `/health` response.
- Dates are UTC (`...Utc` suffix in field names).
- Enums are serialized as strings (`"Email"`), but numeric values are also
  accepted on input.
- Every endpoint returns **HTTP 200**. Read `StatusCode` in the body for the
  real outcome.
- The request id is echoed in `RequestId`, the `X-Request-Id` response header,
  and the `X-Request-Id` request header when the client supplies one.

## Response envelope

```json
{
  "RequestId": "0HN8R7A2QK9V3:00000001",
  "Data": {},
  "Data2": null,
  "Pagination": null,
  "Pagination2": null,
  "Message": "Retrieved successfully",
  "Status": "SUCCESS",
  "StatusCode": "200"
}
```

| Field | Meaning |
| --- | --- |
| `RequestId` | Correlates logs, audit rows and the response |
| `Data` | Primary payload (object, list, or `null` on failure) |
| `Data2` | Optional second grid (for procedures returning two grids) |
| `Pagination` | Metadata for `Data` when the endpoint is paged |
| `Pagination2` | Metadata for `Data2` |
| `Message` | Human-readable outcome |
| `Status` | `SUCCESS`, `FAILURE` or `ERROR` |
| `StatusCode` | String code listed below |

### Status codes

| `StatusCode` | `Status` | Typical cause |
| --- | --- | --- |
| `"200"` | `SUCCESS` | Success |
| `"400"` | `FAILURE` | Missing/invalid request body |
| `"401"` | `FAILURE` | Missing/expired token, bad credentials |
| `"403"` | `FAILURE` | Authenticated but not allowed |
| `"404"` | `FAILURE` | Entity not found |
| `"409"` | `FAILURE` | Duplicate email or `CustomerId` |
| `"422"` | `FAILURE` | Validation or file-content error |
| `"429"` | `FAILURE` | Rate limit; also sets `Retry-After` |
| `"500"` | `ERROR` | Unhandled server error |

Failures expose an empty collection in `Data` when the payload type is a
collection, otherwise `null`.

### Pagination metadata

```json
{
  "TotalRecords": 143,
  "PageSize": 20,
  "PageNumber": 2,
  "TotalPages": 8,
  "HasNextPage": true,
  "HasPreviousPage": true
}
```

## Authentication

1. `POST /api/v1/auth/login` returns an access token and a refresh token.
2. Send `Authorization: Bearer <accessToken>` on protected endpoints.
3. When the access token expires, call `POST /api/v1/auth/refresh`. The old
   refresh token is revoked and a new pair is returned (rotation).
4. Store tokens in secure storage on the client. Never put the refresh token in
   a URL.

Access tokens contain `sub`, `nameidentifier`, `email`, `name`, `role`, `jti`
and `iat`. The API maps `sub` to `ClaimTypes.NameIdentifier` and `role` to
`ClaimTypes.Role` internally (`MapInboundClaims = false`; `UserInfoProvider`
handles both claim spellings).

`POST /api/v1/auth/register` requires the `Admin` role.

## Endpoints

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/` | anonymous | Service name, environment, server time |
| GET | `/health` | anonymous | Health check (`Healthy`) |
| POST | `/api/v1/auth/login` | anonymous, `auth` rate limit | Login |
| POST | `/api/v1/auth/refresh` | anonymous, `auth` rate limit | Rotate refresh token |
| POST | `/api/v1/auth/register` | `Admin` | Create a user |
| GET | `/api/v1/students/{customerId}` | any user | Student by customer id |
| GET | `/api/v1/students` | any user | Paged/filtered students |
| POST | `/api/v1/students` | any user | Create a student |
| POST | `/api/v1/files` | any user | Upload a document (multipart) |
| GET | `/api/v1/files/{fileId}` | owner or `Admin` | File metadata |
| GET | `/api/v1/files/{fileId}/content` | owner or `Admin` | Binary download |
| GET | `/api/v1/dashboard/snapshot` | any user | Metrics and latest dashboard update |
| POST | `/api/v1/dashboard/publish` | any user | Push an event to dashboard clients |
| POST | `/api/v1/notifications` | any user | Queue email or send in-app |
| WS | `/hubs/dashboard` | JWT | Dashboard updates |
| WS | `/hubs/notifications` | JWT | Per-user notifications |

### Login

```bash
curl -s http://localhost:5142/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"Email":"admin@example.com","Password":"YOUR_ADMIN_PASSWORD"}'
```

```json
{
  "RequestId": "...",
  "Data": {
    "AccessToken": "eyJ...",
    "AccessTokenExpiresAtUtc": "2026-11-01T13:20:00Z",
    "RefreshToken": "K3f...",
    "RefreshTokenExpiresAtUtc": "2026-11-15T13:00:00Z",
    "UserId": "0d7f...",
    "DisplayName": "Administrator",
    "Roles": ["Admin", "User"]
  },
  "Message": "Login successful",
  "Status": "SUCCESS",
  "StatusCode": "200"
}
```

Unknown email and wrong password return the same `401` message
(`Invalid email or password.`) to avoid account enumeration.

### Refresh

```bash
curl -s http://localhost:5142/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"RefreshToken":"K3f..."}'
```

Returns the same `LoginResponse` shape. A replayed or expired refresh token
returns `401`.

### Register (Admin)

```bash
curl -s http://localhost:5142/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"Email":"teacher@example.com","Password":"Str0ng!Passw0rd","DisplayName":"Teacher"}'
```

Password rules: at least 12 characters, one uppercase, one lowercase, one digit
and one symbol. Duplicate email returns `409`.

### Get student

```bash
curl -s http://localhost:5142/api/v1/students/CUST-1001 \
  -H "Authorization: Bearer $TOKEN"
```

```json
{
  "RequestId": "...",
  "Data": { "Name": "Asha Rao", "Email": "asha@example.com", "Mobile": "9999999999", "City": "Pune" },
  "Message": "Retrieved successfully",
  "Status": "SUCCESS",
  "StatusCode": "200"
}
```

Missing student returns `404` with `Message: "StudentNotFound"`.

### Paged students

```bash
curl -s "http://localhost:5142/api/v1/students?name=Asha&city=Pune&pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer $TOKEN"
```

`pageNumber` must be greater than 0; `pageSize` must be between 1 and 200.

```json
{
  "Data": {
    "Items": [
      {
        "StudentId": "6c9d...",
        "CustomerId": "CUST-1001",
        "Name": "Asha Rao",
        "Email": "asha@example.com",
        "Mobile": "9999999999",
        "City": "Pune"
      }
    ]
  },
  "Pagination": {
    "TotalRecords": 1,
    "PageSize": 20,
    "PageNumber": 1,
    "TotalPages": 1,
    "HasNextPage": false,
    "HasPreviousPage": false
  },
  "Message": "Data retrieved successfully",
  "Status": "SUCCESS",
  "StatusCode": "200"
}
```

### Create student

```bash
curl -s http://localhost:5142/api/v1/students \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"CustomerId":"CUST-1001","Name":"Asha Rao","Email":"asha@example.com","Mobile":"9999999999","City":"Pune"}'
```

`Mobile` accepts 7–15 digits with an optional leading `+`. Duplicate
`CustomerId` returns `409`.

### Upload a document

```bash
curl -s http://localhost:5142/api/v1/files \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@invoice.pdf"
```

The multipart field must be named `file`. Limits:

- Extension allow-list from `Storage:AllowedExtensions`
  (`.pdf`, `.docx`, `.xlsx`, `.pptx`, `.doc`, `.xls`, `.ppt`, `.txt`, `.csv`).
- `Storage:MaxFileSizeBytes` (25 MB by default) and a 50 MB request cap.
- Content must match the extension: `%PDF-` signature, OOXML ZIP layout,
  OLE signature, or valid UTF-8 text. A renamed binary is rejected with `422`.
- ClamAV scan when `VirusScan:Provider=ClamAV`; failures are rejected.

```json
{
  "Data": {
    "FileId": "0f0c...",
    "FileName": "invoice.pdf",
    "ContentType": "application/pdf",
    "SizeBytes": 18234,
    "Sha256Hash": "A1B2...",
    "VirusScanStatus": "Clean",
    "DownloadUrl": "https://...presigned..."
  },
  "Message": "File uploaded successfully",
  "StatusCode": "200"
}
```

### File metadata and download

`GET /api/v1/files/{fileId}` returns metadata and either a presigned S3 URL or
the local API path. Only the uploader or an `Admin` can read it.

`GET /api/v1/files/{fileId}/content` is the **only binary response**:

- Success: `200 OK` with the file stream, `Content-Type`, `Content-Disposition`
  and range support.
- Failure: the normal JSON envelope with HTTP 200.

The file must be `Clean`; otherwise the content endpoint returns `422`.

### Dashboard snapshot

```bash
curl -s http://localhost:5142/api/v1/dashboard/snapshot \
  -H "Authorization: Bearer $TOKEN"
```

```json
{
  "Data": {
    "Metrics": {
      "TotalRequests": 42,
      "FailedRequests": 1,
      "RequestsLastMinute": 5,
      "AverageDurationMs": 12.34,
      "CapturedAtUtc": "2026-11-01T12:00:00Z"
    },
    "LatestUpdate": {
      "EventName": "orders.live",
      "Payload": { "count": 12 },
      "PublishedAtUtc": "2026-11-01T11:59:58Z"
    }
  },
  "StatusCode": "200"
}
```

### Publish dashboard data

```bash
curl -s http://localhost:5142/api/v1/dashboard/publish \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"EventName":"orders.live","Payload":{"count":12,"region":"west"}}'
```

`EventName` is 3–100 characters. The payload is any JSON object. Every
connected dashboard client receives the `dashboard.update` event.

### Send a notification

```bash
curl -s http://localhost:5142/api/v1/notifications \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"Channel":"Email","Recipient":"asha@example.com","Subject":"Welcome","Body":"Hello Asha"}'
```

| Channel | Behaviour |
| --- | --- |
| `Email` | Row is queued; the runner dispatches it through SMTP/log |
| `InApp` | Published immediately to `/hubs/notifications` for the current user |

For `Email`, `Recipient` must be a valid address. The response contains the
queue id:

```json
{ "Data": { "NotificationId": "d4f1..." }, "Message": "Notification queued", "StatusCode": "200" }
```

## Validation and errors

- FluentValidation runs inside the dispatcher before the handler. All messages
  are joined with `" | "` and returned with `422`.
- Model binding errors (bad JSON, wrong types) are rewritten to `422` by the
  API layer.
- A missing body on endpoints that require one returns `400`.
- `AppException` thrown by infrastructure is converted to its status code and
  message.
- Anything else becomes `500` with `Status: "ERROR"` and a generic message; the
  exception details stay in the logs.
- Cancellation when the client disconnects is not written to the response.

Rate-limited requests get `Counter`-style headers from ASP.NET Core, a
`Retry-After` header and the usual envelope:

```json
{ "Message": "Too many requests. Please wait and retry.", "Status": "FAILURE", "StatusCode": "429" }
```

## SignalR

### Connect

Both hubs require a JWT. Browsers and Flutter web cannot set an `Authorization`
header on the WebSocket handshake, so pass the token in the query string:

```
/hubs/dashboard?access_token=<accessToken>
/hubs/notifications?access_token=<accessToken>
```

### Dashboard events

| Event | Arguments |
| --- | --- |
| `dashboard.update` | `eventName`, `payload`, `publishedAtUtc` |

The hub replays the latest buffered update immediately on connect, so a
dashboard that joins late still renders current data. `DashboardBroadcastWorker`
also publishes a heartbeat snapshot every `BackgroundJobs:DashboardIntervalSeconds`.

### Notification events

| Event | Argument |
| --- | --- |
| `notification.received` | `{ NotificationId, Subject, Body, CreatedAtUtc }` |

In-app notifications are sent to `Clients.User(userId)`; when `userId` is null
they are broadcast to all connected clients.

### Flutter / Dart example

```dart
import 'package:signalr_netcore/signalr_client.dart';

final hub = HubConnectionBuilder()
    .withUrl('http://localhost:5142/hubs/dashboard?access_token=$accessToken')
    .withAutomaticReconnect()
    .build();

await hub.start();
hub.on('dashboard.update', (args) {
  final eventName = args?[0] as String?;
  final payload = args?[1];
  final publishedAt = args?[2];
  // update state
});
```

For mobile/desktop Flutter, plain HTTPS calls and SignalR work without CORS.
For Flutter web, the development server uses a random localhost port, which is
why the API allows any localhost port outside Production. Send credentials only
if you use cookies; Bearer tokens do not require `AllowCredentials`.

## CORS

- Preflight (`OPTIONS`) is answered for allowed origins and is exempt from rate
  limiting.
- Non-Production: any `localhost`, `127.0.0.1` or `[::1]` port is allowed, plus
  `Cors:AllowedOrigins`.
- Production: only `Cors:AllowedOrigins`, and `AllowLocalhostAnyPort` is ignored.
- All headers and methods are allowed; credentials are allowed when
  `Cors:AllowCredentials` is `true`.

## OpenAPI

Outside Production:

- JSON: `/openapi/v1.json`
- Scalar UI: `/scalar/v1`

`[ResponseType<T>]` documents payload types; the wire format is still the
envelope, which is intentional and documented here.
