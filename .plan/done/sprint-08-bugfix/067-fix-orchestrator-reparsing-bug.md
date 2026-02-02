# [TASK-067] Fix MigrationOrchestrator re-parsing converted project

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P0 (Critical Bug) |
| **Estimate** | M |
| **Sprint** | 8 (Bugfix) |
| **Agent** | Claude |
| **Started** | 2026-02-01 |
| **Completed** | 2026-02-01 |

## Dependencies

- **Depends on:** (none)
- **Blocks:** TASK-068

---

## Description

**BUG:** Controllers, DAL, logging, and other C# source files are NOT being migrated during `netlift migrate`.

**Root Cause:**
In `MigrateCommand.MigrateProjectAsync()`:
1. Line 339: Project is parsed with `OldFormatProjectParser` → gets `CompileItems` from old format
2. Line 431: The converted SDK-style project is **WRITTEN TO DISK**
3. Line 451: `_orchestrator.MigrateProjectAsync(projectPath)` is called

Then in `MigrationOrchestrator.MigrateProjectAsync()`:
- Line 107: `_projectParser.AnalyzeAsync(projectPath)` reads the **ALREADY CONVERTED** SDK-style file
- `OldFormatProjectParser` sees SDK-style format → returns **empty CompileItems**
- Line 134: `if (projectInfo.CompileItems.Any())` is **FALSE** → source transformations SKIPPED

**Result:** Only configuration files (appsettings.json, Program.cs) are generated. Controllers, models, DbContext, etc. are NOT transformed.

---

## Acceptance Criteria

- [ ] `MigrationOrchestrator` receives `ProjectInfo` directly instead of re-parsing
- [ ] All C# source files are transformed (controllers, models, DbContext, etc.)
- [ ] Running migration on ContosoUniversity transforms all controllers
- [ ] Existing tests pass
- [ ] New test validates source file transformation after project conversion

---

## Technical Notes

### Solution: Pass ProjectInfo to Orchestrator

Modify the orchestrator to accept `ProjectInfo` instead of just the project path.

### Files to modify:

1. **`src/NetLift.Core/Interfaces/IMigrationOrchestrator.cs`**
   - Change signature to accept `ProjectInfo projectInfo` instead of `string projectPath`

2. **`src/NetLift.Transforms/MigrationOrchestrator.cs`**
   - Update `MigrateProjectAsync` to use passed `ProjectInfo`
   - Remove internal call to `_projectParser.AnalyzeAsync()`

3. **`src/NetLift.Cli/Commands/MigrateCommand.cs`**
   - Pass the already-parsed `projectInfo` to the orchestrator

### Example fix in MigrateCommand.cs:

```csharp
// Before (buggy):
var migrationResult = await _orchestrator.MigrateProjectAsync(
    projectRef.AbsolutePath,  // Path only - orchestrator re-parses!
    settings.TargetFramework,
    migrationOptions,
    CancellationToken.None);

// After (fixed):
var migrationResult = await _orchestrator.MigrateProjectAsync(
    projectInfo,  // Pass already-parsed ProjectInfo
    settings.TargetFramework,
    migrationOptions,
    CancellationToken.None);
```

### Key decision:
- The orchestrator should NOT re-parse the project file
- The caller (MigrateCommand) is responsible for providing accurate ProjectInfo

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2026-02-01 | Claude | Created - Critical bug identified |

---
