# url-shortener

A .NET 9 reference implementation of a URL shortener service, built as a portfolio piece demonstrating Clean Architecture, TDD, and modern .NET practices.

**Status: work in progress.** Phases 1 (Domain), 2 (Application), 3 (Infrastructure), and 4 (API) are complete. Remaining work is Phase 5: GitHub Actions CI, README polish, and MIT license.

## What this demonstrates

- Clean Architecture with strict layer boundaries (Domain depends on nothing; Application depends on Domain; Infrastructure depends on Application; API depends on Application + Infrastructure)
- Test-Driven Development with granular conventional commits
- Domain modeling with value objects, aggregate roots, domain events, and contextual exception messages
- Result pattern for use case error handling (no domain exceptions cross the Application boundary)
- Repository abstraction with EF Core 9 implementation
- Domain event dispatcher with handler registration via DI and reflection-based invocation
- EF Core value object conversions, unique indexes, and migrations
- Integration testing with SQLite in-memory plus end-to-end test wiring real components
- Functional API testing via `WebApplicationFactory<Program>` against the full ASP.NET Core pipeline
- Minimal APIs with route constraints, fixed-window rate limiting, OpenAPI document generation, Scalar UI, structured logging, and Result-to-HTTP error mapping

## Tech stack

- .NET 9, C# 13
- ASP.NET Core Minimal APIs, `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`
- EF Core 9 (Npgsql for PostgreSQL in production, SQLite in-memory for tests)
- xUnit, FluentAssertions, Moq, `Microsoft.AspNetCore.Mvc.Testing`
- Microsoft.Extensions.DependencyInjection

## Architecture

```
url-shortener/
├── docs/
│   ├── phase-1-spec.md
│   ├── phase-2-spec.md
│   ├── phase-3-spec.md
│   └── phase-4-spec.md
├── src/
│   ├── UrlShortener.Domain/                    # zero external deps
│   │   ├── Common/                             # Entity, ValueObject, IDomainEvent
│   │   ├── Exceptions/                         # DomainException + concrete subtypes
│   │   ├── ShortUrls/                          # ShortUrl aggregate, ShortCode, OriginalUrl
│   │   │   └── Events/                         # ShortUrlClickedEvent
│   │   └── ClickAudits/                        # ClickAudit record
│   ├── UrlShortener.Application/               # depends on Domain
│   │   ├── Abstractions/                       # IShortUrlRepository, IShortCodeGenerator,
│   │   │                                       #   IDateTimeProvider, IDomainEventDispatcher
│   │   ├── Common/                             # Result<T>, Error, Errors catalogue
│   │   ├── DependencyInjection/                # AddApplication() extension
│   │   └── ShortUrls/
│   │       ├── Create/                         # CreateShortUrlUseCase + request/response
│   │       ├── GetByCode/                      # GetShortUrlUseCase + request/response
│   │       ├── Redirect/                       # RedirectUseCase + request/response
│   │       └── Admin/
│   │           ├── Disable/                    # DisableShortUrlUseCase + request/response
│   │           ├── Enable/                     # EnableShortUrlUseCase + request/response
│   │           └── UpdateExpiration/           # UpdateExpirationUseCase + request/response
│   ├── UrlShortener.Infrastructure/            # depends on Application
│   │   ├── Codes/                              # Base62ShortCodeGenerator (crypto RNG)
│   │   ├── DependencyInjection/                # AddInfrastructure(connectionString) extension
│   │   ├── Events/                             # DomainEventDispatcher, IDomainEventHandler
│   │   │   └── Handlers/                       # ShortUrlClickedEventHandler
│   │   ├── Persistence/                        # ApplicationDbContext, design-time factory
│   │   │   ├── Configurations/                 # ShortUrlConfiguration, ClickAuditConfiguration
│   │   │   ├── Migrations/                     # InitialCreate + model snapshot
│   │   │   └── Repositories/                   # EfShortUrlRepository
│   │   └── Time/                               # SystemDateTimeProvider
│   └── UrlShortener.Api/                       # depends on Application + Infrastructure
│       ├── Configuration/                      # RateLimitingOptions
│       ├── Contracts/                          # CreateShortUrlContract, ShortUrlContract,
│       │                                       #   ShortUrlStateContract, ShortUrlExpirationContract,
│       │                                       #   UpdateExpirationContract, ErrorResponse
│       ├── Endpoints/                          # ShortUrlsEndpoints, RedirectEndpoint
│       ├── ErrorMapping/                       # ErrorToHttpResultMapper (Error.Code → HTTP status)
│       └── Program.cs                          # hosting, DI wiring, rate limiter, OpenAPI, Scalar
├── tests/
│   ├── UrlShortener.Domain.Tests/              # 84 tests
│   ├── UrlShortener.Application.Tests/         # 93 tests, Moq-based
│   ├── UrlShortener.Infrastructure.Tests/      # 49 tests
│   │   ├── Codes/
│   │   ├── DependencyInjection/
│   │   ├── EndToEnd/                           # RedirectFlowIntegrationTests
│   │   ├── Events/
│   │   ├── Persistence/                        # incl. MigrationApplyTests
│   │   ├── TestSupport/                        # SqliteInMemoryFixture
│   │   └── Time/
│   └── UrlShortener.Api.Tests/                 # 57 tests (functional via WebApplicationFactory)
│       ├── Endpoints/                          # one test class per endpoint
│       ├── ErrorMapping/                       # ErrorToHttpResultMapperTests
│       ├── OpenApi/                            # OpenApiDocumentTests
│       ├── RateLimiting/                       # RedirectRateLimitTests
│       └── TestSupport/                        # ApiWebApplicationFactory, TestClock,
│                                               #   RateLimitedApiFactory, DevelopmentApiFactory
└── UrlShortener.sln
```

## Use cases shipped

- CreateShortUrl (with custom code support and Base62 generator retry on collision)
- Redirect (validates state, registers click, dispatches domain event, persists audit log)
- GetShortUrl (read-only by code, indifferent to disabled/expired state)
- DisableShortUrl, EnableShortUrl, UpdateExpiration (admin operations)

## HTTP endpoints

| Method | Path                                       | Use case          |
| ------ | ------------------------------------------ | ----------------- |
| POST   | `/api/shorturls`                           | CreateShortUrl    |
| GET    | `/{code}`                                  | Redirect          |
| GET    | `/api/shorturls/{code}`                    | GetShortUrl       |
| POST   | `/api/shorturls/{code}/disable`            | DisableShortUrl   |
| POST   | `/api/shorturls/{code}/enable`             | EnableShortUrl    |
| PATCH  | `/api/shorturls/{code}/expiration`         | UpdateExpiration  |

The redirect route is constrained to 7-character Base62 codes (`length(7):regex(^[A-Za-z0-9]+$)`) and protected by a fixed-window rate limiter (100 req/min per IP, configurable). Errors from the Application layer are mapped to HTTP status codes via `ErrorToHttpResultMapper`, switching on `Error.Code` (the stable contract) — never on the message text.

In Development, the OpenAPI document is served at `/openapi/v1.json` and Scalar UI at `/scalar/v1`.

## Test coverage

283 tests, all green (2 functional tests skipped with documented justifications):

- 84 Domain unit tests
- 93 Application unit tests (Moq-based, sub-millisecond)
- 49 Infrastructure tests (mix of unit and integration with SQLite in-memory, including end-to-end redirect flow)
- 57 API functional tests via `WebApplicationFactory<Program>` (SQLite in-memory swap, controllable test clock, rate-limit and OpenAPI-document coverage)

## Build and test

Run from repo root:

```shell
dotnet build
dotnet test
```

## Run the API locally

The API targets PostgreSQL via the `DefaultConnection` connection string in `appsettings.json`. Apply the migration once before the first run:

```shell
dotnet ef database update --project src/UrlShortener.Infrastructure --startup-project src/UrlShortener.Api
dotnet run --project src/UrlShortener.Api
```

The API does not auto-migrate at startup by design (Phase 4 spec section 8.4). For a different connection string, override via the standard ASP.NET Core configuration env var: `ConnectionStrings__DefaultConnection`.

## Roadmap

- Phase 5: GitHub Actions CI, public-facing README polish, MIT license

## License

MIT (to be added in Phase 5).
