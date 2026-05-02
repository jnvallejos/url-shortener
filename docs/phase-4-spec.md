# Phase 4 Spec — API Layer (TDD)

**Repo:** `url-shortener`
**Domain:** URL Shortener
**Stack:** .NET 9, ASP.NET Core Minimal APIs, Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore, xUnit, FluentAssertions, Microsoft.AspNetCore.Mvc.Testing
**Approach:** Test-Driven Development, granular commits, feature branch + PR
**Branch:** `phase-4-api`

---

## 1. Goal of Phase 4

Expose every Application use case as an HTTP endpoint, wire infrastructure end-to-end via configuration, and ship the API layer with rate limiting, OpenAPI documentation, structured logging, and functional tests.

At the end of Phase 4:
- `UrlShortener.Api` project compiles and runs as an ASP.NET Core Minimal API host
- `UrlShortener.Api.Tests` project passes 100% green (functional tests via `WebApplicationFactory<Program>` + SQLite in-memory)
- Six endpoints exposed: Create, Redirect, GetByCode, Disable, Enable, UpdateExpiration
- Errors from `Result<T>` mapped to appropriate HTTP statuses via `error.Code` (Phase 2 contract honored)
- Redirect endpoint protected by fixed-window rate limiting (100 req/min per IP, configurable)
- OpenAPI spec generated automatically from Minimal API metadata, served at `/openapi/v1.json`
- Scalar UI served at `/scalar/v1` for interactive exploration
- `appsettings.json` reads connection string and rate limit options
- Structured logging via `ILogger<T>` to console
- Domain code from Phase 1 is **untouched**
- Application code from Phase 2 is **untouched**
- Infrastructure code from Phase 3 is **untouched** (only consumed via `AddInfrastructure`)

---

## 2. Solution & Folder Structure

```
url-shortener/
├── docs/
│   ├── phase-1-spec.md
│   ├── phase-2-spec.md
│   ├── phase-3-spec.md
│   └── phase-4-spec.md
├── src/
│   ├── UrlShortener.Domain/                     (unchanged)
│   ├── UrlShortener.Application/                (unchanged)
│   ├── UrlShortener.Infrastructure/             (unchanged)
│   └── UrlShortener.Api/                        (NEW)
│       ├── Endpoints/
│       │   ├── ShortUrlsEndpoints.cs
│       │   └── RedirectEndpoint.cs
│       ├── ErrorMapping/
│       │   └── ErrorToHttpResultMapper.cs
│       ├── Configuration/
│       │   └── RateLimitingOptions.cs
│       ├── Contracts/
│       │   ├── CreateShortUrlContract.cs
│       │   ├── ShortUrlContract.cs
│       │   ├── UpdateExpirationContract.cs
│       │   └── ErrorResponse.cs
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── UrlShortener.Api.csproj
└── tests/
    ├── UrlShortener.Domain.Tests/                (unchanged)
    ├── UrlShortener.Application.Tests/           (unchanged)
    ├── UrlShortener.Infrastructure.Tests/        (unchanged)
    └── UrlShortener.Api.Tests/                   (NEW)
        ├── Endpoints/
        │   ├── CreateShortUrlEndpointTests.cs
        │   ├── RedirectEndpointTests.cs
        │   ├── GetShortUrlEndpointTests.cs
        │   ├── DisableShortUrlEndpointTests.cs
        │   ├── EnableShortUrlEndpointTests.cs
        │   └── UpdateExpirationEndpointTests.cs
        ├── ErrorMapping/
        │   └── ErrorToHttpResultMapperTests.cs
        ├── RateLimiting/
        │   └── RedirectRateLimitTests.cs
        ├── OpenApi/
        │   └── OpenApiDocumentTests.cs
        ├── TestSupport/
        │   └── ApiWebApplicationFactory.cs
        └── UrlShortener.Api.Tests.csproj
```

Add new projects to `UrlShortener.sln`. Reference graph: Api → Infrastructure → Application → Domain. Tests reference Api plus dependencies.

Root namespace: `UrlShortener.Api`.

---

## 3. Endpoints

All endpoints registered via extension methods on `IEndpointRouteBuilder` to keep `Program.cs` clean.

### 3.1 `POST /api/shorturls` — CreateShortUrl

**Request body (JSON):**
```json
{
  "originalUrl": "https://example.com/very/long/path",
  "customCode": "abc1234",
  "expiresAt": "2026-12-31T23:59:59Z"
}
```

`customCode` and `expiresAt` are optional (nullable in the contract).

**Success response: 201 Created**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "shortCode": "abc1234",
  "originalUrl": "https://example.com/very/long/path",
  "shortUrl": "https://localhost:5001/abc1234",
  "expiresAt": "2026-12-31T23:59:59Z",
  "createdAt": "2026-05-02T14:30:00Z"
}
```

`Location` header set to `/api/shorturls/{shortCode}`. The `shortUrl` field is built from the request's host (Phase 4 reads from `HttpRequest.Scheme + Host`).

**Error mapping:** see section 4.

### 3.2 `GET /{code}` — Redirect

**Path parameter:** `code` (7-char Base62 string).

**Success response: 302 Found**
- `Location` header set to the original URL
- Empty body
- Triggers `RegisterClick` and dispatches `ShortUrlClickedEvent` (which writes to `ClickAudits`)

**Error responses:**
- 400 if `code` fails `ShortCode.Create` validation
- 404 if no `ShortUrl` matches the code
- 410 if `ShortUrl` is disabled
- 410 if `ShortUrl` is expired

**Rate limiting:** see section 5.

**Note on the route conflict:** `GET /{code}` would catch `/api`, `/openapi/...`, `/scalar/...` and any other prefix. The route is registered with a constraint:
```csharp
.MapGet("/{code:length(7):regex(^[A-Za-z0-9]+$)}", ...)
```
This restricts the route to exactly 7 alphanumeric characters, naturally excluding API paths.

### 3.3 `GET /api/shorturls/{code}` — GetShortUrl

**Path parameter:** `code`.

**Success response: 200 OK**
```json
{
  "id": "550e8400-...",
  "shortCode": "abc1234",
  "originalUrl": "https://example.com/...",
  "shortUrl": "https://localhost:5001/abc1234",
  "expiresAt": "2026-12-31T23:59:59Z",
  "createdAt": "2026-05-02T14:30:00Z",
  "isEnabled": true,
  "clickCount": 42
}
```

This endpoint is read-only and does not register a click. Returns 404 even for disabled or expired URLs (admin must use this; expired/disabled state is part of the response, not a 410).

### 3.4 `POST /api/shorturls/{code}/disable` — DisableShortUrl

**Request body:** empty.

**Success response: 200 OK**
```json
{
  "code": "abc1234",
  "isEnabled": false
}
```

Idempotent (calling on already-disabled returns 200 with current state).

### 3.5 `POST /api/shorturls/{code}/enable` — EnableShortUrl

Symmetric to Disable. Same response shape with `"isEnabled": true`.

### 3.6 `PATCH /api/shorturls/{code}/expiration` — UpdateExpiration

**Request body (JSON):**
```json
{
  "newExpiresAt": "2027-01-01T00:00:00Z"
}
```

`newExpiresAt` can be `null` to clear the expiration.

**Success response: 200 OK**
```json
{
  "code": "abc1234",
  "expiresAt": "2027-01-01T00:00:00Z"
}
```

---

## 4. Error Mapping

### 4.1 `ErrorToHttpResultMapper`

Single static class. Maps `Error.Code` to an `IResult` with the appropriate HTTP status and body shape. The Application layer's contract (Phase 2 spec section 3.2) is honored: switch on `error.Code`, never on `error.Message` or instance equality.

```csharp
public static class ErrorToHttpResultMapper
{
    public static IResult ToHttpResult(Error error, string? traceId = null)
    {
        var response = new ErrorResponse(error.Code, error.Message, traceId);

        return error.Code switch
        {
            "OriginalUrl.Invalid"             => Results.BadRequest(response),
            "OriginalUrl.Required"            => Results.BadRequest(response),
            "ShortCode.Invalid"               => Results.BadRequest(response),
            "Validation.InvalidExpiration"    => Results.BadRequest(response),
            "ShortUrl.NotFound"               => Results.NotFound(response),
            "ShortUrl.Disabled"               => Results.Json(response, statusCode: 410),
            "ShortUrl.Expired"                => Results.Json(response, statusCode: 410),
            "ShortUrl.CodeAlreadyExists"      => Results.Conflict(response),
            "ShortUrl.CodeGenerationFailed"   => Results.Json(response, statusCode: 503),
            _                                 => Results.Json(response, statusCode: 500)
        };
    }
}
```

### 4.2 `ErrorResponse`

```csharp
public sealed record ErrorResponse(string Code, string Message, string? TraceId);
```

**Decisions:**
- **Switch on `error.Code`, not `error.Message`.** The Code is the stable contract; the Message can vary per call (e.g. `Errors.OriginalUrl.Invalid` produces different messages with the same Code).
- **Default case (`_`) returns 500.** Catch-all for new error codes that haven't been mapped yet. Defensive.
- **`TraceId` is optional in the response shape.** Endpoints pass `Activity.Current?.Id ?? HttpContext.TraceIdentifier`. Useful for clients to correlate with server logs.
- **No ProblemDetails.** Spec section C.1 of pre-flight closed this: ErrorResponse is simpler, sufficient for portfolio. If a future phase needs RFC 7807, the mapper is the only place that changes.

### 4.3 Endpoints calling the mapper

Each endpoint invokes the use case, gets a `Result<T>`, and either returns success or maps the error:

```csharp
var result = await useCase.ExecuteAsync(request, ct);
if (!result.IsSuccess)
    return ErrorToHttpResultMapper.ToHttpResult(result.Error, httpContext.TraceIdentifier);

return Results.Created($"/api/shorturls/{result.Value.ShortCode}", result.Value);
```

---

## 5. Rate Limiting

### 5.1 Strategy

`FixedWindow` rate limiter on the Redirect endpoint only. Other endpoints are unprotected (in production they'd be behind authentication, which is out of scope).

### 5.2 Configuration

`RateLimitingOptions` record bound from `appsettings.json`:

```csharp
public sealed record RateLimitingOptions
{
    public int PermitLimit { get; init; } = 100;
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
    public int QueueLimit { get; init; } = 0;
}
```

Bound via:
```csharp
builder.Services.Configure<RateLimitingOptions>(
    builder.Configuration.GetSection("RateLimiting"));
```

`appsettings.json`:
```json
{
  "RateLimiting": {
    "PermitLimit": 100,
    "Window": "00:01:00",
    "QueueLimit": 0
  }
}
```

### 5.3 Registration

```csharp
builder.Services.AddRateLimiter(options =>
{
    var rateLimitConfig = builder.Configuration
        .GetSection("RateLimiting")
        .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

    options.AddPolicy("redirect", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitConfig.PermitLimit,
                Window = rateLimitConfig.Window,
                QueueLimit = rateLimitConfig.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse(
                "RateLimit.Exceeded",
                "Too many requests; try again later",
                context.HttpContext.TraceIdentifier),
            ct);
    };
});
```

### 5.4 Applying the policy

```csharp
app.MapGet("/{code:length(7):regex(^[A-Za-z0-9]+$)}", RedirectHandler)
   .RequireRateLimiting("redirect");
```

**Decisions:**
- **Partition key is the remote IP.** Section D.3 of pre-flight closed this: simple, sufficient for portfolio. No `X-Forwarded-For` parsing.
- **`QueueLimit = 0`.** Excess requests are rejected immediately, not queued. Predictable behavior.
- **`AutoReplenishment = true`.** The window resets automatically without manual intervention.
- **`OnRejected` returns the same `ErrorResponse` shape** as the rest of the API. Consistent client experience.
- **Rate limit applies per-IP.** A second IP gets its own bucket. This is correct for redirect abuse mitigation.
- **Retry-After header** included when the lease provides it. RFC-compliant.

---

## 6. OpenAPI Documentation

### 6.1 Stack

`Microsoft.AspNetCore.OpenApi` (built-in to .NET 9) generates the OpenAPI document automatically from Minimal API metadata. `Scalar.AspNetCore` provides interactive UI.

### 6.2 Registration

```csharp
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "URL Shortener API",
            Version = "v1",
            Description = "Reference URL shortener built with Clean Architecture and TDD.",
            License = new OpenApiLicense { Name = "MIT" }
        };
        return Task.CompletedTask;
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("URL Shortener API");
        options.WithTheme(ScalarTheme.Default);
    });
}
```

**Decisions:**
- **OpenAPI and Scalar UI exposed only in Development.** Production deploys would not serve the spec publicly by default. For portfolio purposes, running locally always exposes them.
- **OpenAPI document at `/openapi/v1.json`.** Standard path.
- **Scalar UI at `/scalar/v1`.** Modern alternative to Swagger UI; better default styling, no extra config.

### 6.3 Endpoint metadata

Each endpoint chains metadata describing its behavior:

```csharp
endpoints.MapPost("/api/shorturls", CreateShortUrlHandler)
    .WithName("CreateShortUrl")
    .WithSummary("Create a new shortened URL")
    .WithDescription("Generates a 7-character Base62 code (or accepts a custom code) and persists the short URL.")
    .Produces<ShortUrlResponse>(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
    .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);
```

Apply the same pattern to all six endpoints. Status codes documented match the error mapping table in section 4.

### 6.4 Contracts

API contracts (request/response DTOs at the HTTP boundary) live in `UrlShortener.Api/Contracts/`. They are NOT the same as Application layer Request/Response records — they are the JSON-serializable shape exposed to HTTP clients.

```csharp
public sealed record CreateShortUrlContract(
    string OriginalUrl,
    string? CustomCode,
    DateTime? ExpiresAt);

public sealed record ShortUrlContract(
    Guid Id,
    string ShortCode,
    string OriginalUrl,
    string ShortUrl,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    bool IsEnabled,
    long ClickCount);

public sealed record UpdateExpirationContract(DateTime? NewExpiresAt);
```

**Decision: API contracts are separate from Application Request/Response records.** This insulates the HTTP shape from internal changes. If the Application's `CreateShortUrlResponse` adds a field, the API contract decides whether to expose it.

---

## 7. Logging

### 7.1 Strategy

Built-in `ILogger<T>` injected at the endpoint or use case level. Console sink via `builder.Logging.AddConsole()` (default in ASP.NET Core templates). No Serilog, no Application Insights, no external sinks.

### 7.2 What to log

The endpoint handlers log their entry and outcome at appropriate levels. Use cases do NOT log; logging is an infrastructure concern at the API edge.

**Information level:**
- Each request entering an endpoint with the route and key parameters (no body, no PII)
- Successful operations with the resulting short code

**Information level (4xx outcomes):**
- Errors mapped to 400/404/410/409 are operational, not exceptional. Logged at Information.

**Warning level:**
- `ShortUrl.CodeGenerationFailed` after retries exhausted
- Rate limit rejections (sampled, see section 7.3)

**Error level:**
- Unmapped error codes (the `_` case in the mapper) — indicates a bug
- Unhandled exceptions caught by ASP.NET Core's default exception handler

### 7.3 Rate limit log sampling

Rate limit rejections can be high volume during abuse. Logging every rejection floods logs. **Strategy: log at most one rejection per IP per minute via a small in-memory dictionary keyed by IP.**

For Phase 4, an even simpler approach is acceptable: log all rejections at Warning level. The dictionary-based sampling is mentioned here as a Phase 5+ improvement if log volume becomes a concern. Phase 4 ships the simple version.

### 7.4 Structured logging

Use message templates, not string interpolation:

```csharp
// CORRECT
logger.LogInformation("Created short URL {ShortCode} for origin {OriginalUrl}",
    response.ShortCode, request.OriginalUrl);

// INCORRECT
logger.LogInformation($"Created short URL {response.ShortCode} for origin {request.OriginalUrl}");
```

The structured form lets log analyzers index by `ShortCode` or `OriginalUrl`. Standard convention.

---

## 8. Configuration

### 8.1 `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=urlshortener;Username=postgres;Password=postgres"
  },
  "RateLimiting": {
    "PermitLimit": 100,
    "Window": "00:01:00",
    "QueueLimit": 0
  }
}
```

### 8.2 `appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

### 8.3 Reading in `Program.cs`

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Missing 'ConnectionStrings:DefaultConnection' in configuration.");

builder.Services.AddInfrastructure(connectionString);
```

**Decisions:**
- **Failing fast on missing connection string.** App should not start if it can't reach the DB. Throwing in `Program.cs` produces a clear startup error.
- **Development connection string in `appsettings.json`.** For portfolio use, anyone clones and runs against local Postgres. The `Development.json` only overrides logging.
- **No secrets management (User Secrets, Azure Key Vault, etc.)** for Phase 4. The `DefaultConnection` is a convention placeholder; a real production deploy would override via environment variables (`ConnectionStrings__DefaultConnection`), which ASP.NET Core handles natively. No code changes needed.

### 8.4 Database migration on startup

**Decision: do NOT auto-migrate on startup.** Migrations are an explicit deployment step, run via `dotnet ef database update` or via CI. Auto-migrate is a smell (unpredictable startup, race conditions in multi-instance deployments).

For development convenience, `Program.cs` can run `EnsureCreated()` only when `app.Environment.IsDevelopment()` AND a specific config flag is set, but **Phase 4 does not include this**. Anyone running locally executes `dotnet ef database update` once before starting the API. This is documented in the README.

---

## 9. Test Strategy — Phase 4

Phase 4 introduces functional tests via `WebApplicationFactory<Program>`. These spin up the full ASP.NET Core pipeline in-process and exercise endpoints over real HTTP. Combined with the existing unit/integration tests, total coverage is comprehensive.

### 9.1 `ApiWebApplicationFactory`

Custom factory that swaps the Postgres provider for SQLite in-memory:

```csharp
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public ApiWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove production DbContext registration
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            // Add SQLite in-memory replacement
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection));

            // Initialize schema once
            using var scope = services.BuildServiceProvider().CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        _connection.Dispose();
        base.Dispose(disposing);
    }
}
```

**Decisions:**
- **`Program.cs` exposes `Program` as `public partial class Program {}`** at the bottom of the file. This is required for `WebApplicationFactory<Program>` to find it. Standard ASP.NET Core idiom.
- **One SQLite connection per factory instance.** Schema persists across requests within a single test class. Tests within the same class share state; test classes are isolated.
- **`appsettings.Testing.json`** is NOT created. The factory overrides services directly instead of layering config. Simpler.

### 9.2 Endpoint tests

For each endpoint, tests cover:
- Happy path (201/200/302 with expected body or headers)
- Validation errors (400 with mapped error code)
- Not found (404)
- Domain rule violations (410 for disabled/expired)
- Conflict (409 for duplicate code)
- Cancellation propagation

#### 9.2.1 `CreateShortUrlEndpointTests`

```
Post_WithValidRequest_Returns201CreatedWithLocationHeader
Post_WithValidRequest_ResponseBodyContainsExpectedFields
Post_WithCustomCode_UsesProvidedCode
Post_WithDuplicateCustomCode_Returns409Conflict
Post_WithInvalidOriginalUrl_Returns400WithErrorCode
Post_WithExpiresAtInPast_Returns400WithInvalidExpirationCode
Post_WithoutBody_Returns400
```

#### 9.2.2 `RedirectEndpointTests`

```
Get_WithValidExistingCode_Returns302WithLocationHeader
Get_WithValidExistingCode_IncrementsClickCountInDatabase
Get_WithValidExistingCode_PersistsClickAuditRow
Get_WithInvalidCodeFormat_Returns400
Get_WithMissingCode_Returns404
Get_WithDisabledShortUrl_Returns410
Get_WithExpiredShortUrl_Returns410
Get_WithCodeShorterThan7Chars_Returns404DueToRouteConstraint
Get_WithCodeLongerThan7Chars_Returns404DueToRouteConstraint
Get_WithNonAlphanumericCode_Returns404DueToRouteConstraint
```

> Note: the last three tests verify that the route constraint (`length(7):regex(^[A-Za-z0-9]+$)`) returns 404 instead of reaching the handler. This is correct: the URL doesn't match the route pattern, so it's not "the wrong code", it's "not a route". Distinct from the 400 returned when the code reaches the handler but fails validation (which can't happen with the constraint, but is documented for clarity).

#### 9.2.3 `GetShortUrlEndpointTests`

```
Get_WithExistingCode_Returns200WithFullDetails
Get_WithDisabledShortUrl_Returns200WithIsEnabledFalse
Get_WithExpiredShortUrl_Returns200WithExpiresAtInPast
Get_WithMissingCode_Returns404
Get_WithInvalidCodeFormat_Returns400
```

#### 9.2.4 `DisableShortUrlEndpointTests` and `EnableShortUrlEndpointTests`

Identical shape:

```
Post_WithExistingCode_Returns200WithUpdatedState
Post_OnAlreadyTargetState_StillReturns200
Post_WithMissingCode_Returns404
Post_WithInvalidCodeFormat_Returns400
```

#### 9.2.5 `UpdateExpirationEndpointTests`

```
Patch_WithValidFutureDate_Returns200WithUpdatedExpiresAt
Patch_WithNullExpiresAt_Returns200AndClearsExpiration
Patch_WithPastDate_Returns400WithInvalidExpirationCode
Patch_OnDisabledShortUrl_AllowsUpdate
Patch_OnExpiredShortUrl_AllowsUpdate
Patch_WithMissingCode_Returns404
Patch_WithInvalidCodeFormat_Returns400
```

### 9.3 Error mapper unit tests

`ErrorToHttpResultMapperTests.cs` verifies the mapping table directly. Pure unit test, no `WebApplicationFactory`.

```
ToHttpResult_OnOriginalUrlInvalid_Returns400
ToHttpResult_OnOriginalUrlRequired_Returns400
ToHttpResult_OnShortCodeInvalid_Returns400
ToHttpResult_OnInvalidExpiration_Returns400
ToHttpResult_OnShortUrlNotFound_Returns404
ToHttpResult_OnShortUrlDisabled_Returns410
ToHttpResult_OnShortUrlExpired_Returns410
ToHttpResult_OnCodeAlreadyExists_Returns409
ToHttpResult_OnCodeGenerationFailed_Returns503
ToHttpResult_OnUnknownErrorCode_Returns500
ToHttpResult_IncludesTraceIdInResponseBody
ToHttpResult_IncludesErrorCodeInResponseBody
```

These tests assert on `IResult` by inspecting `(result as IStatusCodeHttpResult)?.StatusCode` and similar techniques. Documented in test code.

### 9.4 Rate limiting tests

`RedirectRateLimitTests.cs` is a focused functional test that verifies the rate limit kicks in:

```
Get_AfterPermitLimitExceeded_Returns429
Get_429Response_IncludesRetryAfterHeader
Get_429Response_BodyMatchesErrorResponseShape
Get_DifferentIpsHaveSeparatePartitions
Get_AfterWindowExpires_AcceptsNewRequests
```

The factory must override `RateLimitingOptions` to use a small `PermitLimit` (e.g. 3) and short `Window` (e.g. 1 second) to keep tests fast.

> Note: the `DifferentIpsHaveSeparatePartitions` test is non-trivial — `WebApplicationFactory` defaults to a fixed `RemoteIpAddress`. The test uses a custom `IHttpContextAccessor` shim or sets the remote IP via `HttpContext` middleware. If this proves complex, the test can be `[Fact(Skip="HTTP test infra limitation")]` with a justification, and the partition behavior is left to manual verification.

### 9.5 OpenAPI document tests

`OpenApiDocumentTests.cs` verifies that the OpenAPI spec is reachable and contains the expected endpoints:

```
Get_OpenApiJson_Returns200WithValidJson
OpenApiDocument_ContainsAllSixEndpoints
OpenApiDocument_DescribesShortUrlContractSchema
OpenApiDocument_DescribesErrorResponseSchema
OpenApiDocument_TitleIsUrlShortenerApi
```

These run against the dev environment configuration. The factory uses `builder.UseEnvironment("Development")` for these specific tests, since OpenAPI is only mapped in Development.

---

## 10. Commit Convention — Phase 4

Same Conventional Commits as previous phases. New scopes:

- `api`: anything inside `UrlShortener.Api`

Existing scopes: `repo`, `solution`, `domain` (must NOT appear), `application` (must NOT appear), `infrastructure` (must NOT appear).

**Granularity:** TDD pair = one commit. Configuration files = one commit. Endpoint registration + handler = one TDD pair.

**Example commit sequence (illustrative):**

```
chore(solution): add UrlShortener.Api and Api.Tests projects
feat(api): add Program.cs with minimal hosting, configuration, and AddInfrastructure wiring
feat(api): add appsettings.json and Development variant
feat(api): add ErrorResponse contract and ErrorToHttpResultMapper
test(api): add ErrorToHttpResultMapper unit tests
feat(api): add CreateShortUrlContract and ShortUrlContract DTOs
test(api): add CreateShortUrlEndpoint happy path tests
feat(api): implement CreateShortUrl endpoint
test(api): add CreateShortUrlEndpoint validation and conflict tests
feat(api): map duplicate and invalid errors to HTTP responses
test(api): add RedirectEndpoint happy path test
feat(api): implement Redirect endpoint with route constraint
test(api): add RedirectEndpoint disabled and expired tests
feat(api): wire redirect endpoint to error mapper for 410 responses
test(api): add GetShortUrlEndpoint tests
feat(api): implement GetShortUrl endpoint
test(api): add Disable and Enable endpoint tests
feat(api): implement Disable and Enable endpoints
test(api): add UpdateExpiration endpoint tests
feat(api): implement UpdateExpiration endpoint
feat(api): add RateLimitingOptions and FixedWindow rate limiter
test(api): add redirect rate limit tests
feat(api): apply rate limit policy to redirect endpoint and add 429 response shape
feat(api): add OpenAPI document with metadata transformer
feat(api): add Scalar UI for development environment
test(api): add OpenAPI document tests
feat(api): add structured logging on all endpoints
test(api): add ApiWebApplicationFactory with SQLite in-memory swap
docs: add Phase 4 progress note to README
```

---

## 11. What NOT to Do in Phase 4

- **Do not** modify Domain code. Phase 1 is frozen.
- **Do not** modify Application code. Phase 2 + admin extensions from Phase 3 are frozen. The API consumes use cases via the existing public surface.
- **Do not** modify Infrastructure code (`AddInfrastructure`, `EfShortUrlRepository`, `DomainEventDispatcher`, etc.). The API only calls `services.AddInfrastructure(connectionString)`.
- **Do not** introduce a service layer between the API and the use cases. Endpoints call `useCase.ExecuteAsync` directly.
- **Do not** add MediatR or any HTTP-specific mediator pattern.
- **Do not** add FluentValidation. The Application layer's value objects already validate.
- **Do not** add AutoMapper, Mapster, or any mapping library. Manual mapping in endpoint handlers is fine.
- **Do not** add JWT auth, OAuth, API keys, or any authentication layer. Out of scope for portfolio v1.
- **Do not** add CORS configuration. Out of scope; if a real client needs it, that's a deployment concern.
- **Do not** add response caching. Out of scope.
- **Do not** add HTTPS enforcement (`UseHttpsRedirection`). The Kestrel default for development is fine; production deploys handle TLS at the proxy.
- **Do not** auto-migrate the database on startup. Migrations run via `dotnet ef database update`.
- **Do not** add health check endpoints (`/health`, `/ready`). Phase 5 may add minimal health checks if needed for CI; Phase 4 doesn't.
- **Do not** add metrics, OpenTelemetry, Application Insights, or any observability beyond ILogger.
- **Do not** add Swashbuckle. Spec section 6 chose `Microsoft.AspNetCore.OpenApi`.
- **Do not** add NuGet packages outside the allowed list:
  - `UrlShortener.Api`:
    - `Microsoft.AspNetCore.OpenApi`
    - `Scalar.AspNetCore`
    - (Project references to Application + Infrastructure)
  - `UrlShortener.Api.Tests`:
    - `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`
    - `FluentAssertions`, `coverlet.collector`
    - `Microsoft.AspNetCore.Mvc.Testing`
    - `Microsoft.Data.Sqlite`
    - `Microsoft.EntityFrameworkCore.Sqlite`
- **Do not** ship secrets (real database passwords, API keys) in `appsettings.json`. The `DefaultConnection` placeholder is fine; production overrides via environment variables.
- **Do not** parallelize endpoint handling beyond what ASP.NET Core does by default.
- **Do not** swallow exceptions in endpoint handlers. The default exception handler middleware catches and returns 500.
- **Do not** return raw `Exception.Message` in error responses for unmapped errors. The default 500 case returns the `ErrorResponse` shape with the Application's error code, not the exception details.

---

## 12. Acceptance Criteria for Phase 4 Completion

Before opening the PR:

- [ ] `dotnet build` from solution root: zero warnings, zero errors
- [ ] `dotnet test` from solution root: all green (Phases 1-3 tests still pass; Phase 4 tests added)
- [ ] Every test enumerated in section 9 is implemented (or `[Fact(Skip="reason")]` with justification in commit message)
- [ ] No NuGet packages outside the allowed list in section 11
- [ ] Domain, Application, and Infrastructure projects unchanged in this PR's diff
- [ ] All six endpoints registered and reachable
- [ ] All errors from `Result<T>` mapped to appropriate HTTP statuses via `ErrorToHttpResultMapper`
- [ ] Rate limiting active on Redirect endpoint (verified by functional test)
- [ ] OpenAPI document served at `/openapi/v1.json` in Development
- [ ] Scalar UI served at `/scalar/v1` in Development
- [ ] `appsettings.json` and `appsettings.Development.json` committed
- [ ] Connection string read from configuration; missing connection string fails fast at startup
- [ ] Structured logging on all endpoints with message templates (no string interpolation in log calls)
- [ ] Commit history is granular and follows section 10 convention
- [ ] No TODO comments, no commented-out code
- [ ] No `using` statements outside `System.*`, `UrlShortener.*`, `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, `Microsoft.EntityFrameworkCore.*` (tests), `Microsoft.Data.Sqlite` (tests), `Scalar.AspNetCore`, `FluentAssertions`, `Xunit`
- [ ] PR opened on branch `phase-4-api` against `main`, NOT merged

---

## 13. Branch & PR Workflow — Phase 4

Identical to Phases 2 and 3. Summary:

1. `git checkout -b phase-4-api` from `main` before the first commit.
2. Commit on the branch following section 10.
3. Push: `git push -u origin phase-4-api`.
4. Open a PR via `gh pr create --base main --head phase-4-api --title "Phase 4: API Layer"` with body containing:
   - Summary paragraph
   - Acceptance criteria checklist from section 12
   - Test highlights (notably: WebApplicationFactory setup, rate limit functional test, OpenAPI document tests)
   - "What's deferred to Phase 5" note: GitHub Actions CI, README polish for portfolio audience, MIT license file
5. Do NOT merge.
6. Report: "Phase 4 complete. PR opened: <URL>. Acceptance criteria checked. Awaiting review." and stop.

---

## 14. Handoff to Phase 5 (preview, not in scope)

- GitHub Actions CI workflow:
  - Runs `dotnet build` on every PR
  - Runs `dotnet test` with code coverage report
  - Optionally runs against multiple .NET versions (matrix build)
  - Build status badge in README
- Public-facing README polish:
  - Add architecture diagram (Mermaid or SVG)
  - Add live demo link if deployed (Render, Railway, fly.io, etc.)
  - Reorganize sections for portfolio audience
  - Add "Why this design" section explaining decisions
- MIT license file at repo root.
- Optional: deploy to a free-tier host and link from README.

Phase 5 is the polish phase. After Phase 5, the repo is "complete" for Upwork portfolio purposes.
