# NetLift Modernize - Architecture Modernization Roadmap

> **Command:** `netlift modernize` - Transform legacy architecture to Clean Architecture + CQRS

---

## Overview

The `modernize` command transforms traditional ASP.NET MVC applications with fat controllers and direct DbContext usage into Clean Architecture with CQRS pattern using MediatR.

**Key transformations:**
- Fat Controllers → Slim Controllers + MediatR dispatch
- Direct DbContext → Repository pattern
- Data Annotations → FluentValidation
- Anemic Models → Rich Domain Models (stubs)
- ViewBag → Strongly-typed ViewModels

---

## Current Status

| Metric | Value |
|--------|-------|
| **Phase** | ✅ Sprints 8-11 COMPLETE |
| **Progress** | 38/38 tasks |
| **Tests** | 1567 (354 new for modernization) |
| **Last Updated** | 2026-02-02 |

---

## Sprint Roadmap

### Sprint 8: Foundation ✅ COMPLETE
> **Goal:** CLI command + Core models + Controller analyzer

| # | Task | Status | Description |
|---|------|--------|-------------|
| 069 | Create ModernizationOptions model | ✅ | Options for modernize command |
| 070 | Create ModernizationResult model | ✅ | Result structure with generated files |
| 071 | Create ControllerInfo model | ✅ | Represents analyzed controller |
| 072 | Create ActionInfo model | ✅ | Represents controller action method |
| 073 | Create CommandInfo/QueryInfo models | ✅ | CQRS representations |
| 074 | Create IControllerAnalyzer interface | ✅ | Controller analysis contract |
| 075 | Create IModernizationOrchestrator interface | ✅ | Main orchestration contract |
| 076 | Implement ControllerAnalyzer | ✅ | Roslyn-based controller analysis |
| 077 | Add ControllerAnalyzer tests | ✅ | 40 unit tests for analyzer |
| 078 | Create ModernizeCommand for CLI | ✅ | Spectre.Console command |
| 079 | Implement ModernizationOrchestrator stub | ✅ | Basic orchestrator implementation |
| 080 | Wire up DI in Program.cs | ✅ | Register new services |

### Sprint 9: CQRS Generators ✅ COMPLETE
> **Goal:** Generate Commands, Queries, and Handlers

| # | Task | Status | Description |
|---|------|--------|-------------|
| 081 | Create ICommandGenerator interface | ✅ | Command generation contract (Sprint 8) |
| 082 | Create IQueryGenerator interface | ✅ | Query generation contract (Sprint 8) |
| 083 | Create IHandlerGenerator interface | ✅ | Handler generation contract |
| 084 | Implement CommandGenerator | ✅ | Generate Command classes + 13 tests |
| 085 | Implement QueryGenerator | ✅ | Generate Query classes |
| 086 | Implement HandlerGenerator | ✅ | Generate Result, DTOs, DbContext + 35 tests |
| 087 | Implement ValidatorGenerator | ✅ | FluentValidation + 24 tests |
| 088 | Wire up DI + orchestrator | ✅ | Full integration complete |

### Sprint 10: Clean Architecture Scaffolding ✅ COMPLETE
> **Goal:** Generate project structure and repositories

| # | Task | Status | Description |
|---|------|--------|-------------|
| 089 | Create IProjectScaffolder interface | ✅ | Project scaffolding contract |
| 090 | Create IRepositoryGenerator interface | ✅ | Repository generation contract |
| 091 | Implement CleanArchitectureScaffolder | ✅ | Generate Domain/Application/Infrastructure (29 tests) |
| 092 | Implement RepositoryGenerator | ✅ | Generate IRepository + Repository (23 tests) |
| 093 | Generate DI registration code | ✅ | MediatR + repositories DI setup |
| 094 | Add scaffolding tests | ✅ | Integration tests |

### Sprint 11: Logic Extraction + Auth + Observability ✅ COMPLETE
> **Goal:** Extract business logic, transform controllers, modernize auth and logging

| # | Task | Status | Description |
|---|------|--------|-------------|
| 095 | ServiceInfo/ExtractedLogic Models | ✅ | Models for service analysis |
| 096 | IServiceAnalyzer + ServiceAnalyzer | ✅ | Roslyn-based service class analysis |
| 097 | ILogicExtractor + LogicExtractor | ✅ | Extract business logic from method bodies |
| 098 | IControllerTransformer + ControllerSlimmer | ✅ | Transform controllers to use MediatR (9 tests) |
| 099 | BusinessLogicBuilder | ✅ | Convert extracted logic to handler code |
| 100 | Complete ModernizationOrchestrator | ✅ | Full orchestration with logic extraction |
| 101 | Auth Modernization Analyzer | ✅ | Detect [Authorize], Membership, FormsAuth |
| 102 | Auth Modernization Generator | ✅ | Generate ASP.NET Core Identity code |
| 103 | Observability Analyzer | ✅ | Detect log4net, NLog, Enterprise Library |
| 104 | Observability Generator | ✅ | Generate ILogger, health checks, OpenTelemetry |
| 105 | E2E Tests for Modernize | ✅ | 15 comprehensive E2E tests |
| 106 | DI Registration | ✅ | All new services registered |

---

## Architecture

### File Structure

```
src/NetLift.Transforms/
├── Modernization/
│   ├── Analyzers/
│   │   ├── ControllerAnalyzer.cs
│   │   ├── ServiceAnalyzer.cs
│   │   ├── LogicExtractor.cs
│   │   ├── AuthenticationAnalyzer.cs     # Auth pattern detection
│   │   └── LoggingAnalyzer.cs            # Logging framework detection
│   ├── Generators/
│   │   ├── CommandGenerator.cs
│   │   ├── QueryGenerator.cs
│   │   ├── HandlerGenerator.cs
│   │   ├── ValidatorGenerator.cs
│   │   ├── RepositoryGenerator.cs
│   │   ├── BusinessLogicBuilder.cs
│   │   ├── AuthenticationGenerator.cs    # Identity code generation
│   │   └── ObservabilityGenerator.cs     # ILogger/OpenTelemetry generation
│   ├── Scaffolding/
│   │   └── CleanArchitectureScaffolder.cs
│   ├── Transformers/
│   │   └── ControllerSlimmer.cs
│   └── ModernizationOrchestrator.cs

src/NetLift.Core/
├── Models/Modernization/
│   ├── ModernizationOptions.cs
│   ├── ModernizationResult.cs
│   ├── ControllerInfo.cs
│   ├── ActionInfo.cs
│   ├── CommandInfo.cs
│   ├── QueryInfo.cs
│   ├── ParameterInfo.cs
│   ├── ServiceInfo.cs
│   ├── ExtractedLogic.cs
│   ├── ActionLogicContext.cs
│   ├── AuthenticationInfo.cs             # Auth detection results
│   ├── AuthModernizationResult.cs        # Generated auth code
│   ├── LoggingInfo.cs                    # Logging detection results
│   └── ObservabilityResult.cs            # Generated observability code
├── Interfaces/Modernization/
│   ├── IControllerAnalyzer.cs
│   ├── ICommandGenerator.cs
│   ├── IQueryGenerator.cs
│   ├── IHandlerGenerator.cs
│   ├── IValidatorGenerator.cs
│   ├── IModernizationOrchestrator.cs
│   ├── IServiceAnalyzer.cs
│   ├── ILogicExtractor.cs
│   ├── IControllerTransformer.cs
│   ├── IAuthenticationAnalyzer.cs        # Auth analysis interface
│   ├── IAuthenticationGenerator.cs       # Auth generation interface
│   ├── ILoggingAnalyzer.cs               # Logging analysis interface
│   └── IObservabilityGenerator.cs        # Observability generation interface
```

### Detection Patterns

**Command Detection (Write operations):**
```csharp
// Detect by: SaveChanges, Add, Update, Remove, Delete
if (method.DescendantNodes().Any(n =>
    n.ToString().Contains("SaveChanges") ||
    n.ToString().Contains(".Add(") ||
    n.ToString().Contains(".Remove(")))
{
    // Generate Command + Handler
}
```

**Query Detection (Read operations):**
```csharp
// Detect by: HttpGet without SaveChanges, or return type
if (method.HasAttribute("HttpGet") && !HasSaveChanges(method))
{
    // Generate Query + Handler
}
```

---

## Confidence Scoring

| Transformation | Confidence | Notes |
|---------------|-----------|-------|
| Controller → CQRS | 95% | Fully automated |
| Data Annotations → FluentValidation | 95% | Direct mapping |
| ViewBag → ViewModel | 90% | May need manual refinement |
| Repository extraction | 85% | Query complexity varies |
| Domain model enrichment | 60% | Generates stubs with TODOs |

---

## Usage

```bash
# Analyze modernization potential
netlift modernize MySolution.sln --analyze-only --output=report.html

# Dry run (preview changes)
netlift modernize MySolution.sln --dry-run

# Apply modernization
netlift modernize MySolution.sln --pattern=cqrs --interactive

# Full modernization
netlift modernize MySolution.sln \
    --pattern=cqrs \
    --pattern=clean-architecture \
    --pattern=fluentvalidation \
    --confidence-threshold=80
```

---

## Dependencies

New packages required for generated code:
- `MediatR` (12.0.0+) - CQRS dispatch
- `FluentValidation` (11.0.0+) - Validation
- `AutoMapper` (12.0.0+) - DTO mapping (optional)

---

## Notes

- Modernization is **additive** - original code preserved in git history
- When in doubt, generate **TODO comments** for manual review
- Focus on **high-confidence transformations** first
- Generated code follows **Clean Architecture conventions**

---

*Last updated: 2026-02-02*
