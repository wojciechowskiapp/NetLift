# NetLift - Architecture

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              NetLift CLI                                │
│                    (Entry point, command routing)                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐                 │
│  │   ANALYZE   │    │  TRANSFORM  │    │  VALIDATE   │                 │
│  │   Command   │───▶│   Command   │───▶│   Command   │                 │
│  └─────────────┘    └─────────────┘    └─────────────┘                 │
│         │                  │                  │                         │
├─────────┼──────────────────┼──────────────────┼─────────────────────────┤
│         ▼                  ▼                  ▼                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                      NetLift.Core                                │   │
│  │  (Models, Interfaces, Extensions, Shared Logic)                  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│         │                  │                  │                         │
├─────────┼──────────────────┼──────────────────┼─────────────────────────┤
│         ▼                  ▼                  ▼                         │
│  ┌───────────┐      ┌───────────┐      ┌───────────┐                   │
│  │ Analysis  │      │ Transforms│      │ Validation│                   │
│  │  Engine   │      │  Engine   │      │  Engine   │                   │
│  └───────────┘      └───────────┘      └───────────┘                   │
│       │                   │                   │                         │
│       │    ┌──────────────┴──────────────┐   │                         │
│       │    │                             │   │                         │
│       ▼    ▼                             ▼   ▼                         │
│  ┌─────────────────┐              ┌─────────────────┐                  │
│  │  Roslyn Engine  │              │  Git Operations │                  │
│  │  (Code parsing  │              │  (LibGit2Sharp) │                  │
│  │   & rewriting)  │              │                 │                  │
│  └─────────────────┘              └─────────────────┘                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
NetLift/
├── src/
│   ├── NetLift.Cli/                    # CLI application
│   │   ├── Commands/
│   │   │   ├── AnalyzeCommand.cs
│   │   │   ├── MigrateCommand.cs
│   │   │   ├── ValidateCommand.cs
│   │   │   └── ReportCommand.cs
│   │   ├── Infrastructure/
│   │   │   └── DependencyInjection.cs
│   │   └── Program.cs
│   │
│   ├── NetLift.Core/                   # Shared models & interfaces
│   │   ├── Models/
│   │   │   ├── Solution/
│   │   │   │   ├── SolutionInfo.cs
│   │   │   │   ├── ProjectInfo.cs
│   │   │   │   └── DependencyGraph.cs
│   │   │   ├── Analysis/
│   │   │   │   ├── AnalysisReport.cs
│   │   │   │   ├── CompatibilityIssue.cs
│   │   │   │   └── MigrationComplexity.cs
│   │   │   ├── Transform/
│   │   │   │   ├── TransformResult.cs
│   │   │   │   ├── TransformPhase.cs
│   │   │   │   └── ConfidenceScore.cs
│   │   │   └── Config/
│   │   │       ├── WebConfigModel.cs
│   │   │       └── AppSettingsModel.cs
│   │   ├── Interfaces/
│   │   │   ├── IProjectAnalyzer.cs
│   │   │   ├── ITransformer.cs
│   │   │   ├── IValidator.cs
│   │   │   └── IGitOperations.cs
│   │   └── Extensions/
│   │       ├── StringExtensions.cs
│   │       └── RoslynExtensions.cs
│   │
│   ├── NetLift.Analysis/               # Analysis engine
│   │   ├── Analyzers/
│   │   │   ├── SolutionAnalyzer.cs
│   │   │   ├── ProjectAnalyzer.cs
│   │   │   ├── DependencyAnalyzer.cs
│   │   │   └── ConfigAnalyzer.cs
│   │   ├── Detectors/
│   │   │   ├── ProjectTypeDetector.cs
│   │   │   ├── MvcDetector.cs
│   │   │   ├── WcfDetector.cs
│   │   │   └── EfDetector.cs
│   │   └── Reports/
│   │       ├── ReportGenerator.cs
│   │       └── HtmlReportBuilder.cs
│   │
│   ├── NetLift.Transforms/             # Transformation engine
│   │   ├── Engine/
│   │   │   ├── TransformEngine.cs
│   │   │   ├── TransformPipeline.cs
│   │   │   └── TransformContext.cs
│   │   ├── Project/
│   │   │   ├── CsprojTransformer.cs
│   │   │   ├── PackagesConfigTransformer.cs
│   │   │   └── SolutionTransformer.cs
│   │   ├── Config/
│   │   │   ├── WebConfigTransformer.cs
│   │   │   ├── AppSettingsGenerator.cs
│   │   │   └── ProgramCsGenerator.cs
│   │   ├── Mvc/
│   │   │   ├── NamespaceRewriter.cs
│   │   │   ├── ControllerTransformer.cs
│   │   │   ├── RoutingTransformer.cs
│   │   │   └── FilterTransformer.cs
│   │   ├── Ef/
│   │   │   ├── DbContextTransformer.cs
│   │   │   ├── FluentApiTransformer.cs
│   │   │   └── QueryTransformer.cs
│   │   └── Wcf/
│   │       ├── ServiceContractAnalyzer.cs
│   │       ├── DataContractTransformer.cs
│   │       ├── GrpcGenerator.cs
│   │       └── RestApiGenerator.cs
│   │
│   ├── NetLift.Validation/             # Validation engine
│   │   ├── BuildValidator.cs
│   │   ├── TestRunner.cs
│   │   ├── SemanticDiffAnalyzer.cs
│   │   └── ConfidenceCalculator.cs
│   │
│   └── NetLift.Git/                    # Git operations
│       ├── GitOperations.cs
│       ├── BranchStrategy.cs
│       └── CommitGenerator.cs
│
├── rules/                              # Declarative transformation rules
│   ├── namespaces.yaml                 # Namespace mappings
│   ├── nuget-mappings.yaml             # Package replacements
│   ├── api-mappings.yaml               # API equivalents
│   └── ef-mappings.yaml                # EF6 → EF Core mappings
│
├── tests/
│   ├── NetLift.Tests.Unit/
│   │   ├── Analysis/
│   │   ├── Transforms/
│   │   └── Validation/
│   └── NetLift.Tests.Integration/
│       └── EndToEnd/
│
├── test-fixtures/                      # Sample legacy projects
│   ├── mvc5-basic/
│   ├── mvc5-with-auth/
│   ├── mvc5-with-ef6/
│   ├── wcf-basic/
│   └── ef6-complex/
│
└── NetLift.sln
```

---

## Module Responsibilities

### NetLift.Cli
- Parse command line arguments
- Route to appropriate command handlers
- Display progress and results (Spectre.Console)
- Handle errors gracefully

### NetLift.Core
- Define shared models (ProjectInfo, TransformResult, etc.)
- Define interfaces (ITransformer, IValidator)
- Provide extension methods
- No business logic, just contracts

### NetLift.Analysis
- Parse .sln and .csproj files
- Detect project types (MVC, WCF, etc.)
- Build dependency graph
- Generate compatibility reports
- Calculate migration complexity

### NetLift.Transforms
- Execute code transformations using Roslyn
- Apply declarative rules from YAML files
- Generate new files (Program.cs, appsettings.json)
- Track confidence scores

### NetLift.Validation
- Verify migrated code compiles
- Run existing tests
- Compare before/after semantics
- Calculate overall confidence

### NetLift.Git
- Create branches for migration phases
- Generate meaningful commits
- Support rollback operations

---

## Key Interfaces

```csharp
// Analysis
public interface IProjectAnalyzer
{
    Task<ProjectInfo> AnalyzeAsync(string projectPath);
}

public interface ISolutionAnalyzer
{
    Task<SolutionInfo> AnalyzeAsync(string solutionPath);
}

// Transformation
public interface ITransformer
{
    string Name { get; }
    TransformPhase Phase { get; }
    Task<TransformResult> TransformAsync(TransformContext context);
}

public interface ITransformPipeline
{
    void AddTransformer(ITransformer transformer);
    Task<PipelineResult> ExecuteAsync(SolutionInfo solution);
}

// Validation
public interface IValidator
{
    Task<ValidationResult> ValidateAsync(string projectPath);
}

// Git
public interface IGitOperations
{
    Task<string> CreateBranchAsync(string branchName);
    Task CommitAsync(string message, IEnumerable<string> files);
    Task<bool> RollbackAsync(string commitHash);
}
```

---

## Data Flow

```
1. ANALYZE
   Input: Solution path
   Process: Parse → Detect → Report
   Output: AnalysisReport (what needs migration, complexity)

2. MIGRATE
   Input: AnalysisReport + Options
   Process: For each phase:
            - Create branch
            - Apply transformers
            - Commit changes
   Output: Migrated code in Git branches

3. VALIDATE
   Input: Migrated solution
   Process: Build → Test → Diff
   Output: ValidationReport (success, failures, confidence)
```

---

## Transform Pipeline

```
┌──────────────┐
│  Solution    │
│  Analysis    │
└──────┬───────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│   Phase 1    │───▶│   Phase 2    │───▶│   Phase 3    │───▶ ...
│  .csproj     │    │   Config     │    │  Namespaces  │
└──────────────┘    └──────────────┘    └──────────────┘
       │                   │                   │
       ▼                   ▼                   ▼
   [Git Commit]       [Git Commit]       [Git Commit]
```

Each phase:
1. Creates a Git branch (or continues on migration branch)
2. Applies all transformers for that phase
3. Validates the result compiles
4. Commits with descriptive message

---

## Confidence Scoring

```
Score Range    | Action
─────────────────────────────────────
95-100%       | Auto-apply, no review needed
80-94%        | Auto-apply with INFO comment
60-79%        | Apply with WARNING, recommend review
40-59%        | Apply with TODO, require review
0-39%         | Don't apply, generate manual task
```

Factors affecting confidence:
- Pattern matching certainty
- API mapping directness (1:1 vs complex)
- Semantic equivalence verification
- Test coverage of transformed code

---

## Rules Engine

YAML-based declarative rules for simple mappings:

```yaml
# namespaces.yaml
mappings:
  - from: "System.Web.Mvc"
    to: "Microsoft.AspNetCore.Mvc"
    confidence: 100

  - from: "System.Web.Http"
    to: "Microsoft.AspNetCore.Mvc"
    confidence: 95
    notes: "Web API controllers now use same base"

# nuget-mappings.yaml
packages:
  - from: "Microsoft.AspNet.Mvc"
    to: "Microsoft.AspNetCore.Mvc"
    minVersion: "2.2.0"

  - from: "EntityFramework"
    to: "Microsoft.EntityFrameworkCore.SqlServer"
    notes: "May need additional EF Core packages"
```

---

## Error Handling Strategy

1. **Recoverable errors** → Log warning, continue with reduced confidence
2. **Non-recoverable errors** → Stop current transform, rollback to last commit
3. **Unknown patterns** → Generate TODO comment, continue

All errors logged to:
- Console (summarized)
- `netlift-report/errors.log` (detailed)
- HTML report (human-readable)

---

## Future Extensions (Post-MVP)

- **AI Module:** Claude API for complex refactoring
- **VS Extension:** Visual Studio integration
- **Web UI:** Browser-based interface
- **Plugin System:** Custom transformers
- **Cloud Service:** SaaS version

---

*Last updated: 2025-01-31*
