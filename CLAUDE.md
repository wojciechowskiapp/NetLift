# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NetLift is a CLI tool for automated migration of .NET Framework → .NET 8+.

**Migration scope:** ASP.NET MVC 5, Entity Framework 6, web.config, WCF → ASP.NET Core, EF Core, appsettings.json, gRPC/REST

**Modernize scope:** CQRS with MediatR, Clean Architecture scaffolding, FluentValidation, Auth (Identity → JWT), OpenTelemetry

**Out of Scope:** WebForms, WPF, WinForms

## Commands

```bash
dotnet build                                              # Build solution
dotnet test                                               # Run all tests
dotnet test --filter "FullyQualifiedName~ClassName"       # Run tests matching class
dotnet test --filter "Name~MethodName"                    # Run tests matching method

# CLI usage
dotnet run --project src/NetLift.Cli -- analyze MySolution.sln --html
dotnet run --project src/NetLift.Cli -- migrate MySolution.sln --dry-run
dotnet run --project src/NetLift.Cli -- modernize MySolution.sln --pattern cqrs --dry-run
```

## Architecture

```
NetLift.Cli        → Spectre.Console commands, DI setup
NetLift.Core       → Models & interfaces only (no logic)
NetLift.Analysis   → Parsers (.sln, .csproj, packages.config), type detection
NetLift.Transforms → Roslyn rewriters, organized by domain:
                     ├── Ef/           (DbContext, FluentAPI, queries)
                     ├── Mvc/          (controllers, routing, filters, Razor)
                     ├── Wcf/          (service contracts → gRPC/REST)
                     ├── Modernization/ (CQRS, Clean Architecture, validators)
                     └── SignalR/      (Hub migration)
NetLift.Validation → Build validation, HTML reports
NetLift.Git        → LibGit2Sharp, branch-per-phase, auto-commits
```

All interfaces in `NetLift.Core/Interfaces/`, implementations in respective projects.
DI registration in `src/NetLift.Cli/Program.cs`.

## Confidence Scoring

Every transformation must output confidence score:
- **95-100%**: Auto-apply, no review needed
- **80-94%**: Auto-apply with INFO comment
- **60-79%**: Apply with TODO, recommend review
- **<60%**: Don't auto-apply, generate manual task

When uncertain, generate TODO comment instead of guessing.

## Key Patterns

1. **Roslyn** (`Microsoft.CodeAnalysis.CSharp`) for C# parsing/transformation
2. **XDocument** for XML parsing (old .csproj, packages.config, web.config)
3. **YAML rules** for package mappings: `src/NetLift.Transforms/Configuration/package-mappings.yml`
4. **Git workflow**: Branch-per-phase, atomic commits via `IBranchStrategy` and `ICommitGenerator`
5. **Test fixture**: `tests/fixtures/mvc5-basic/` - minimal MVC5 project for parser testing
