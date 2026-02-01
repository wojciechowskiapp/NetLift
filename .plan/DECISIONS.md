# NetLift - Architecture Decision Records (ADR)

> Log wszystkich ważnych decyzji architektonicznych i technicznych

---

## Format ADR

```markdown
## [ADR-XXX] Title

- **Date:** YYYY-MM-DD
- **Status:** Proposed | Accepted | Deprecated | Superseded
- **Context:** Why we need to make this decision
- **Decision:** What we decided
- **Consequences:** What happens as a result
- **Alternatives Considered:** What else we looked at
```

---

## Decisions

---

### [ADR-001] Use Roslyn for code analysis and transformation

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
Need to parse and transform C# code reliably. Options: regex, text manipulation, Roslyn, or third-party parsers.

**Decision:**
Use Microsoft.CodeAnalysis (Roslyn) Workspace API for all code analysis and transformation.

**Consequences:**
- ✅ 100% accurate C# parsing (same parser as compiler)
- ✅ Semantic analysis available (understand what symbols mean)
- ✅ Type-safe transformations via SyntaxRewriter
- ✅ Well documented, maintained by Microsoft
- ❌ Learning curve for Roslyn APIs
- ❌ Heavier dependency than regex

**Alternatives Considered:**
- Regex: Too fragile, can't handle all C# syntax
- Tree-sitter: Good but less C#-specific
- Custom parser: Too much work, error-prone

---

### [ADR-002] Use Spectre.Console for CLI

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
Need rich CLI experience with progress bars, tables, colors.

**Decision:**
Use Spectre.Console and Spectre.Console.Cli for command-line interface.

**Consequences:**
- ✅ Beautiful output (tables, trees, progress)
- ✅ Strong typing for commands
- ✅ Cross-platform
- ❌ Additional dependency

**Alternatives Considered:**
- System.CommandLine: Official but less mature for rich output
- Cocona: Good but less rich UI
- Plain Console: Too basic for our needs

---

### [ADR-003] Git-native workflow with branch-per-phase

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
Migrations can fail partially. Need easy rollback and review capability.

**Decision:**
Each migration phase creates atomic Git commits. Optionally create branches per major phase.

**Consequences:**
- ✅ Easy rollback to any point
- ✅ Reviewable PRs for each phase
- ✅ Clear audit trail
- ❌ Requires Git repository (acceptable constraint)
- ❌ Slightly more complex implementation

**Alternatives Considered:**
- File backups: Messy, no diff capability
- Single commit: Hard to review large changes
- No versioning: Too risky

---

### [ADR-004] Use LibGit2Sharp for Git operations

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
Need to perform Git operations (branch, commit, etc.) programmatically.

**Decision:**
Use LibGit2Sharp for native Git operations without shelling out.

**Consequences:**
- ✅ No Git CLI dependency
- ✅ Proper error handling
- ✅ Cross-platform
- ❌ Native library dependency (platform-specific binaries)

**Alternatives Considered:**
- Shell out to git: Requires Git installed, parsing output
- GitLib2: Same as LibGit2Sharp, just different wrapper

---

### [ADR-005] YAML-based declarative rules for simple mappings

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
Many transformations are simple mappings (namespace A → namespace B). Need easy way to add/modify these.

**Decision:**
Store simple mappings in YAML files under `rules/` directory. Complex transformations remain in code.

**Consequences:**
- ✅ Easy to add new mappings without code changes
- ✅ User-editable for customization
- ✅ Version-controllable
- ❌ Another format to parse
- ❌ Can't express complex logic (by design)

**Alternatives Considered:**
- JSON: Less readable for humans
- C# code only: Harder to modify/extend
- DSL: Overkill for simple mappings

---

### [ADR-006] Confidence scoring for all transformations

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
Not all transformations are equally reliable. Users need to know what to review.

**Decision:**
Every transformation outputs a confidence score (0-100%). Low confidence items require human review.

**Consequences:**
- ✅ Transparency about automation quality
- ✅ Prioritized review list for humans
- ✅ Can tune thresholds per use case
- ❌ Need to calculate/estimate confidence for each transform

**Scoring guide:**
- 95-100%: Auto-apply without review
- 80-94%: Auto-apply with info comment
- 60-79%: Apply with TODO, recommend review
- <60%: Generate manual task, don't auto-apply

---

### [ADR-007] Prioritize MVC → Core over other migrations

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
Multiple migration types needed (MVC, WCF, EF). Need to decide order.

**Decision:**
Priority order: MVC → EF → Config → WCF. MVC is most common and foundational.

**Consequences:**
- ✅ Maximum value in MVP
- ✅ EF often comes with MVC (combined value)
- ❌ WCF users wait longer

**Alternatives Considered:**
- WCF first: Smaller market
- All parallel: Too much scope for solo dev

---

### [ADR-008] Skip WPF/WinForms in MVP

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
WPF and WinForms can run on .NET Core/5+ with minimal changes (unlike web apps).

**Decision:**
Exclude WPF and WinForms from MVP scope. Focus on web (MVC, WCF) and data (EF).

**Consequences:**
- ✅ Reduced scope
- ✅ Focus on higher-value migrations
- ❌ Some potential customers excluded

**Rationale:**
WPF/WinForms migration is mostly project file changes - Microsoft's tooling handles this adequately. Our value-add is web/service layer migration where tooling is weak.

---

### [ADR-009] Support both gRPC and REST output for WCF

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
WCF can map to either gRPC (similar semantics) or REST APIs.

**Decision:**
Support both output formats. Default to gRPC for internal services, REST for public APIs.

**Consequences:**
- ✅ Flexibility for different use cases
- ✅ gRPC for performance-critical
- ✅ REST for browser clients
- ❌ Two code paths to maintain

**Selection criteria:**
- HasCallback → gRPC (streaming)
- Public API → REST (broader compatibility)
- Internal service → gRPC (performance)

---

### [ADR-010] AdhocWorkspace for syntax-only analysis

- **Date:** 2025-01-31
- **Status:** Accepted

**Context:**
Full Roslyn Workspace requires .NET SDK to resolve references. Legacy .NET Framework projects may not have SDK installed.

**Decision:**
Use AdhocWorkspace for syntax-only analysis. Use full workspace only when semantic analysis is required and SDK is available.

**Consequences:**
- ✅ Works without .NET Framework SDK
- ✅ Faster for simple transforms
- ❌ Limited semantic info in some scenarios
- ❌ Some transforms need full workspace anyway

**Mitigation:**
Gracefully degrade when full workspace unavailable. Document which transforms need semantic analysis.

---

## Template for new ADR

```markdown
### [ADR-XXX] Title

- **Date:** YYYY-MM-DD
- **Status:** Proposed

**Context:**
[Why we need to make this decision]

**Decision:**
[What we decided]

**Consequences:**
- ✅ Benefit 1
- ✅ Benefit 2
- ❌ Downside 1

**Alternatives Considered:**
- Alternative 1: [Why rejected]
- Alternative 2: [Why rejected]
```

---

*Last updated: 2025-01-31*
