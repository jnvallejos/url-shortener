# url-shortener

A .NET 9 reference implementation of a URL shortener service, built as a portfolio piece demonstrating Clean Architecture, TDD, and modern .NET practices.

**Status: work in progress.** Phases 1 (Domain), 2 (Application), and 3 (Infrastructure) are complete. Subsequent phases will add the API (ASP.NET Core Minimal APIs) and CI.

## Tech stack

- .NET 9, C# 13
- xUnit, FluentAssertions, Moq for testing
- Clean Architecture (Domain, Application, Infrastructure, API)
- Test-Driven Development with conventional commits

## Phase 1 progress

- Pure domain layer with zero external dependencies
- 84 unit tests, all green
- Aggregate: ShortUrl with ShortCode and OriginalUrl value objects
- Domain events (ShortUrlClickedEvent) raised by aggregate
- ClickAudit record for audit log entries
- Exception hierarchy with contextual messages

## Phase 2 progress

- Application layer orchestrating the Domain
- Use cases: CreateShortUrl, Redirect, GetShortUrl
- `Result<T>` pattern with stable `Errors` catalogue (no exceptions cross the boundary)
- Abstractions for Phase 3: `IShortUrlRepository`, `IShortCodeGenerator`, `IDateTimeProvider`, `IDomainEventDispatcher`
- `services.AddApplication()` extension for DI wiring
- 60 Application unit tests (Moq + FluentAssertions), all green

## Phase 3 progress

- Infrastructure layer with EF Core 9 and PostgreSQL (Npgsql)
- Concrete implementations of every Phase 2 abstraction: `EfShortUrlRepository`, `Base62ShortCodeGenerator`, `SystemDateTimeProvider`, `DomainEventDispatcher`
- `ApplicationDbContext` with entity configurations that map value objects (`ShortCode`, `OriginalUrl`) via `HasConversion`
- Domain event handler `ShortUrlClickedEventHandler` writing audit rows to the `ClickAudits` table
- Three admin use cases added to the Application layer: `DisableShortUrlUseCase`, `EnableShortUrlUseCase`, `UpdateExpirationUseCase`
- `services.AddInfrastructure(connectionString)` extension wiring everything for the host
- Initial EF migration committed; round-trip and end-to-end integration tests run against SQLite in-memory
- 49 Infrastructure tests (unit + integration) plus 33 new Application tests, all green

## Build and test

Run from repo root:

```shell
dotnet build
dotnet test
```

## License

MIT (to be added in Phase 5).
