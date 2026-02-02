# NetLift - Master Plan

> **Narzędzie do automatycznej migracji .NET Framework → .NET 8+**

---

## Vision

NetLift automatyzuje migrację legacy aplikacji .NET Framework (MVC, WCF, EF6) do nowoczesnego .NET 8+, redukując 60-80% manualnej pracy poprzez inteligentne transformacje kodu z Roslyn i AI-assisted refactoring.

---

## Current Status

| Metric | Value |
|--------|-------|
| **Phase** | ✅ Modernize Feature Complete |
| **Sprint** | Sprint 11 - Complete |
| **Progress** | 38/38 Modernize tasks |
| **Tasks Total** | 66 MVP + 2 P2 + 38 Modernize |
| **Tasks Done** | 106/106 |
| **Tests Passing** | 1567 (1533 unit + 34 integration) |
| **Last Updated** | 2026-02-02 |
| **Release** | v1.0.0 MVP ready, v2.0.0 Modernize ready |

**Completed:** `netlift modernize` command with CQRS, Clean Architecture, Auth Modernization, and Observability.
See [MODERNIZE.md](./MODERNIZE.md) for details.

---

## Quick Links

- [Architecture](./ARCHITECTURE.md) - System design & modules
- [Decisions Log](./DECISIONS.md) - ADR records
- [Agent Guide](./AGENT_GUIDE.md) - How agents should work
- [Current Tasks](./in-progress/) - What's being worked on

---

## MVP Scope

### ✅ IN SCOPE (MVP)

| Feature | Description |
|---------|-------------|
| **ASP.NET MVC 5 → Core** | Controllers, views, routing, filters |
| **Entity Framework 6 → Core** | DbContext, Fluent API, queries |
| **web.config → appsettings.json** | Config migration, Program.cs generation |
| **WCF → gRPC/REST** | Service contracts, data contracts |
| **Git Workflow** | Branch per phase, atomic commits |
| **Analysis Report** | Complexity scoring, compatibility check |
| **CLI Tool** | Cross-platform, .NET 8 |

### ❌ OUT OF SCOPE (Post-MVP)

- WebForms → Blazor
- WPF / WinForms migration
- Visual Studio Extension
- Web UI
- AI-assisted complex refactoring
- Enterprise licensing system
- On-premise deployment
- **Architecture Modernization** (see below)

---

## ✅ Post-MVP: Architecture Modernization (COMPLETE)

> **Status:** ✅ Complete | **Sprints:** 8-11

The `netlift modernize` command is fully implemented with the following features:

| Feature | Status | Description |
|---------|--------|-------------|
| **Clean Architecture** | ✅ | Generates Domain/Application/Infrastructure/Presentation structure |
| **CQRS Pattern** | ✅ | Commands, Queries, Handlers with MediatR |
| **FluentValidation** | ✅ | Generates validators from Data Annotations |
| **Repository Pattern** | ✅ | IRepository + Repository implementations |
| **Controller Transformation** | ✅ | Slims controllers to use MediatR |
| **Logic Extraction** | ✅ | Extracts business logic from controllers/services |
| **Auth Modernization** | ✅ | Membership/FormsAuth → ASP.NET Core Identity |
| **Observability** | ✅ | log4net/NLog → ILogger + OpenTelemetry |

### Implementation Details:

- **38 tasks** completed across 4 sprints
- **354 new tests** for modernization features
- Roslyn-based analysis and code generation
- Confidence scoring for all transformations

---

## Tech Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET | 8.0 |
| Code Analysis | Roslyn | 4.8.0 |
| CLI Framework | Spectre.Console | 0.48+ |
| Git Operations | LibGit2Sharp | 0.30+ |
| Config Files | YamlDotNet | 15.1+ |
| Testing | xUnit + FluentAssertions | Latest |
| AI (future) | Claude API | - |

---

## Sprint Overview

| Sprint | Focus | Tasks | Status | Key Deliverable |
|--------|-------|-------|--------|-----------------|
| **1** | Foundation | 14 | ✅ Done | CLI + Solution/Project parsing |
| **2** | Project Files | 10 | ✅ Done | .csproj SDK-style conversion |
| **3** | Configuration | 8 | ✅ Done | web.config → appsettings.json |
| **4** | MVC Controllers | 10 | ✅ Done | Controller/routing migration |
| **4b** | P2: Areas+Bundles | 2 | ✅ Done | MVC Areas, BundleConfig→Vite |
| **5** | Entity Framework | 8 | ✅ Done | EF6 → EF Core transforms |
| **6** | WCF Services | 10 | ✅ Done | WCF → gRPC/REST |
| **7** | Validation | 6 | ✅ Done | Build validation, reports |
| **8** | Modernize Foundation | 12 | ✅ Done | Core models, ControllerAnalyzer, CLI |
| **9** | CQRS Generators | 8 | ✅ Done | Command/Query/Handler generators |
| **10** | Clean Architecture | 6 | ✅ Done | Scaffolding, Repository generator |
| **11** | Logic + Auth + Observability | 12 | ✅ Done | Logic extraction, Auth, Logging |

**Legend:** 📋 Backlog | 🔄 In Progress | ✅ Done | 🚫 Blocked

---

## Milestones

- [x] **M1:** First successful .sln parse (Sprint 1) ✅
- [x] **M2:** .csproj migration working (Sprint 2) ✅
- [x] **M3:** web.config → appsettings.json (Sprint 3) ✅
- [x] **M4:** MVC controller migration (Sprint 4) ✅
- [x] **M5:** EF6 → EF Core working (Sprint 5) ✅
- [x] **M6:** WCF → gRPC basic (Sprint 6) ✅
- [x] **M7:** MVP complete with validation (Sprint 7) ✅

---

## Risk Register

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Roslyn learning curve | High | Medium | Start with simple transforms, good docs exist |
| WCF duplex complexity | High | High | Scope down - warning only for duplex, no auto-migration |
| Edge cases in real projects | Medium | High | Build comprehensive test fixtures |
| .NET Framework SDK needed for Roslyn | Medium | Low | Use AdhocWorkspace for syntax-only analysis |

---

## Test Fixtures

Projects for testing migrations:

| Fixture | Type | Purpose | Status |
|---------|------|---------|--------|
| `mvc5-basic` | ASP.NET MVC 5 | Minimal MVC app | ✅ Created |
| `mvc5-with-auth` | ASP.NET MVC 5 | + Forms Authentication | 📋 Planned |
| `mvc5-with-ef6` | ASP.NET MVC 5 + EF6 | + Entity Framework | 📋 Planned |
| `mvc5-areas` | ASP.NET MVC 5 | + Areas, complex routing | 📋 Planned |
| `wcf-basic` | WCF Service | Simple service contract | 📋 Planned |
| `wcf-duplex` | WCF Service | Callback contracts | 📋 Planned |
| `ef6-complex` | EF6 | TPH, TPT, complex Fluent API | 📋 Planned |

---

## Success Metrics

| Metric | Target |
|--------|--------|
| Auto-migration rate (MVC) | > 75% |
| Auto-migration rate (EF6) | > 70% |
| Auto-migration rate (config) | > 90% |
| Auto-migration rate (WCF basic) | > 60% |
| Build success after migration | > 80% |
| Time saved vs manual | > 60% |

---

## Notes

- Focus on **correctness over speed** - wrong migration is worse than slow migration
- Every transformation must have **confidence score**
- Git commits must be **atomic and reviewable**
- When in doubt, **generate TODO comment** instead of guessing

---

*Last updated: 2026-02-02*
