# NetLift

[![Build](https://github.com/wojciechowskiapp/NetLift/actions/workflows/build.yml/badge.svg)](https://github.com/wojciechowskiapp/NetLift/actions/workflows/build.yml)

Automated migration from .NET Framework to .NET 8+.

## Why

.NET Framework is no longer supported. Microsoft stopped releasing updates in 2024, and security patches won't last forever. If you're running MVC 5 or Entity Framework 6 in production, you're on borrowed time.

Manual migration works, but it's slow. You're updating namespaces, rewriting DbContext constructors, converting XML config to JSON, fixing using statements. File after file. Most of it is mechanical work that follows predictable patterns.

Migration is also a good time to fix architectural debt. Legacy codebases often have business logic buried in controllers, missing separation of concerns, no clear command/query boundaries. Refactoring this by hand while also migrating frameworks is a lot to take on at once.

NetLift handles the mechanical parts. The `migrate` command gets you to .NET 8. The `modernize` command goes further and restructures your code into CQRS patterns with proper separation between commands, queries, and handlers. You end up with a codebase that's not just on a supported framework, but actually easier to work with.

## Examples

<img width="889" height="633" alt="image" src="https://github.com/user-attachments/assets/bce1b6d2-f304-4ab2-8852-3306eb9c78d9" />
<img width="422" height="575" alt="image" src="https://github.com/user-attachments/assets/83367c75-825d-436c-ac2d-697ee09ae01e" />

## Real migrations you can review:

- [ContosoUniversity](https://github.com/wojciechowskiapp/ContosoUniversity.LegacyMigration/pull/2) - Academic sample app with students, courses, enrollments
- [MvcMusicStore](https://github.com/wojciechowskiapp/MvcMusicStore.LegacyMigration/pull/2) - E-commerce app with shopping cart, checkout, CQRS modernization
- [eShopModernizing](https://github.com/wojciechowskiapp/eShopModernizing.LegacyMigration/pull/2) - Microsoft's reference app, multi-project solution with catalog management

## How it works

NetLift uses Roslyn to parse your C# code into syntax trees, applies transformations, and writes the results back. No AI, no language models, no randomness. Same input produces the same output every time.

This matters because you need to trust your migration tooling. When NetLift changes a file, you can review the diff and understand exactly what happened. There are no hallucinations, no invented code, no surprises.

The tool also knows when it's uncertain. Instead of guessing, it adds a TODO comment and moves on. You decide how to handle edge cases.

## What gets migrated

**Project structure**
- `.csproj` old format to SDK-style
- `packages.config` to PackageReference
- Assembly info extraction

**Configuration**
- `web.config` to `appsettings.json`
- Connection strings, app settings, environment configs
- `Program.cs` generation with middleware setup

**ASP.NET MVC**
- Controllers from `System.Web.Mvc` to `Microsoft.AspNetCore.Mvc`
- Action results, filters, routing attributes
- `HttpContext.Current` to injected `IHttpContextAccessor`

**Razor Views**
- Html helpers to Tag Helpers (`Html.TextBoxFor` → `<input asp-for="">`)
- Form helpers, validation, labels, dropdowns, checkboxes
- `@Scripts.Render` and `@Styles.Render` to direct script/link tags
- Partial views and display templates

**Entity Framework**
- DbContext constructors and configuration
- Fluent API relationships (HasRequired, HasMany)
- Include/ThenInclude patterns
- Raw SQL queries

**Static Files**
- `Content` folder → `wwwroot/css`
- `Scripts` folder → `wwwroot/js`
- Fixes CSS `url()` paths after moving files
- Root files like `favicon.ico` and `robots.txt` handled

**SignalR**
- Legacy SignalR hubs to ASP.NET Core SignalR
- `GlobalHost.ConnectionManager` to injected `IHubContext`
- Client method calls and group management

**WCF Services**
- Service contracts to gRPC or REST
- Data contracts to DTOs
- Client proxy generation

**DI Container Analysis**
- Detects Autofac, Unity, Ninject, StructureMap registrations
- Maps container lifetimes to Microsoft.Extensions.DependencyInjection
- Identifies property injection and interceptors that need manual review

**Modernization** (optional, run after migrate)
- CQRS structure with MediatR - generates commands, queries, and handlers
- Controller slimming - extracts business logic into separate services
- FluentValidation - creates validators from data annotations
- Infrastructure patterns - Result types, logging behaviors, transaction handling
- Manual `.Select()` projections for DTO mapping (same SQL efficiency as AutoMapper)

## Quick start

```bash
git clone https://github.com/wojciechowskiapp/NetLift.git
cd NetLift
dotnet build
```

**1. Analyze** - see what you're working with

```bash
dotnet run --project src/NetLift.Cli -- analyze YourSolution.sln --verbose
```

**2. Migrate** - convert to .NET 8

```bash
dotnet run --project src/NetLift.Cli -- migrate YourSolution.sln --verbose
```

**3. Modernize** - restructure to CQRS (optional)

```bash
dotnet run --project src/NetLift.Cli -- modernize YourSolution.sln --pattern cqrs --verbose
```

## Confidence scoring

Not all transformations are equal. A namespace rename is safe. A complex EF relationship might need review.

NetLift assigns a confidence score to every change:

| Score | Action | What it means |
|-------|--------|---------------|
| 95-100% | Auto-applied | Safe, mechanical transformation |
| 80-94% | Applied + INFO | Likely correct, worth a glance |
| 60-79% | Applied + TODO | Needs human review |
| Below 60% | Skipped | Manual task generated |

When the tool isn't sure, it tells you. No silent failures, no hidden assumptions.

## Commands

**analyze** - Scan a solution and report migration readiness

```bash
dotnet run --project src/NetLift.Cli -- analyze Solution.sln --verbose
dotnet run --project src/NetLift.Cli -- analyze Solution.sln --html --verbose
```

**migrate** - Run the migration

```bash
dotnet run --project src/NetLift.Cli -- migrate Solution.sln --verbose
dotnet run --project src/NetLift.Cli -- migrate Solution.sln --interactive --verbose
```

**modernize** - Generate CQRS structure (run after migrate)

```bash
dotnet run --project src/NetLift.Cli -- modernize Solution.sln --verbose
dotnet run --project src/NetLift.Cli -- modernize Solution.sln --pattern cqrs --verbose
```

## Limitations

NetLift focuses on web applications and services. These are not supported:

- WebForms
- WPF
- WinForms

These frameworks have fundamentally different architectures. Automated migration would produce code that technically compiles but misses the point. For these, a rewrite makes more sense than a migration.

## Tests

```bash
dotnet test
```

1,819 tests covering parsers, transformers, and end-to-end migration scenarios.

## License

Dual licensed under AGPL-3.0 and a commercial license.

**Open source projects**: Free under AGPL-3.0. Your code must also be open source.

**Commercial use**: Paid license if you want to keep your code proprietary.

See [LICENSE](LICENSE) or contact hello@wojciechowski.app for details.
