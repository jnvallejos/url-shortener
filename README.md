# url-shortener

A .NET 9 reference implementation of a URL shortener service, built as a portfolio piece demonstrating Clean Architecture, TDD, and modern .NET practices.

**Status: work in progress.** Phases 1 (Domain), 2 (Application), and 3 (Infrastructure) are complete. Remaining phases will add the API layer (ASP.NET Core Minimal APIs with rate limiting and OpenAPI) and CI.

## What this demonstrates

- Clean Architecture with strict layer boundaries (Domain depends on nothing; Application depends on Domain; Infrastructure depends on Application; API will depend on all)
- Test-Driven Development with granular conventional commits
- Domain modeling with value objects, aggregate roots, domain events, and contextual exception messages
- Result pattern for use case error handling (no domain exceptions cross the Application boundary)
- Repository abstraction with EF Core 9 implementation
- Domain event dispatcher with handler registration via DI and reflection-based invocation
- EF Core value object conversions, unique indexes, and migrations
- Integration testing with SQLite in-memory plus end-to-end test wiring real components

## Tech stack

- .NET 9, C# 13
- EF Core 9 (Npgsql for PostgreSQL in production, SQLite in-memory for tests)
- xUnit, FluentAssertions, Moq
- Microsoft.Extensions.DependencyInjection

## Architecture

```
url-shortener/
├── docs/
│   ├── phase-1-spec.md
│   ├── phase-2-spec.md
│   └── phase-3-spec.md
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
│   └── UrlShortener.Infrastructure/            # depends on Application
│       ├── Codes/                              # Base62ShortCodeGenerator (crypto RNG)
│       ├── DependencyInjection/                # AddInfrastructure(connectionString) extension
│       ├── Events/                             # DomainEventDispatcher, IDomainEventHandler
│       │   └── Handlers/                       # ShortUrlClickedEventHandler
│       ├── Persistence/                        # ApplicationDbContext, design-time factory
│       │   ├── Configurations/                 # ShortUrlConfiguration, ClickAuditConfiguration
│       │   ├── Migrations/                     # InitialCreate + model snapshot
│       │   └── Repositories/                   # EfShortUrlRepository
│       └── Time/                               # SystemDateTimeProvider
├── tests/
│   ├── UrlShortener.Domain.Tests/              # 84 tests
│   ├── UrlShortener.Application.Tests/         # 93 tests, Moq-based
│   └── UrlShortener.Infrastructure.Tests/      # 49 tests
│       ├── Codes/
│       ├── DependencyInjection/
│       ├── EndToEnd/                           # RedirectFlowIntegrationTests
│       ├── Events/
│       ├── Persistence/                        # incl. MigrationApplyTests
│       ├── TestSupport/                        # SqliteInMemoryFixture
│       └── Time/
└── UrlShortener.sln
```

## Use cases shipped

- CreateShortUrl (with custom code support and Base62 generator retry on collision)
- Redirect (validates state, registers click, dispatches domain event, persists audit log)
- GetShortUrl (read-only by code, indifferent to disabled/expired state)
- DisableShortUrl, EnableShortUrl, UpdateExpiration (admin operations)

## Test coverage

226 tests, all green:

- 84 Domain unit tests
- 93 Application unit tests (Moq-based, sub-millisecond)
- 49 Infrastructure tests (mix of unit and integration with SQLite in-memory, including end-to-end redirect flow)

## Build and test

Run from repo root:

```shell
dotnet build
dotnet test
```

## Roadmap

- Phase 4: API layer with Minimal APIs, rate limiting on the redirect endpoint, OpenAPI/Swagger, error-to-HTTP mapping, structured logging
- Phase 5: GitHub Actions CI, public-facing README polish, MIT license

## License

MIT (to be added in Phase 5).
