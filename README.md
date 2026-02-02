# NetLift

Automated migration from .NET Framework to .NET 8+.

## Why

.NET Framework is no longer supported. Microsoft stopped releasing updates in 2024, and security patches won't last forever. If you're running MVC 5 or Entity Framework 6 in production, you're on borrowed time.

Manual migration works, but it's slow. You're updating namespaces, rewriting DbContext constructors, converting XML config to JSON, fixing using statements. File after file. Most of it is mechanical work that follows predictable patterns.

NetLift handles the mechanical parts. You focus on the code that actually needs human judgment.

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
- Razor view namespaces

**Entity Framework**
- DbContext constructors and configuration
- Fluent API relationships (HasRequired, HasMany)
- Include/ThenInclude patterns
- Raw SQL queries

**WCF Services**
- Service contracts to gRPC or REST
- Data contracts to DTOs
- Client proxy generation

## Example

See a real migration in action:

[ContosoUniversity migration PR](https://github.com/wojciechowskiapp/ContosoUniversity.LegacyMigration/pull/1)

## Quick start

```bash
git clone https://github.com/wojciechowskiapp/NetLift.git
cd NetLift
dotnet build
```

Analyze your solution first:

```bash
dotnet run --project src/NetLift.Cli -- analyze YourSolution.sln
```

Then migrate with dry-run to preview changes:

```bash
dotnet run --project src/NetLift.Cli -- migrate YourSolution.sln --dry-run
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
dotnet run --project src/NetLift.Cli -- analyze Solution.sln
dotnet run --project src/NetLift.Cli -- analyze Solution.sln --html  # HTML report
```

**migrate** - Run the migration

```bash
dotnet run --project src/NetLift.Cli -- migrate Solution.sln --dry-run      # Preview
dotnet run --project src/NetLift.Cli -- migrate Solution.sln --verbose      # Run
dotnet run --project src/NetLift.Cli -- migrate Solution.sln --interactive  # Step by step
```

**modernize** - Generate CQRS structure (run after migrate)

```bash
dotnet run --project src/NetLift.Cli -- modernize Solution.sln --dry-run
dotnet run --project src/NetLift.Cli -- modernize Solution.sln --pattern cqrs
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

The test suite covers parsers, transformers, and end-to-end migration scenarios.

## License

Dual licensed under AGPL-3.0 and a commercial license.

**Open source projects**: Free under AGPL-3.0. Your code must also be open source.

**Commercial use**: Paid license if you want to keep your code proprietary.

See [LICENSE](LICENSE) or contact hello@wojciechowski.app for details.
