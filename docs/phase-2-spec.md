# Phase 2 Spec — Application Layer (TDD)

**Repo:** `url-shortener`
**Domain:** URL Shortener
**Stack:** .NET 9, xUnit, FluentAssertions, Moq
**Approach:** Test-Driven Development, granular commits, feature branch + PR
**Branch:** `phase-2-application`

---

## 1. Goal of Phase 2

Build the Application layer in isolation. References `UrlShortener.Domain` only. Defines the abstractions (interfaces) that Phase 3 will implement (persistence, dispatcher, time, code generation). Use cases orchestrate domain behavior and return `Result<T>`; they never throw domain exceptions out to callers.

At the end of Phase 2:
- `UrlShortener.Application` project compiles
- `UrlShortener.Application.Tests` project passes 100% green
- Three use cases implemented end-to-end with TDD
- All Phase-2 abstractions defined (no implementations — those are Phase 3)
- `services.AddApplication()` extension registers every use case
- No reference from Application to Infrastructure or Api (those don't exist yet)
- Domain code from Phase 1 is **untouched**

---

## 2. Solution & Folder Structure

```
url-shortener/
├── CLAUDE.md
├── docs/
│   ├── phase-1-spec.md
│   └── phase-2-spec.md
├── src/
│   ├── UrlShortener.Domain/                 (unchanged from Phase 1)
│   └── UrlShortener.Application/
│       ├── Common/
│       │   ├── Result.cs
│       │   ├── Error.cs
│       │   └── Errors.cs
│       ├── Abstractions/
│       │   ├── IShortUrlRepository.cs
│       │   ├── IShortCodeGenerator.cs
│       │   ├── IDateTimeProvider.cs
│       │   └── IDomainEventDispatcher.cs
│       ├── ShortUrls/
│       │   ├── Create/
│       │   │   ├── CreateShortUrlUseCase.cs
│       │   │   ├── CreateShortUrlRequest.cs
│       │   │   └── CreateShortUrlResponse.cs
│       │   ├── Redirect/
│       │   │   ├── RedirectUseCase.cs
│       │   │   ├── RedirectRequest.cs
│       │   │   └── RedirectResponse.cs
│       │   └── GetByCode/
│       │       ├── GetShortUrlUseCase.cs
│       │       ├── GetShortUrlRequest.cs
│       │       └── GetShortUrlResponse.cs
│       ├── DependencyInjection/
│       │   └── ApplicationServiceCollectionExtensions.cs
│       └── UrlShortener.Application.csproj
└── tests/
    ├── UrlShortener.Domain.Tests/           (unchanged from Phase 1)
    └── UrlShortener.Application.Tests/
        ├── Common/
        │   ├── ResultTests.cs
        │   └── ErrorTests.cs
        ├── ShortUrls/
        │   ├── CreateShortUrlUseCaseTests.cs
        │   ├── RedirectUseCaseTests.cs
        │   └── GetShortUrlUseCaseTests.cs
        ├── DependencyInjection/
        │   └── ApplicationServiceCollectionExtensionsTests.cs
        └── UrlShortener.Application.Tests.csproj
```

Add both new projects to `UrlShortener.sln`. Application project references Domain. Application.Tests references Application + Domain.

Root namespace: `UrlShortener.Application`.

---

## 3. Building Blocks

### 3.1 `Error` (sealed record)

```csharp
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
```

- Two stable strings: `Code` (machine-readable, dotted notation, used by API to map to HTTP status) and `Message` (human-readable, may include contextual values).
- `Error.None` is the sentinel for successful results.
- Value equality comes free with `record`.

### 3.2 `Errors` (static catalogue)

Static class with nested groups per feature. Codes are stable contracts — Phase 4 (API) will switch on them. **Do not** rename codes once shipped.

```csharp
public static class Errors
{
    public static class ShortUrl
    {
        public static readonly Error NotFound =
            new("ShortUrl.NotFound", "Short URL not found");

        public static readonly Error Disabled =
            new("ShortUrl.Disabled", "Short URL is disabled");

        public static readonly Error Expired =
            new("ShortUrl.Expired", "Short URL has expired");

        public static Error CodeAlreadyExists(string code) =>
            new("ShortUrl.CodeAlreadyExists",
                $"Short code '{code}' already exists");

        public static readonly Error CodeGenerationFailed =
            new("ShortUrl.CodeGenerationFailed",
                "Could not generate a unique short code after maximum retries");
    }

    public static class OriginalUrl
    {
        public static Error Invalid(string reason) =>
            new("OriginalUrl.Invalid", reason);
    }

    public static class ShortCode
    {
        public static Error Invalid(string reason) =>
            new("ShortCode.Invalid", reason);
    }

    public static class Validation
    {
        public static Error InvalidExpiration(string reason) =>
            new("Validation.InvalidExpiration", reason);
    }
}
```

**Rule:** errors that wrap a domain exception's contextual message (`OriginalUrl.Invalid`, `ShortCode.Invalid`, `Validation.InvalidExpiration`) take the domain exception's `Message` as `reason`, preserving the rich context already produced in Phase 1. Errors with no per-instance context (`NotFound`, `Disabled`, `Expired`, `CodeGenerationFailed`) are static singletons.

**API matching contract:** API consumers (Phase 4) must match errors by `error.Code` (the stable contract string), not by `Error` instance equality. The `Code` is invariant across factory invocations; the `Message` may vary per call (e.g. `Errors.OriginalUrl.Invalid("...")` produces different `Error` instances with the same `Code`). Equality on the `Error` record is value-based and not the right tool for HTTP status mapping.

### 3.3 `Result` and `Result<T>`

```csharp
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException(
                "Successful result cannot have an error");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException(
                "Failed result must have an error");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, Error.None) { _value = value; }
    private Result(Error error) : base(false, error) { _value = default; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            "Cannot access Value of failed Result");

    public new static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
```

Notes:
- `Result<T>` inherits from `Result` so a use case can be wrapped uniformly (e.g. by future middleware), but consumers should program against `Result<T>` directly.
- Constructor invariants enforce that no caller can construct an inconsistent result (success with error, failure without error).
- Implicit conversions exist for ergonomics: `return Errors.ShortUrl.NotFound;` and `return response;` both compile inside a `Task<Result<TResponse>>`-returning method.
- Single-error per result. If multi-error validation aggregation is ever needed it will be a separate `ValidationResult` type, not added here.

### 3.4 Use case shape

- One **plain class** per use case (no marker interface, no `IUseCase<,>` generic).
- Constructor injects collaborators.
- Single public method:

  ```csharp
  Task<Result<TResponse>> ExecuteAsync(TRequest request, CancellationToken ct);
  ```

  (or `Task<Result>` if no payload — none of the Phase 2 use cases need this).
- `TRequest` and `TResponse` are `sealed record` types living next to the use case class.
- Response records are constructed manually from the entity inside the use case (e.g. `new CreateShortUrlResponse(shortUrl.Id, shortUrl.ShortCode.ToString(), …)`). No mapping library.
- All collaborators that perform IO take `CancellationToken`.
- The use case **does not** throw domain exceptions out. It catches them at well-defined boundaries (see section 4) and converts to `Result.Failure`. Anything else (cancellation, infrastructure failures) propagates.

### 3.5 Abstractions

All four interfaces live in `Abstractions/`. Phase 2 ships **only** the interfaces; concrete implementations are Phase 3.

```csharp
public interface IShortUrlRepository
{
    Task<ShortUrl?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ShortUrl?> GetByCodeAsync(ShortCode code, CancellationToken ct);
    Task<bool> ExistsByCodeAsync(ShortCode code, CancellationToken ct);
    Task AddAsync(ShortUrl shortUrl, CancellationToken ct);
    Task UpdateAsync(ShortUrl shortUrl, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Repository signature ordering rule (binding for all use cases):**

`ExistsByCodeAsync` takes a `ShortCode` value object, not a `string`. This forces the calling order:

1. Construct the `ShortCode` value object first (validates format).
2. Then call `ExistsByCodeAsync`.

For user-supplied custom codes, this means a malformed code is rejected with `Errors.ShortCode.Invalid` before any persistence call. Validation is sub-microsecond and catches the most common authoring mistake (wrong length, illegal characters) without a database round-trip. This trade-off is intentional and consistent: the repository surface speaks domain types only.

**`ExistsByCodeAsync` intended use:** this method is used by `CreateShortUrlUseCase` to detect duplicate codes before `AddAsync`. Phase 3 implementations may alternatively rely on a unique-index constraint at the database level and translate the violation to a duplicate result; either approach is acceptable. The Application contract is simply: this method must return `true` if a `ShortUrl` with the given code already exists in the repository.

```csharp
public interface IShortCodeGenerator
{
    Task<ShortCode> GenerateAsync(CancellationToken ct);
}
```

- Returns a `ShortCode` value object, not a string. The generator is responsible for producing characters that satisfy `ShortCode.Create`.
- The generator does **not** consult the repository. Uniqueness is the use case's job (retry loop in `CreateShortUrlUseCase`).
- If the generator throws (e.g. crypto RNG failure in Phase 3), the exception propagates — the use case does not catch it.

```csharp
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
```

- Only `UtcNow`. No `Now`, no `Today`.
- `RedirectUseCase` injects this and reads `_clock.UtcNow` internally for the click timestamp. The clock is a use-case-internal concern — callers should not be able to spoof time without intent.
- The Domain layer's `ShortUrl.Create` and `ShortUrl.UpdateExpiration` continue to call `DateTime.UtcNow` directly (Phase 1 contract, not changed).

```csharp
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct);
}
```

- Use cases call `DispatchAsync(entity.DomainEvents, ct)` **after** `SaveChangesAsync` succeeds, then `entity.ClearDomainEvents()`.
- Calling with an empty sequence is a valid no-op (use cases that don't raise events still call it, for uniformity).
- Phase 2 only defines the interface and uses `Mock<IDomainEventDispatcher>` in tests. The real handler-discovery dispatcher arrives in Phase 3.

---

## 4. Use Cases — Contracts

### 4.1 `CreateShortUrlUseCase`

**Request:**

```csharp
public sealed record CreateShortUrlRequest(
    string OriginalUrl,
    DateTime? ExpiresAt,
    string? CustomCode);
```

**Response:**

```csharp
public sealed record CreateShortUrlResponse(
    Guid Id,
    string ShortCode,
    string OriginalUrl,
    DateTime? ExpiresAt,
    DateTime CreatedAt);
```

**Collaborators:** `IShortUrlRepository`, `IShortCodeGenerator`, `IDomainEventDispatcher`.

**Constants:**
- `private const int MaxCodeGenerationAttempts = 5;`

**Algorithm (in this exact order):**

1. Build `OriginalUrl` value object via `OriginalUrl.Create(request.OriginalUrl)`.
   - Catch `InvalidOriginalUrlException ex` → `return Errors.OriginalUrl.Invalid(ex.Message);`
2. Resolve the short code:
   - **If** `request.CustomCode` is not null/whitespace:
     a. `ShortCode.Create(request.CustomCode)`.
        - Catch `InvalidShortCodeException ex` → `return Errors.ShortCode.Invalid(ex.Message);`
     b. `await _repo.ExistsByCodeAsync(shortCode, ct)`.
        - If `true` → `return Errors.ShortUrl.CodeAlreadyExists(shortCode.ToString());`
   - **Else** (no custom code): retry loop, max `MaxCodeGenerationAttempts` iterations:
     a. `var candidate = await _generator.GenerateAsync(ct);`
     b. `if (!await _repo.ExistsByCodeAsync(candidate, ct)) { shortCode = candidate; break; }`
     - If the loop exits without assigning `shortCode` → `return Errors.ShortUrl.CodeGenerationFailed;`
3. Build the entity via `ShortUrl.Create(shortCode, originalUrl, request.ExpiresAt)`.
   - Catch `DomainException ex` (raised on past `ExpiresAt`) → `return Errors.Validation.InvalidExpiration(ex.Message);`

   > **Note:** the use case does NOT pre-validate `request.ExpiresAt` against `IDateTimeProvider.UtcNow` before calling `ShortUrl.Create`. The entity is the sole authority on timing invariants. There is a vanishingly small race window where `request.ExpiresAt` is in the future at validation but not at entity creation; this is acceptable. Duplicating the check in Application would split responsibility for no gain.

4. `await _repo.AddAsync(shortUrl, ct);`
5. `await _repo.SaveChangesAsync(ct);`
6. `await _dispatcher.DispatchAsync(shortUrl.DomainEvents, ct);` (empty for Create — uniform call still made.)
7. `shortUrl.ClearDomainEvents();`
8. Construct and return the response manually:

   ```csharp
   return new CreateShortUrlResponse(
       Id:          shortUrl.Id,
       ShortCode:   shortUrl.ShortCode.ToString(),
       OriginalUrl: shortUrl.OriginalUrl.ToString(),
       ExpiresAt:   shortUrl.ExpiresAt,
       CreatedAt:   shortUrl.CreatedAt);
   ```

### 4.2 `RedirectUseCase`

**Request:**

```csharp
public sealed record RedirectRequest(
    string Code,
    string? UserAgent,
    string? IpAddress);
```

**Response:**

```csharp
public sealed record RedirectResponse(string OriginalUrl);
```

> The clock is **not** part of the request. `RedirectUseCase` injects `IDateTimeProvider` and reads `_clock.UtcNow` itself when calling `RegisterClick`. Tests fake the clock through Moq.

**Collaborators:** `IShortUrlRepository`, `IDomainEventDispatcher`, `IDateTimeProvider`.

**Algorithm:**

1. Build `ShortCode` via `ShortCode.Create(request.Code)`.
   - Catch `InvalidShortCodeException ex` → `return Errors.ShortCode.Invalid(ex.Message);`
2. `var shortUrl = await _repo.GetByCodeAsync(shortCode, ct);`
   - If `null` → `return Errors.ShortUrl.NotFound;` (no save, no dispatch.)
3. `try { shortUrl.RegisterClick(_clock.UtcNow, request.UserAgent, request.IpAddress); }`
   - `catch (ShortUrlExpiredException)` → `return Errors.ShortUrl.Expired;` (no save, no dispatch.)
   - `catch (DomainException)` → `return Errors.ShortUrl.Disabled;`

   > **Risk note:** today the only `DomainException` that `RegisterClick` can raise (other than `ShortUrlExpiredException`) is the disabled-state guard from Phase 1. If new invariants are added to `RegisterClick` in future phases (rate limiting, max clicks, etc.), this catch will silently misclassify them as "Disabled". When that happens, replace the broad `catch (DomainException)` with a specific exception type. Do not change this in Phase 2.

4. `await _repo.UpdateAsync(shortUrl, ct);`
5. `await _repo.SaveChangesAsync(ct);`
6. `await _dispatcher.DispatchAsync(shortUrl.DomainEvents, ct);`
7. `shortUrl.ClearDomainEvents();`
8. `return new RedirectResponse(shortUrl.OriginalUrl.ToString());`

### 4.3 `GetShortUrlUseCase`

Read-only. Does **not** mutate the entity, does **not** call `Update`/`Save`/`Dispatch`. Designed for admin/preview lookups.

**Request:**

```csharp
public sealed record GetShortUrlRequest(string Code);
```

**Response:**

```csharp
public sealed record GetShortUrlResponse(
    Guid Id,
    string ShortCode,
    string OriginalUrl,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    bool IsEnabled,
    long ClickCount);
```

**Collaborators:** `IShortUrlRepository`.

**Algorithm:**

1. Build `ShortCode`.
   - Catch `InvalidShortCodeException ex` → `return Errors.ShortCode.Invalid(ex.Message);`
2. `var shortUrl = await _repo.GetByCodeAsync(shortCode, ct);`
   - If `null` → `return Errors.ShortUrl.NotFound;`
3. Construct and return the response manually:

   ```csharp
   return new GetShortUrlResponse(
       Id:          shortUrl.Id,
       ShortCode:   shortUrl.ShortCode.ToString(),
       OriginalUrl: shortUrl.OriginalUrl.ToString(),
       ExpiresAt:   shortUrl.ExpiresAt,
       CreatedAt:   shortUrl.CreatedAt,
       IsEnabled:   shortUrl.IsEnabled,
       ClickCount:  shortUrl.ClickCount);
   ```

> `GetShortUrlUseCase` is intentionally indifferent to `IsEnabled` and `ExpiresAt`. It surfaces them in the response and lets the caller decide. Disabled or expired short URLs still resolve to a successful read.

### 4.4 Dispatcher failure semantics

If `IDomainEventDispatcher.DispatchAsync` throws after `SaveChangesAsync` has succeeded, the persisted state is committed but the events are lost. This is acceptable in Phase 2 because the dispatcher is in-process and only used in tests with mocked behavior. If event durability becomes a requirement (e.g. integration with external systems in later phases), the Application contract will evolve to an outbox pattern in Phase 3 or later. Phase 2 use cases do not catch dispatcher exceptions; they propagate.

---

## 5. DependencyInjection

Single extension class:

```csharp
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateShortUrlUseCase>();
        services.AddScoped<RedirectUseCase>();
        services.AddScoped<GetShortUrlUseCase>();

        return services;
    }
}
```

- Use cases registered as **scoped** (one per request once an HTTP host is wired in Phase 4; one per resolution in tests).
- Abstractions (`IShortUrlRepository`, `IShortCodeGenerator`, `IDateTimeProvider`, `IDomainEventDispatcher`) are **not** registered here — Phase 3 ships an `AddInfrastructure()` that registers them.

The DI test builds a `ServiceCollection`, registers Moq-generated stubs for the four abstractions, calls `AddApplication`, builds the provider, and resolves each use case to prove the wiring is complete.

---

## 6. Test List — Phase 2

**Convention:** `Method_Scenario_ExpectedBehavior` (Roy Osherove). Async methods named `ExecuteAsync`. xUnit `[Fact]` / `[Theory]`. FluentAssertions for assertions. Moq for collaborators.

### 6.1 `ResultTests.cs`

```
Success_NonGeneric_ReturnsResultWithIsSuccessTrueAndErrorNone
Success_Generic_ReturnsResultWithValueAndIsSuccessTrue
Failure_NonGeneric_ReturnsResultWithIsFailureTrueAndError
Failure_Generic_ReturnsResultWithIsFailureTrueAndError
Constructor_SuccessWithNonNoneError_ThrowsInvalidOperationException
Constructor_FailureWithNoneError_ThrowsInvalidOperationException
Value_OnFailedResult_ThrowsInvalidOperationException
ImplicitConversion_FromValue_CreatesSuccessResult
ImplicitConversion_FromError_CreatesFailureResult
```

### 6.2 `ErrorTests.cs`

```
Equals_TwoErrorsWithSameCodeAndMessage_AreEqual
Equals_TwoErrorsWithDifferentCode_AreNotEqual
Equals_TwoErrorsWithDifferentMessage_AreNotEqual
None_HasEmptyCodeAndEmptyMessage
```

### 6.3 `CreateShortUrlUseCaseTests.cs`

Happy path:
```
ExecuteAsync_WithValidUrlAndNoCustomCode_ReturnsSuccessWithGeneratedCode
ExecuteAsync_WithValidUrlAndCustomCode_ReturnsSuccessWithCustomCode
ExecuteAsync_WithValidUrlAndExpiration_ReturnsSuccessWithExpiration
ExecuteAsync_WithoutCustomCode_CallsCodeGenerator
ExecuteAsync_WithCustomCode_DoesNotCallCodeGenerator
ExecuteAsync_OnSuccess_CallsAddAsyncAndSaveChangesAsyncInOrder
ExecuteAsync_OnSuccess_CallsDispatcherWithEmptyEventsAfterSave  // verifies uniform dispatcher call even when no events are raised
ExecuteAsync_OnSuccess_ClearsDomainEventsAfterDispatch
ExecuteAsync_OnSuccess_ResponseFieldsMatchEntity
```

Validation/failure:
```
ExecuteAsync_WithInvalidOriginalUrl_ReturnsFailureWithOriginalUrlInvalid
ExecuteAsync_WithInvalidOriginalUrl_DoesNotCallRepositoryOrGenerator
ExecuteAsync_WithInvalidCustomCode_ReturnsFailureWithShortCodeInvalid
ExecuteAsync_WithInvalidCustomCode_DoesNotCallRepository
ExecuteAsync_WithDuplicateCustomCode_ReturnsFailureWithCodeAlreadyExists
ExecuteAsync_WithDuplicateCustomCode_DoesNotCallAddOrSave
ExecuteAsync_WithPastExpiration_ReturnsFailureWithInvalidExpiration
ExecuteAsync_WithPastExpiration_DoesNotCallAddOrSave
```

Code-generation collisions:
```
ExecuteAsync_WhenGeneratedCodeCollidesOnce_RetriesAndSucceedsOnSecondAttempt
ExecuteAsync_WhenAllAttemptsCollide_ReturnsFailureWithCodeGenerationFailed
ExecuteAsync_WhenAllAttemptsCollide_DoesNotCallAddOrSave
ExecuteAsync_WhenGeneratorRetries_DoesNotExceedMaxAttempts
```

Cancellation:
```
ExecuteAsync_WhenCancellationRequested_PropagatesOperationCanceledException
```

### 6.4 `RedirectUseCaseTests.cs`

Happy path:
```
ExecuteAsync_WithValidEnabledNotExpiredCode_ReturnsSuccessWithOriginalUrl
ExecuteAsync_OnSuccess_CallsRegisterClickWithClockUtcNowAndUserAgentAndIp
ExecuteAsync_OnSuccess_CallsUpdateAsyncAndSaveChangesAsyncInOrder
ExecuteAsync_OnSuccess_DispatchesShortUrlClickedEventAfterSave
ExecuteAsync_OnSuccess_ClearsDomainEventsAfterDispatch
```

Failure paths:
```
ExecuteAsync_WithInvalidCode_ReturnsFailureWithShortCodeInvalid
ExecuteAsync_WithInvalidCode_DoesNotCallRepository
ExecuteAsync_WhenCodeNotFound_ReturnsFailureWithNotFound
ExecuteAsync_WhenCodeNotFound_DoesNotCallUpdateOrSaveOrDispatch
ExecuteAsync_WhenShortUrlIsDisabled_ReturnsFailureWithDisabled
ExecuteAsync_WhenShortUrlIsDisabled_DoesNotCallSaveOrDispatch
ExecuteAsync_WhenShortUrlIsExpired_ReturnsFailureWithExpired
ExecuteAsync_WhenShortUrlIsExpired_DoesNotCallSaveOrDispatch
```

Cancellation:
```
ExecuteAsync_WhenCancellationRequested_PropagatesOperationCanceledException
```

### 6.5 `GetShortUrlUseCaseTests.cs`

```
ExecuteAsync_WithValidExistingCode_ReturnsSuccessWithFullDetails
ExecuteAsync_WithInvalidCode_ReturnsFailureWithShortCodeInvalid
ExecuteAsync_WhenCodeNotFound_ReturnsFailureWithNotFound
ExecuteAsync_OnDisabledShortUrl_StillReturnsSuccess
ExecuteAsync_OnExpiredShortUrl_StillReturnsSuccess
ExecuteAsync_DoesNotCallUpdateOrSaveOrDispatch
ExecuteAsync_OnSuccess_ResponseFieldsMatchEntityIncludingClickCountAndIsEnabled
```

### 6.6 `ApplicationServiceCollectionExtensionsTests.cs`

```
AddApplication_RegistersCreateShortUrlUseCase
AddApplication_RegistersRedirectUseCase
AddApplication_RegistersGetShortUrlUseCase
AddApplication_AfterMockingAbstractions_AllUseCasesResolveFromProvider
```

The DI test seeds the `ServiceCollection` with `Mock.Of<IShortUrlRepository>()`, `Mock.Of<IShortCodeGenerator>()`, `Mock.Of<IDateTimeProvider>()`, `Mock.Of<IDomainEventDispatcher>()` before calling `AddApplication`.

---

## 7. Commit Convention — Phase 2

Same Conventional Commits convention as Phase 1 (section 6 of the Phase 1 spec). New scope this phase:

- `application`: anything inside `UrlShortener.Application`
- `tests`: shared test infrastructure changes (rare)

Existing scopes still valid: `repo`, `domain` (must NOT appear — domain is frozen), `solution` (only for adding the new csproj/sln entries).

**Granularity rule unchanged:** one logical concept per commit. TDD pair (one test + the code that passes it) is one commit. Adding the `Result` type and a batch of its tests in two commits (test, then code) is one TDD cycle.

**Example commit sequence (illustrative):**

```
chore(solution): add UrlShortener.Application and Application.Tests projects
test(application): add Result success and failure tests
feat(application): implement Result and Result<T> with implicit conversions
test(application): add Error equality and Error.None tests
feat(application): implement Error record and Error.None
feat(application): add Errors catalogue with ShortUrl, OriginalUrl, ShortCode, Validation groups
feat(application): add IShortUrlRepository, IShortCodeGenerator, IDateTimeProvider, IDomainEventDispatcher
test(application): add CreateShortUrlUseCase happy path with custom code
feat(application): implement CreateShortUrlUseCase happy path
test(application): add CreateShortUrlUseCase invalid url and invalid custom code paths
feat(application): convert domain exceptions to Result.Failure in CreateShortUrlUseCase
test(application): add CreateShortUrlUseCase code-generation retry tests
feat(application): implement code generation retry loop with max attempts
test(application): add RedirectUseCase happy path and failure paths
feat(application): implement RedirectUseCase
test(application): add GetShortUrlUseCase tests
feat(application): implement GetShortUrlUseCase
test(application): add AddApplication DI registration tests
feat(application): add AddApplication service collection extension
docs: add Phase 2 progress note to README
```

---

## 8. What NOT to Do in Phase 2

- **Do not** modify Phase 1 Domain code. If something in Domain looks wrong, raise it as a question and stop. Do not edit silently.
- **Do not** implement any abstraction (`IShortUrlRepository`, `IShortCodeGenerator`, `IDateTimeProvider`, `IDomainEventDispatcher`). Phase 3 owns those.
- **Do not** add EF Core, ASP.NET, or any persistence/HTTP code.
- **Do not** add `Disable`, `Enable`, or `UpdateExpiration` use cases. Deferred to Phase 3 where integration tests against real persistence make them meaningful.
- **Do not** add `ListShortUrlsUseCase`, `GetClickAuditsUseCase`, or any feature not enumerated in section 4.
- **Do not** add a generic `IUseCase<TRequest, TResponse>` marker interface, MediatR, or any runtime dispatch.
- **Do not** add Mapster, AutoMapper, or any mapping library. Response records are constructed manually inside each use case.
- **Do not** add FluentValidation, DataAnnotations, or any validation framework. Validation lives in Domain value objects; the Application layer catches and translates.
- **Do not** create `Result.Combine`, `Result.Map`, `Result.Bind`, or other functional helpers. We will add them only when a use case actually demands them.
- **Do not** add multi-error / `ValidationResult` aggregation. Single error per result is the contract.
- **Do not** add logging (`ILogger`). Logging cross-cuts will be added in Phase 4 at the API edge.
- **Do not** add NuGet packages outside the allowed list:
  - `UrlShortener.Application`: `Microsoft.Extensions.DependencyInjection.Abstractions`.
  - `UrlShortener.Application.Tests`: `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `Moq`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`, plus references to `UrlShortener.Application` and `UrlShortener.Domain`.
- **Do not** write integration tests in Phase 2. All Application tests are unit tests with mocked abstractions, sub-millisecond per test.
- **Do not** allow domain exceptions to escape a use case method. Every public `ExecuteAsync` either returns `Result<T>` or propagates `OperationCanceledException` / infrastructure failures. No `DomainException` reaches the caller.
- **Do not** swallow unknown exceptions. Catch only the specific domain exceptions listed in section 4 (`InvalidOriginalUrlException`, `InvalidShortCodeException`, `ShortUrlExpiredException`, `DomainException`). Anything else propagates.
- **Do not** call `ClearDomainEvents()` before dispatching. Order is: save → dispatch → clear.
- **Do not** write XML doc comments on every member. Reserve them for non-obvious public API.
- **Do not** add sample/demo programs (`Program.cs`, `Main`). Application is a class library only.

---

## 9. Acceptance Criteria for Phase 2 Completion

Before opening the PR:

- [ ] `dotnet build` from solution root: zero warnings, zero errors
- [ ] `dotnet test` from solution root: all green (Domain tests still pass; Application tests all pass)
- [ ] Every test in section 6 is implemented (or `[Fact(Skip="reason")]` with justification in commit message)
- [ ] Every error returned by a use case comes from the `Errors` catalogue (no ad-hoc `new Error(...)` inside use cases)
- [ ] No NuGet packages outside the allowed list in section 8
- [ ] Domain project, Domain tests, and `phase-1-spec.md` are unchanged in this PR's diff
- [ ] Commit history is granular and follows section 7 convention
- [ ] No TODO comments, no commented-out code
- [ ] No `using` statements outside `System.*`, `UrlShortener.Domain.*`, `UrlShortener.Application.*`, or `Microsoft.Extensions.DependencyInjection.*` in production code (test project may also use `Xunit`, `FluentAssertions`, `Moq`)
- [ ] No `DomainException` reaches outside any use case method (verified by tests asserting `Result.IsFailure` for every domain-violation path)
- [ ] `services.AddApplication()` resolves all three use cases from a `ServiceProvider` when the four abstractions are pre-registered as mocks
- [ ] PR opened on branch `phase-2-application` against `main`, NOT merged

---

## 10. Branch & PR Workflow — Phase 2

This is the change from Phase 1 (which committed straight to `main`). For Phase 2 onward:

1. **Create branch** `phase-2-application` from `main` before the first commit:
   ```
   git checkout -b phase-2-application
   ```
2. **Commit on the branch** following section 7.
3. When acceptance criteria are met, **push** the branch:
   ```
   git push -u origin phase-2-application
   ```
4. **Open a PR** via `gh`:
   ```
   gh pr create \
     --base main \
     --head phase-2-application \
     --title "Phase 2: Application Layer" \
     --body-file <PR body file>
   ```
   PR body must contain:
   - One-paragraph summary of what's in the diff
   - The acceptance criteria checklist from section 9, all items checked
   - A brief "test highlights" section listing the most interesting test scenarios
   - A "what's deferred to Phase 3" note (admin use cases, persistence, dispatcher impl, code generator impl, time provider impl)
5. **Do NOT merge.** The owner reviews the PR cold and merges manually.
6. **Do NOT push intermediate commits to the branch in batches with `--force`.** Push is append-only; if a fixup is needed, add a normal commit (`fix(application): …`) on top.

After the PR is opened, say:

> Phase 2 complete. PR opened: <URL>. Acceptance criteria checked. Awaiting review.

…and stop. Do not start Phase 3.

---

## 11. Handoff to Phase 3 (preview, not in scope)

So you know what's coming and don't accidentally build it now:

- **`UrlShortener.Infrastructure`** with EF Core (SQLite for dev, Postgres later) implementing `IShortUrlRepository`.
- **`SystemDateTimeProvider`** implementing `IDateTimeProvider` (`UtcNow => DateTime.UtcNow`).
- **`Base62ShortCodeGenerator`** implementing `IShortCodeGenerator` using `RandomNumberGenerator` for crypto-grade randomness.
- **`DomainEventDispatcher`** implementing `IDomainEventDispatcher` with handler discovery via DI.
- **`ShortUrlClickedEventHandler`** that writes to the `ClickAudits` table using the `ClickAudit` record from Phase 1.
- **Admin use cases** in Application (or a new `Admin` folder): `DisableShortUrlUseCase`, `EnableShortUrlUseCase`, `UpdateExpirationUseCase`.
- **Integration tests** against a real SQLite database for the repository and dispatcher.
- EF migrations for `ShortUrls` and `ClickAudits` tables.

Phases 4 (Api + DI wiring + rate limiting) and 5 (CI + public README) follow the Phase 1 plan unchanged.

Keep Phase 2 focused on contracts and orchestration. Persistence is Phase 3.
