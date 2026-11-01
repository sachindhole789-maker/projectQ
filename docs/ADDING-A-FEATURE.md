# Adding a feature

This walkthrough adds a read-only `GetInvoiceQuery` and a `CreateInvoiceCommand`
using the same conventions as the example `Student` feature. The pattern is
always: **Domain interface → Infrastructure repository → Application feature file
→ API controller → tests**.

## 1. Add the domain model and DTO

`src/App.Domain/Entities/Invoice.cs`

```csharp
namespace App.Domain.Entities;

public sealed class Invoice
{
    public Guid InvoiceId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

`src/App.Domain/Dtos/Invoices/InvoiceDto.cs` (only needed if the column shape
differs from the entity):

```csharp
namespace App.Domain.Dtos.Invoices;

public sealed record InvoiceDto(
    Guid InvoiceId,
    string CustomerId,
    string Number,
    decimal Amount,
    DateTime CreatedAtUtc);
```

## 2. Add the repository interface

`src/App.Domain/Interfaces/Persistence/IInvoiceRepository.cs`

```csharp
using App.Domain.Dtos.Invoices;
using App.Domain.Entities;

namespace App.Domain.Interfaces.Persistence;

public interface IInvoiceRepository
{
    Task<InvoiceDto?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceDto>> GetByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
```

## 3. Add the stored procedures

Schema, tables and queries are **not published in this repository**. Create the
`Invoices` table and its procedures in your private schema file
(`db/sqlserver/001_Schema.sql` locally, git-ignored) or apply them with your
normal database deployment process.

The repository calls below expect `sp_Invoice_GetById`,
`sp_Invoice_GetByCustomer` and `sp_Invoice_Create` with the parameters shown in
the repository code. Keep your script idempotent (`IF OBJECT_ID ...`,
`CREATE OR ALTER`).

If the database is already provisioned and `InitializeOnStartup` is off, apply the
new batches manually. Keep the script idempotent (`IF OBJECT_ID ...`,
`CREATE OR ALTER`).

## 4. Add the Dapper repository

`src/App.Infrastructure/Persistence/Repositories/InvoiceRepository.cs`

```csharp
using App.Domain.Dtos.Invoices;
using App.Domain.Entities;
using App.Domain.Interfaces.Persistence;

namespace App.Infrastructure.Persistence.Repositories;

public sealed class InvoiceRepository(IDbManager dbManager) : IInvoiceRepository
{
    public Task<InvoiceDto?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        dbManager.QuerySingleOrDefaultAsync<InvoiceDto>(
            "sp_Invoice_GetById",
            new { InvoiceId = invoiceId },
            cancellationToken);

    public Task<IReadOnlyList<InvoiceDto>> GetByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default) =>
        dbManager.QueryAsync<InvoiceDto>(
            "sp_Invoice_GetByCustomer",
            new { CustomerId = customerId },
            cancellationToken);

    public async Task<Guid> CreateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await dbManager.ExecuteAsync("sp_Invoice_Create", new
        {
            invoice.InvoiceId,
            invoice.CustomerId,
            invoice.Number,
            invoice.Amount,
            invoice.CreatedAtUtc,
        }, cancellationToken);

        return invoice.InvoiceId;
    }
}
```

Register it in `src/App.Infrastructure/Extensions/DependencyInjection.cs`:

```csharp
services.AddScoped<IInvoiceRepository, InvoiceRepository>();
```

Dapper maps columns to constructor/property names. Use `InvoiceDto` when the
query shape differs from the entity. `decimal` maps to `DECIMAL(18,2)` as-is.

## 5. Add the feature file

`src/App.Application/Features/V1/Invoices/GetInvoice.cs`

```csharp
using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Dtos.Invoices;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using FluentValidation;

namespace App.Application.Features.V1.Invoices;

public sealed record GetInvoiceQuery(Guid InvoiceId) : IQuery<GetInvoiceResponse>;

public sealed record GetInvoiceResponse(
    Guid InvoiceId,
    string CustomerId,
    string Number,
    decimal Amount,
    DateTime CreatedAtUtc);

public sealed class GetInvoiceQueryValidator : AbstractValidator<GetInvoiceQuery>
{
    public GetInvoiceQueryValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
    }
}

public sealed class GetInvoiceQueryHandler(
    IInvoiceRepository invoiceRepository,
    IUserInfoProvider userInfo) : IQueryHandler<GetInvoiceQuery, GetInvoiceResponse>
{
    public async Task<Result<GetInvoiceResponse>> Handle(
        GetInvoiceQuery query,
        CancellationToken cancellationToken)
    {
        if (userInfo.UserId is null || userInfo.UserId == Guid.Empty)
        {
            return Result<GetInvoiceResponse>.Failure(AppStatusCodes.Unauthorized, "UserNotFound");
        }

        var invoice = await invoiceRepository.GetByIdAsync(query.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result<GetInvoiceResponse>.Failure(AppStatusCodes.NotFound, "Invoice not found.");
        }

        return Result<GetInvoiceResponse>.Success(new GetInvoiceResponse(
            invoice.InvoiceId,
            invoice.CustomerId,
            invoice.Number,
            invoice.Amount,
            invoice.CreatedAtUtc), "Invoice retrieved successfully");
    }
}
```

`src/App.Application/Features/V1/Invoices/CreateInvoice.cs`

```csharp
using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Domain.Entities;
using App.Domain.Interfaces.Infrastructure;
using App.Domain.Interfaces.Persistence;
using FluentValidation;

namespace App.Application.Features.V1.Invoices;

public sealed record CreateInvoiceCommand(
    string CustomerId,
    string Number,
    decimal Amount) : ICommand<CreateInvoiceResponse>;

public sealed record CreateInvoiceResponse(Guid InvoiceId);

public sealed class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Number).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class CreateInvoiceCommandHandler(
    IInvoiceRepository invoiceRepository,
    IUserInfoProvider userInfo,
    TimeProvider timeProvider) : ICommandHandler<CreateInvoiceCommand, CreateInvoiceResponse>
{
    public async Task<Result<CreateInvoiceResponse>> Handle(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        if (userInfo.UserId is null || userInfo.UserId == Guid.Empty)
        {
            return Result<CreateInvoiceResponse>.Failure(AppStatusCodes.Unauthorized, "UserNotFound");
        }

        var invoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            CustomerId = command.CustomerId.Trim(),
            Number = command.Number.Trim(),
            Amount = command.Amount,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };

        await invoiceRepository.CreateAsync(invoice, cancellationToken);

        return Result<CreateInvoiceResponse>.Success(
            new CreateInvoiceResponse(invoice.InvoiceId),
            "Invoice created successfully");
    }
}
```

No DI registration is needed: `AddApplication()` scans the Application assembly
for validators and handlers (`includeInternalTypes: true`).

### Paged feature

When the procedure returns rows plus a count, use `Result.SuccessPaginated` and
`ToPaginatedResult`:

```csharp
var paged = await repository.GetPagedAsync(filter, cancellationToken);
var mapped = paged.ToPaginatedResult(row => new InvoiceListItem(...));

return Result<GetInvoicesPagedResponse>.SuccessPaginated(
    new GetInvoicesPagedResponse(mapped.Items),
    mapped.Pagination,
    "Data retrieved successfully");
```

Pass `countProcedureName` to `QueryPagedAsync` only when the count is a separate
procedure; the default path expects the count as the second result grid.

## 6. Add the controller

`src/Rest.API/Controllers/InvoicesController.cs`

```csharp
using App.Application.Abstractions;
using App.Application.Common.Responses;
using App.Application.Features.V1.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rest.API.Extensions;

namespace Rest.API.Controllers;

public sealed record CreateInvoiceRequest(string? CustomerId, string? Number, decimal Amount);

[ApiController]
[Route("api/v1/invoices")]
[Produces("application/json")]
[Authorize]
[ResponseType<GetInvoiceResponse>]
[ResponseType<CreateInvoiceResponse>]
public sealed class InvoicesController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet("{invoiceId:guid}")]
    public async Task<IActionResult> GetById(Guid invoiceId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.ExecuteQuery(new GetInvoiceQuery(invoiceId), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateInvoiceRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<CreateInvoiceResponse>
                .Failure(AppStatusCodes.BadRequest, "Request body is required.")
                .ToActionResult();
        }

        var result = await dispatcher.ExecuteCommand(
            new CreateInvoiceCommand(
                request.CustomerId ?? string.Empty,
                request.Number ?? string.Empty,
                request.Amount),
            cancellationToken);

        return result.ToActionResult();
    }
}
```

Rules:

- Keep actions thin: build the query/command, call the dispatcher, return
  `result.ToActionResult()`.
- Always return `IActionResult`; never return a bare model.
- Add one `[ResponseType<T>]` per response payload.
- Use nullable request record properties and coalesce to `string.Empty` so the
  validator produces useful `422` messages instead of a model-binding exception.
- Put `[Authorize]` on the controller and a policy on individual actions if
  needed (`[Authorize(Policy = "Admin")]`).

## 7. Add tests

Validators and handlers are plain classes, so test them directly:

```csharp
[Fact]
public void CreateInvoice_Rejects_Zero_Amount()
{
    var result = new CreateInvoiceCommandValidator()
        .Validate(new CreateInvoiceCommand("CUST-1", "INV-1", 0));

    Assert.False(result.IsValid);
}
```

For a full HTTP test, add a case to `tests/App.Tests/Api/ApiSmokeTests.cs` or
create a new `WebApplicationFactory<Program>` fixture. The existing fixture boots
the real host in `Testing` with rate limiting, audit and background workers
disabled, so no external services are required.

## 8. Checklist

- [ ] Entity/DTO in Domain
- [ ] Repository interface in Domain
- [ ] Stored procedures added to your local, git-ignored schema file (or applied manually)
- [ ] Dapper repository in Infrastructure
- [ ] Repository registered in `AddPersistence`
- [ ] Query/command, response, validator and handler in one feature file
- [ ] Controller action with `[ResponseType<T>]` and `ToActionResult()`
- [ ] Failure paths return `Result.Failure` with an `AppStatusCodes` value
- [ ] Pagination uses `SuccessPaginated` when applicable
- [ ] Validators cover required fields and bounds
- [ ] Unit test for validation/handler; API test for the happy path
- [ ] `dotnet build Rest.slnx` and `dotnet test Rest.slnx` pass

## Where things go if the feature is cross-cutting

| Need | Where |
| --- | --- |
| New external provider (payment, SMS) | Interface in Domain/Application, implementation in Infrastructure, register in `AddInfrastructure` |
| New configuration section | `App.Application/Common/Options`, bind and validate in `AddInfrastructure` |
| New background job | `App.Infrastructure/BackgroundJobs`; choose the host with `BackgroundJobs:*` |
| New realtime event | Call `IRealtimeDashboardService.PublishAsync` or `IRealtimePublisher` |
| New audit exclusion | `AuditLog:ExcludedPaths` / `SensitiveJsonKeys` |
| New auth policy | `services.AddAuthorization` in `ApiServiceCollectionExtensions` |
