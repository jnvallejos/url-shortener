# Phase 1 Spec — Domain Layer (TDD)

**Repo:** `dotnet-clean-architecture-template`
**Domain:** URL Shortener
**Stack:** .NET 9, xUnit, FluentAssertions, Moq
**Approach:** Test-Driven Development, granular commits

---

## 1. Goal of Phase 1

Build the Domain layer in isolation. Pure C#, zero dependencies on EF Core, ASP.NET, or anything outside `System.*`. Every public behavior is driven by a failing test first.

At the end of Phase 1:
- `UrlShortener.Domain` project compiles
- `UrlShortener.Domain.Tests` project passes 100% green
- No reference to Application, Infrastructure, or Api projects (those don't exist yet)

---

## 2. Solution & Folder Structure

```
dotnet-clean-architecture-template/
├── CLAUDE.md
├── src/
│   └── UrlShortener.Domain/
│       ├── Common/
│       │   ├── Entity.cs
│       │   ├── ValueObject.cs
│       │   └── IDomainEvent.cs
│       ├── Exceptions/
│       │   ├── DomainException.cs
│       │   ├── InvalidOriginalUrlException.cs
│       │   ├── InvalidShortCodeException.cs
│       │   └── ShortUrlExpiredException.cs
│       ├── ShortUrls/
│       │   ├── ShortUrl.cs
│       │   ├── ShortCode.cs
│       │   ├── OriginalUrl.cs
│       │   └── Events/
│       │       └── ShortUrlClickedEvent.cs
│       └── ClickAudits/
│           └── ClickAudit.cs
└── tests/
    └── UrlShortener.Domain.Tests/
        ├── ShortUrls/
        │   ├── ShortUrlTests.cs
        │   ├── ShortCodeTests.cs
        │   └── OriginalUrlTests.cs
        └── ClickAudits/
            └── ClickAuditTests.cs
```

Solution file at root: `UrlShortener.sln`. Reference both projects from the .sln.

Root namespace: `UrlShortener` (no personal initials, no company prefix).

---

## 3. Domain Model

### 3.1 Common building blocks

**`Entity` (abstract base)**
- `Id` of type `Guid`, set in constructor
- `IReadOnlyCollection<IDomainEvent> DomainEvents` exposed
- Protected `RaiseDomainEvent(IDomainEvent)` and public `ClearDomainEvents()`
- Equality by `Id`

**`ValueObject` (abstract base)**
- Equality by structural component comparison
- Override `GetEqualityComponents()` in derived types
- Override `Equals` and `GetHashCode` in base using those components
- `Equals(object?)` returns `false` when argument is `null` or of a different runtime type

**`IDomainEvent`**
- Marker interface with `DateTime OccurredOn { get; }`. Every event records when it happened.

### 3.2 `ShortCode` (Value Object)

Wraps the 7-character Base62 short code.

**Rules:**
- Length must be exactly 7 characters
- Only Base62 chars allowed: `[A-Za-z0-9]`
- Created via factory method `ShortCode.Create(string value)`
- Throws `InvalidShortCodeException` on violation
- `ToString()` returns the underlying value

**The Domain does NOT generate the code.** Generation lives in Application via an interface (`IShortCodeGenerator`). The Domain only validates. Keep this separation explicit.

### 3.3 `OriginalUrl` (Value Object)

Wraps the long URL being shortened.

**Rules:**
- Not null, not empty, not whitespace
- Input is **trimmed first**, then all subsequent validation (length, scheme, parseability) runs against the trimmed value. Length check applies to trimmed length, not raw input length.
- Maximum trimmed length: 2048 characters (industry standard browser limit)
- Must parse via `Uri.TryCreate(trimmedValue, UriKind.Absolute, out var uri)`
- Scheme must be `http` or `https` (case-insensitive). Reject `javascript:`, `data:`, `file:`, `ftp:`, anything else explicitly.
- Created via factory method `OriginalUrl.Create(string value)`
- Throws `InvalidOriginalUrlException` on violation
- `ToString()` returns the underlying normalized value (use `uri.AbsoluteUri` after parsing — handles IDN normalization automatically)

**Edge cases that MUST have tests:**
- URL exactly at 2048 chars trimmed (passes)
- URL at 2049 chars trimmed (fails)
- URL with raw length 2050 where trimmed length is 2046 (passes — trim-before-measure)
- `javascript:alert(1)` (fails)
- `data:text/html,<script>` (fails)
- `ftp://example.com` (fails)
- `HTTP://Example.COM/Path` (passes, normalized)
- IDN domain like `https://例え.jp` or `https://xn--r8jz45g.jp` (passes, normalized to punycode via `AbsoluteUri`)
- Unicode in path: `https://example.com/café` (passes)
- Trailing/leading whitespace: `"  https://example.com  "` trimmed before validation, then passes

### 3.4 `ShortUrl` (Entity / Aggregate Root)

The main entity. Aggregate root for the bounded context.

**Properties (all `private set` or init-only):**
- `Id` (Guid, from base)
- `ShortCode ShortCode`
- `OriginalUrl OriginalUrl`
- `CreatedAt` (DateTime UTC)
- `ExpiresAt` (DateTime? UTC, nullable)
- `IsEnabled` (bool)
- `ClickCount` (long)

**Factory method:**
```
public static ShortUrl Create(
    ShortCode shortCode,
    OriginalUrl originalUrl,
    DateTime? expiresAt = null)
```
- `expiresAt`, if provided, must be in the future (UTC). Otherwise throw `DomainException` with message including the received value.
- `CreatedAt` set to `DateTime.UtcNow` (we'll inject `IDateTimeProvider` in Application later. For Phase 1, hardcode `UtcNow` and accept the test-time coupling. Tests verify "approximately now" within a tolerance).
- `IsEnabled` defaults to `true`
- `ClickCount` defaults to `0`

**Behaviors:**

`RegisterClick(DateTime clickedAtUtc, string? userAgent, string? ipAddress)`
- If `IsEnabled == false` → throw `DomainException("Cannot register click on disabled ShortUrl: {Id}")`
- If `ExpiresAt.HasValue && clickedAtUtc >= ExpiresAt.Value` → throw `ShortUrlExpiredException` with message including `ShortCode` and `ExpiresAt`
- Increment `ClickCount`
- Raise `ShortUrlClickedEvent(ShortUrlId, ShortCodeValue, clickedAtUtc, userAgent, ipAddress)`

> **Design note (intentional, not an oversight):** `RegisterClick` trusts the `clickedAtUtc` timestamp provided by the caller. The Domain does not validate that the timestamp is "reasonable" (not far in the past, not in the future, etc.). Timing validation is the responsibility of the Application layer, which has access to `IDateTimeProvider` and the request context. Domain stays focused on aggregate invariants (enabled state, expiration); freshness of the clock is an Application concern.

`Disable()`
- Sets `IsEnabled = false`. Idempotent (calling on already-disabled is a no-op, no throw).

`Enable()`
- Sets `IsEnabled = true`. Idempotent.

`UpdateExpiration(DateTime? newExpiresAt)`
- If `newExpiresAt.HasValue && newExpiresAt.Value <= DateTime.UtcNow` → throw with contextual message.
- Otherwise update.
- **Allowed in any state:** enabled, disabled, or already-expired ShortUrls all accept this operation. It is an administrative operation (e.g. extend the expiration of a previously-expired URL to reactivate it, or schedule expiration on a disabled URL before re-enabling). The aggregate's `IsEnabled` and current `ExpiresAt` are intentionally not consulted.

### 3.5 `ShortUrlClickedEvent`

```
public sealed record ShortUrlClickedEvent(
    Guid ShortUrlId,
    string ShortCodeValue,
    DateTime ClickedAt,
    string? UserAgent,
    string? IpAddress) : IDomainEvent
{
    public DateTime OccurredOn => ClickedAt;
}
```

> Note: the event exposes `ShortCodeValue` as `string` rather than the `ShortCode` value object to keep events serializable and decoupled from Domain types when downstream handlers persist or transport them (Phase 3 writes events to the `ClickAudits` table; future integrations may serialize to message buses).

This event will be consumed in Phase 3 by a handler that writes to the `ClickAudits` table. Phase 1 only raises and exposes it via `ShortUrl.DomainEvents`.

### 3.6 `ClickAudit` (Domain Record, not Entity)

`ClickAudit` is modeled as a **`sealed record`**, not an `Entity`. Justification:

- It has no behavior beyond construction.
- It does not participate in any aggregate invariant. It is not an aggregate root, nor a child of one.
- It does not raise domain events.
- It is effectively a domain-defined immutable DTO for the audit log: created once by the click event handler in Phase 3, persisted, and never mutated.

Modeling it as `Entity` would force inheritance of `DomainEvents` and identity-based equality that the type does not need or use. `record` gives us value-based equality, immutability, and concise syntax for free.

**Definition:**

```
public sealed record ClickAudit
{
    public Guid Id { get; }
    public Guid ShortUrlId { get; }
    public string ShortCodeValue { get; }
    public DateTime ClickedAt { get; }
    public string? UserAgent { get; }
    public string? IpAddress { get; }

    private ClickAudit(...) { ... }

    public static ClickAudit Create(
        Guid shortUrlId,
        string shortCodeValue,
        DateTime clickedAt,
        string? userAgent,
        string? ipAddress);
}
```

**Rules applied in `Create`:**
- `Id` generated as `Guid.NewGuid()`
- Truncate `userAgent` to 512 chars if longer (don't throw, audit is best-effort)
- Truncate `ipAddress` to 45 chars if longer (fits IPv6)
- `ShortCodeValue` is denormalized for query convenience in audit reads

No public constructor, no setters, no behaviors beyond `Create`.

---

## 4. Domain Exceptions — Message Convention

**Rule:** every domain exception message must include the offending value or relevant context. Generic messages are forbidden.

**Bad (do not write):**
- `"Invalid URL"`
- `"Short code is invalid"`
- `"URL is too long"`

**Good (write like this):**
- `$"OriginalUrl scheme must be 'http' or 'https'; received: '{scheme}'"`
- `$"OriginalUrl exceeds maximum length of 2048 characters; received length: {trimmedValue.Length}"`
- `$"ShortCode must be exactly 7 characters; received '{value}' with length {value.Length}"`
- `$"ShortCode contains invalid character at position {index}: '{character}'. Allowed: [A-Za-z0-9]"`
- `$"ExpiresAt must be in the future; received '{expiresAt:O}', current UTC '{DateTime.UtcNow:O}'"`
- `$"Cannot register click on disabled ShortUrl '{ShortCode}' (Id: {Id})"`
- `$"ShortUrl '{ShortCode}' expired at '{ExpiresAt:O}'; click attempted at '{clickedAt:O}'"`

**Exception class hierarchy:**
- `DomainException : Exception` (base for all domain rule violations)
- `InvalidOriginalUrlException : DomainException`
- `InvalidShortCodeException : DomainException`
- `ShortUrlExpiredException : DomainException`

Generic invariant violations (disabled ShortUrl, past expiration on update, etc.) throw `DomainException` directly. Reserve named subclasses for things the Application layer might want to catch specifically.

---

## 5. Test List — Phase 1

**Convention:** test names use `Method_Scenario_ExpectedBehavior` (Roy Osherove style). xUnit `[Fact]` for single-case, `[Theory]` with `[InlineData]` for parametric.

### 5.1 `ShortCodeTests.cs`

```
Create_WithValid7CharBase62String_ReturnsShortCode
Create_WithExactly7AlphanumericChars_ReturnsShortCode
Create_WithNullValue_ThrowsInvalidShortCodeException
Create_WithEmptyValue_ThrowsInvalidShortCodeException
Create_WithWhitespaceValue_ThrowsInvalidShortCodeException
Create_With6Chars_ThrowsInvalidShortCodeException
Create_With8Chars_ThrowsInvalidShortCodeException
Create_WithSpecialCharacter_ThrowsInvalidShortCodeException [Theory: "abc-123", "abc 123", "abc/123", "abc_12"]
Create_WithUnicodeCharacter_ThrowsInvalidShortCodeException
ToString_ReturnsUnderlyingValue
Equals_TwoShortCodesWithSameValue_AreEqual
Equals_TwoShortCodesWithDifferentValue_AreNotEqual
Equals_WithNull_ReturnsFalse
Equals_WithDifferentType_ReturnsFalse
GetHashCode_TwoShortCodesWithSameValue_ReturnSameHash
ExceptionMessage_OnInvalidLength_ContainsActualLength
ExceptionMessage_OnInvalidCharacter_ContainsCharacterAndPosition
```

### 5.2 `OriginalUrlTests.cs`

```
Create_WithValidHttpsUrl_ReturnsOriginalUrl
Create_WithValidHttpUrl_ReturnsOriginalUrl
Create_WithUppercaseScheme_NormalizesToLowercase
Create_WithLeadingAndTrailingWhitespace_TrimsBeforeValidation
Create_WithNullValue_ThrowsInvalidOriginalUrlException
Create_WithEmptyValue_ThrowsInvalidOriginalUrlException
Create_WithWhitespaceOnly_ThrowsInvalidOriginalUrlException
Create_AtExactly2048CharsTrimmed_ReturnsOriginalUrl
Create_At2049CharsTrimmed_ThrowsInvalidOriginalUrlException
Create_WithRawLength2050ButTrimmedLength2046_ReturnsOriginalUrl
Create_WithJavascriptScheme_ThrowsInvalidOriginalUrlException
Create_WithDataScheme_ThrowsInvalidOriginalUrlException
Create_WithFileScheme_ThrowsInvalidOriginalUrlException
Create_WithFtpScheme_ThrowsInvalidOriginalUrlException
Create_WithRelativeUrl_ThrowsInvalidOriginalUrlException
Create_WithMalformedUrl_ThrowsInvalidOriginalUrlException [Theory: "not-a-url", "http://", "://example.com"]
Create_WithIdnDomain_NormalizesToPunycode
Create_WithUnicodePath_PreservesPath
Create_WithQueryStringAndFragment_PreservesBoth
ToString_ReturnsNormalizedAbsoluteUri
Equals_SameUrlDifferentCasing_AreEqual
Equals_WithNull_ReturnsFalse
Equals_WithDifferentType_ReturnsFalse
ExceptionMessage_OnInvalidScheme_ContainsReceivedScheme
ExceptionMessage_OnLengthViolation_ContainsTrimmedLength
```

### 5.3 `ShortUrlTests.cs`

```
Create_WithValidInputs_ReturnsShortUrlWithDefaults
Create_DefaultsClickCountToZero
Create_DefaultsIsEnabledToTrue
Create_SetsCreatedAtToApproximatelyUtcNow
Create_WithNullExpiresAt_AllowsCreation
Create_WithFutureExpiresAt_AllowsCreation
Create_WithPastExpiresAt_ThrowsDomainException
Create_WithExpiresAtEqualToNow_ThrowsDomainException
```

> Implementation note for `Create_WithExpiresAtEqualToNow_ThrowsDomainException`: to avoid flakiness from clock drift between test arrange and the factory's internal `DateTime.UtcNow` read, pass `expiresAt` as `DateTime.UtcNow` with a tiny negative offset (e.g. `AddMilliseconds(-1)`) or use a fixed reference moment. "Exactly now" is conceptually clear but practically ambiguous under fast execution.

```
RegisterClick_OnEnabledNotExpired_IncrementsClickCount
RegisterClick_OnEnabledNotExpired_RaisesShortUrlClickedEvent
RegisterClick_RaisedEvent_ContainsShortUrlIdAndShortCode
RegisterClick_RaisedEvent_ContainsClickedAtAndUserAgentAndIp
RegisterClick_OnDisabled_ThrowsDomainException
RegisterClick_OnExpired_ThrowsShortUrlExpiredException
RegisterClick_AfterMultipleClicks_AccumulatesCount
RegisterClick_OnExpired_ExceptionMessageContainsShortCodeAndExpiresAt

Disable_OnEnabled_SetsIsEnabledFalse
Disable_OnAlreadyDisabled_IsIdempotent
Enable_OnDisabled_SetsIsEnabledTrue
Enable_OnAlreadyEnabled_IsIdempotent

UpdateExpiration_WithFutureDate_UpdatesExpiresAt
UpdateExpiration_WithNull_ClearsExpiration
UpdateExpiration_WithPastDate_ThrowsDomainException
UpdateExpiration_OnDisabledShortUrl_AllowsUpdate
UpdateExpiration_OnExpiredShortUrl_AllowsUpdate

DomainEvents_AfterCreate_IsEmpty
DomainEvents_AfterRegisterClick_ContainsExactlyOneEvent
DomainEvents_AfterMultipleClicks_ContainsEventPerClick
DomainEvents_AfterClearDomainEvents_IsEmpty
```

### 5.4 `ClickAuditTests.cs`

```
Create_WithValidInputs_ReturnsClickAudit
Create_WithNullUserAgent_AllowsCreation
Create_WithNullIpAddress_AllowsCreation
Create_WithUserAgentLongerThan512_TruncatesTo512
Create_WithIpAddressLongerThan45_TruncatesTo45
Create_WithIpv6Address_PreservesFullAddress
Equals_TwoClickAuditsWithSameValues_AreEqual
Equals_TwoClickAuditsWithDifferentIds_AreNotEqual
```

---

## 6. TDD Workflow & Commit Convention

### Workflow per behavior

1. Write the failing test (red)
2. Write the minimum code to pass (green)
3. Refactor if needed (still green)
4. Commit

### Conventional Commits

Format: `<type>(<scope>): <description>`

**Types used in Phase 1:**
- `test`: adding or modifying tests
- `feat`: new domain code that makes tests pass
- `refactor`: restructuring without changing behavior or tests
- `docs`: README, comments, spec
- `chore`: project setup, .gitignore, .editorconfig, csproj tweaks, repo-level config files

**Scopes used in Phase 1:**
- `repo`: repo-level config that is not tied to the .NET solution (e.g. `CLAUDE.md`)
- `domain`: anything inside `UrlShortener.Domain`
- `tests`: test infrastructure changes (not the tests themselves, those go under `domain`)
- `solution`: .sln, project references, build files

**Example commit sequence (illustrative, not prescriptive):**

```
chore(repo): add CLAUDE.md with operating conventions
chore(solution): scaffold solution with Domain and Domain.Tests projects
chore(solution): add .editorconfig and .gitignore
test(domain): add ShortCode creation tests for valid input
feat(domain): implement ShortCode value object with length validation
test(domain): add ShortCode tests for invalid characters
feat(domain): add character validation to ShortCode
refactor(domain): extract Base62 char check into private helper
test(domain): add OriginalUrl tests for valid http and https schemes
feat(domain): implement OriginalUrl value object with scheme validation
test(domain): add OriginalUrl edge cases (length, IDN, dangerous schemes)
feat(domain): handle URL normalization and dangerous scheme rejection
test(domain): add ShortUrl factory and basic state tests
feat(domain): implement ShortUrl entity with factory method
test(domain): add ShortUrl click registration tests
feat(domain): implement RegisterClick with domain event
test(domain): add ShortUrlClickedEvent assertions
feat(domain): add ShortUrlClickedEvent record
test(domain): add ClickAudit creation and truncation tests
feat(domain): implement ClickAudit record
docs: add Phase 1 progress note to README
```

**Granularity rule:** one logical concept per commit. A commit that touches both `ShortCode` and `OriginalUrl` is too big. A commit that adds a test and the code that passes it is fine (TDD pair). A commit that adds 5 tests and 5 implementations is too big.

---

## 7. What NOT to Do in Phase 1

- **Do not** add EF Core, EF annotations, or any persistence concern. No `[Key]`, no `[Required]`, no `DbContext`. Domain stays pure.
- **Do not** add `IShortCodeGenerator` or any interface that hints at Application layer. That's Phase 2.
- **Do not** implement a domain event dispatcher. Phase 1 only raises events into the entity's internal list. Dispatching is Phase 3.
- **Do not** add logging (`ILogger`), `IDateTimeProvider`, or any cross-cutting abstraction yet. Use `DateTime.UtcNow` directly. We'll inject in Phase 2 when use cases need testability.
- **Do not** create a `Result<T>` type yet. Domain throws exceptions for invariant violations. Result pattern is for Application layer (Phase 2).
- **Do not** create folders for things that don't exist yet (`Repositories/`, `Services/`, etc.).
- **Do not** add validation libraries (FluentValidation, DataAnnotations). Hand-rolled validation only in Domain.
- **Do not** add NuGet packages to the Domain project. It must be `<TargetFramework>net9.0</TargetFramework>` and nothing else. The Tests project gets xUnit, FluentAssertions, Moq, Microsoft.NET.Test.Sdk, coverlet.collector.
- **Do not** write integration tests in Phase 1. Domain tests are unit tests, in-memory, sub-millisecond per test.
- **Do not** add multi-tenancy, user authentication, or anything user-related. Out of scope for v1.
- **Do not** generate slugs, hash anything, or call any external API from Domain.
- **Do not** add `[GeneratedRegex]` source generators or premature optimization. Plain string operations are fine for the volume we're dealing with.
- **Do not** write XML doc comments (`///`) on every member. Reserve them for non-obvious public API. README and tests are the documentation.

---

## 8. Acceptance Criteria for Phase 1 Completion

Before requesting code review:

- [ ] `dotnet build` from solution root: zero warnings, zero errors
- [ ] `dotnet test` from solution root: all green
- [ ] Every test in section 5 is implemented (or explicitly skipped with `[Fact(Skip="reason")]` and justification in commit message)
- [ ] All domain exception messages follow section 4 convention
- [ ] No NuGet packages in `UrlShortener.Domain.csproj` other than implicit `Microsoft.NET.Sdk`
- [ ] Commit history is granular and follows section 6 convention
- [ ] `.editorconfig` and `.gitignore` are committed
- [ ] `CLAUDE.md` is committed at repo root with the operating conventions from section 10
- [ ] No TODO comments, no commented-out code
- [ ] No `using` statements outside `System.*` or `UrlShortener.Domain.*` namespaces in production code (excluding tests)

When all checked, push the branch and tell me. I'll review the diff as if I'm a senior reviewing your PR cold.

---

## 9. Handoff to Phase 2 (preview, not in scope)

So you know what's coming and don't accidentally build it now:

- Phase 2 will add `UrlShortener.Application` with use cases (`CreateShortUrlUseCase`, `RedirectUseCase`, etc.), `Result<T>`, `IShortCodeGenerator`, `IShortUrlRepository`, `IDateTimeProvider`, and `IDomainEventDispatcher` interfaces.
- Phase 3 will add `UrlShortener.Infrastructure` with EF Core, PostgreSQL/SQLite providers, repository implementations, the dispatcher implementation, and the `ShortUrlClickedEvent` handler that writes to `ClickAudits`.
- Phase 4 will add `UrlShortener.Api` with Minimal APIs, rate limiting on the redirect endpoint, OpenAPI, and DI wiring.
- Phase 5 will add GitHub Actions CI and the public-facing README.

Keep Phase 1 clean and the rest follows naturally.

---

## 10. Operating Conventions (repo-wide, all phases)

These conventions apply to **the entire lifetime of the repo (Phase 1 through Phase 5)**, not only to Phase 1. They govern how Claude Code interacts with git and how it communicates phase completion to the repo owner.

### 10.A — Conventions content

**1. Git authorship**
- Los commits los hacés vos directamente con `git commit -m "..."`. No pidas confirmación commit por commit, eso está definido en el spec.
- NO agregues "Co-authored-by: Claude" ni ninguna firma de IA en los mensajes de commit. La autoría queda con la identidad git configurada localmente.
- NO agregues emojis a los commits ni mensajes tipo "🤖 Generated with..."
- NO agregues firmas en el footer de los mensajes.

**2. Mensajes de commit**
- Seguí estrictamente la convención de Conventional Commits (sección 6 del spec): types y scopes definidos, granularidad TDD.
- Mensajes en inglés, presente, lowercase, sin punto final.
- Una línea, máximo 72 caracteres. Sin body extendido salvo que la decisión sea no-obvia.

**3. Push**
- NO hagas push automático. El push lo ejecuta el dueño del repo manualmente al terminar cada fase, después de verificar localmente que build y tests pasan.

**4. Comportamiento al terminar una fase**
- Asegurate que `dotnet build` y `dotnet test` corran verdes.
- Verificá los acceptance criteria de la sección correspondiente del spec.
- Comunicá "Phase N completa", listá los acceptance criteria con check, y esperá instrucciones. NO arranques la fase siguiente por iniciativa propia.

### 10.B — First action of Phase 1: create CLAUDE.md

The **very first action** of Phase 1, before scaffolding the .NET solution, before creating any project, before writing any code, is to create a file named `CLAUDE.md` at the root of the repository.

The contents of `CLAUDE.md` must be the verbatim text of section 10.A above (the four numbered blocks: Git authorship, Mensajes de commit, Push, Comportamiento al terminar una fase). You may add a short header at the top of the file (e.g. `# Operating Conventions` and a one-line description) but the four conventions blocks must appear unchanged.

**Why a separate file:** these conventions apply to all five phases of the repo. The phase-1 spec will be done with after Phase 1 ends; `CLAUDE.md` persists at repo root for the lifetime of the project and is the standard location Claude Code looks for repo-level instructions.

**The first commit of the repo must be:**

```
chore(repo): add CLAUDE.md with operating conventions
```

Only after that commit lands, proceed with the rest of Phase 1 (solution scaffold, .editorconfig/.gitignore, then the TDD cycle defined in sections 5 and 6).
