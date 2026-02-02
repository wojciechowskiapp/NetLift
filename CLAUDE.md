# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NetLift is a CLI tool for automated migration of .NET Framework → .NET 8+.

**MVP Scope:**
- ASP.NET MVC 5 → ASP.NET Core (controllers, routing, filters)
- Entity Framework 6 → EF Core (DbContext, Fluent API, queries)
- web.config → appsettings.json + Program.cs
- WCF → gRPC or REST API (service/data contracts)

**Out of Scope (MVP):** WebForms, WPF, WinForms

**Post-MVP (implemented):** `netlift modernize` command for CQRS, Clean Architecture, FluentValidation, Auth Modernization, Observability (see `.plan/MODERNIZE.md`)

## Commands

```bash
dotnet build                                    # Build solution
dotnet test                                     # Run all 1567 tests
dotnet test --filter "FullyQualifiedName~Name" # Run specific tests

# CLI usage
dotnet run --project src/NetLift.Cli -- analyze MySolution.sln --html
dotnet run --project src/NetLift.Cli -- migrate MySolution.sln --dry-run --interactive
dotnet run --project src/NetLift.Cli -- modernize MySolution.sln --pattern cqrs --dry-run
```

## Architecture

```
NetLift.Cli        → Entry point, Spectre.Console commands
NetLift.Core       → Models & interfaces only (no logic)
NetLift.Analysis   → Parsers (.sln, .csproj, packages.config), type detection
NetLift.Transforms → Roslyn transformers, SDK converter, package mapping
NetLift.Validation → Build validation, HTML reports
NetLift.Git        → LibGit2Sharp, branch-per-phase, auto-commits
```

All interfaces in `NetLift.Core/Interfaces/`, implementations in respective projects.
DI registration in `src/NetLift.Cli/Program.cs`.

## MVP Status

| Sprint | Focus | Tasks | Status |
|--------|-------|-------|--------|
| 1 | Foundation | 14 | ✅ Done |
| 2 | Project Files | 10 | ✅ Done |
| 3 | Configuration | 8 | ✅ Done |
| 4 | MVC Controllers | 10 | ✅ Done |
| 5 | Entity Framework | 8 | ✅ Done |
| 6 | WCF → gRPC/REST | 10 | ✅ Done |
| 7 | Validation & E2E | 6 | ✅ Done |
| 8 | Modernize Foundation | 12 | ✅ Done |
| 9 | CQRS Generators | 8 | ✅ Done |
| 10 | Clean Architecture | 6 | ✅ Done |
| 11 | Logic Extraction + Auth/Observability | 12 | ✅ Done |

**MVP Complete:** 66/66 tasks, **Modernize Complete:** 38/38 tasks, **Total:** 1567 tests passing.

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
