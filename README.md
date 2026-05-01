# url-shortener

A .NET 9 reference implementation of a URL shortener service, built as a portfolio piece demonstrating Clean Architecture, TDD, and modern .NET practices.

**Status: work in progress.** Phases 1 (Domain) and 2 (Application) are complete. Subsequent phases will add Infrastructure (EF Core + PostgreSQL), API (ASP.NET Core Minimal APIs), and CI.

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

## Build and test

Run from repo root:

```shell
dotnet build
dotnet test
```

## License

MIT (to be added in Phase 5).
