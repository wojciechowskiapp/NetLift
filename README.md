# NetLift

CLI tool that migrates .NET Framework projects to .NET 8+. No AI involved - just deterministic Roslyn-based code transformations.

## What it does

Takes your old ASP.NET MVC 5 / Entity Framework 6 codebase and converts it to ASP.NET Core / EF Core. Also handles web.config to appsettings.json, packages.config to PackageReference, and WCF services to gRPC or REST.

The `modernize` command goes further - generates CQRS structure with commands, queries, and handlers.

## Install

```bash
git clone https://github.com/wojciechowskiapp/NetLift.git
cd NetLift
dotnet build
```

## Usage

```bash
# See what you're dealing with
dotnet run --project src/NetLift.Cli -- analyze YourSolution.sln

# Migrate to .NET 8 (use --dry-run first)
dotnet run --project src/NetLift.Cli -- migrate YourSolution.sln --dry-run
dotnet run --project src/NetLift.Cli -- migrate YourSolution.sln --verbose

# Modernize to CQRS (optional, run after migrate)
dotnet run --project src/NetLift.Cli -- modernize YourSolution.sln --dry-run
dotnet run --project src/NetLift.Cli -- modernize YourSolution.sln --verbose
```

## What gets migrated

- `.csproj` old format → SDK-style
- `packages.config` → PackageReference
- `web.config` → `appsettings.json` + `Program.cs`
- ASP.NET MVC 5 controllers → ASP.NET Core
- Entity Framework 6 → EF Core
- WCF services → gRPC or REST

## Confidence scoring

Every transformation outputs a confidence score:

- **95-100%** - Auto-applied, safe
- **80-94%** - Applied with INFO comment
- **60-79%** - Applied with TODO, needs review
- **<60%** - Not applied, generates manual task

When unsure, the tool adds a TODO instead of guessing.

## What's not supported (yet)

- WebForms
- WPF
- WinForms

## Roadmap

- [ ] FluentValidation generation
- [ ] Auth modernization (Identity → JWT/OAuth)
- [ ] OpenTelemetry integration
- [ ] SignalR migration

## Running tests

```bash
dotnet test
```

1567 tests, all passing.

## License

MIT
