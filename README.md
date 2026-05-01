# url-shortener

A .NET 9 reference implementation of a URL shortener service, built as a portfolio piece demonstrating Clean Architecture, TDD, and modern .NET practices.

**Status: work in progress.** Phase 1 (Domain layer) is complete. Subsequent phases will add Application, Infrastructure (EF Core + PostgreSQL), API (ASP.NET Core Minimal APIs), and CI.

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

## Build and test

Run from repo root:

```shell
dotnet build
dotnet test
```

## License

MIT (to be added in Phase 5).
