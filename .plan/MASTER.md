# NetLift - Master Plan

> **Narzędzie do automatycznej migracji .NET Framework → .NET 8+**

---

## Vision

NetLift automatyzuje migrację legacy aplikacji .NET Framework (MVC, WCF, EF6) do nowoczesnego .NET 8+, redukując 60-80% manualnej pracy poprzez inteligentne transformacje kodu z Roslyn i AI-assisted refactoring.

---

## Current Status

| Metric | Value |
|--------|-------|
| **Phase** | ✅ MVP Complete |
| **Sprint** | Sprint 7 ✅ Complete |
| **Progress** | 100% (66/66 MVP tasks) |
| **Tasks Total** | 66 MVP + 2 P2 |
| **Tasks Done** | 66 MVP |
| **Tests Passing** | 1339 (1320 unit + 19 integration) |
| **Last Updated** | 2026-02-01 |
| **Release** | v1.0.0 ready |

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

## 🔮 Post-MVP: Architecture Modernization

> **Status:** Planned | **Requires:** Further analysis and verification

Po zakończeniu MVP (lift and shift), planowany moduł `netlift modernize` do modernizacji architektury:

### Potencjalne funkcje:

| Feature | Opis |
|---------|------|
| **Clean Architecture** | Generowanie struktury folderów (Domain, Application, Infrastructure, Presentation) |
| **CQRS Pattern** | Wykrywanie command/query i separacja z MediatR |
| **Layer Separation** | Automatyczne wydzielanie warstw z monolitu |
| **DDD Patterns** | Identyfikacja agregatów, encji, value objects |
| **Dependency Injection** | Refaktoryzacja do proper DI patterns |
| **Repository Pattern** | Abstrakcja dostępu do danych |

### Wymagana analiza:

- [ ] Badanie wykrywalności wzorców w legacy code
- [ ] Określenie confidence scoring dla refaktoryzacji architektury
- [ ] Analiza ryzyka automatycznej modernizacji
- [ ] Benchmarking vs manualna refaktoryzacja
- [ ] Integracja z AI dla złożonych decyzji architektonicznych

**Uwaga:** Modernizacja architektury jest znacznie bardziej ryzykowna niż lift-and-shift. Wymaga głębszej analizy semantycznej kodu i prawdopodobnie integracji z AI (Claude API) dla podejmowania decyzji architektonicznych.

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

*Last updated: 2026-02-01*
