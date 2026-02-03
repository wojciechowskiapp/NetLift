# NetLift - Feature Backlog

> Priority-ranked feature backlog for future development

---

## Priority Legend

| Tier | Priority | Description |
|------|----------|-------------|
| **T1** | Critical | Must have for complete .NET Framework → .NET 8 migration |
| **T2** | High | Common patterns, high business value |
| **T3** | Medium | Nice to have, situational |
| **T4** | Low | Edge cases, rarely needed |

---

## Current Sprint (Sprint 12)

| Task | Status | Priority | Description |
|------|--------|----------|-------------|
| SignalR DI Registration | **Pending** | T1 | Register SignalR services in Program.cs |

---

## Tier 1 - Critical (Must Have)

### Sprint 17-18: Razor Views Migration ✅ COMPLETE
**Status:** ✅ Complete | **Plan:** [RAZOR_VIEWS_PLAN.md](./backlog/sprint-17-18/RAZOR_VIEWS_PLAN.md)

Transform MVC5 Razor views to ASP.NET Core Razor syntax

| Feature | Confidence | Status |
|---------|-----------|--------|
| Html.ActionLink → asp-action | 95% | ✅ |
| Html.BeginForm → form tag helper | 95% | ✅ |
| Html.EditorFor → asp-for | 90% | ✅ |
| Html.ValidationSummary → asp-validation | 90% | ✅ |
| Bundling references → wwwroot | 85% | ✅ |
| @Scripts.Render/@Styles.Render | 80% | ✅ |

**Added:** 75 unit tests (46 transformer + 29 analyzer)

---

### Sprint 19: Static Files & wwwroot Migration ✅ COMPLETE
**Status:** ✅ Complete | **Plan:** [STATIC_FILES_PLAN.md](./backlog/sprint-19/STATIC_FILES_PLAN.md)

Migrate Content/Scripts folders to wwwroot structure

| Feature | Confidence | Status |
|---------|-----------|--------|
| Content → wwwroot/css | 95% | ✅ |
| Scripts → wwwroot/js | 95% | ✅ |
| Images → wwwroot/images | 95% | ✅ |
| Static file middleware | 100% | ✅ |
| Path references | 85% | ✅ |

**Added:** 32 unit tests for static files analyzer

---

### Sprint 13-16: DI Container Migration 🔄 IN PROGRESS
**Status:** Foundation Complete | **Plan:** [DI_CONTAINER_MIGRATION_PLAN.md](./backlog/sprint-12/DI_CONTAINER_MIGRATION_PLAN.md)

Autofac, Unity, Ninject, StructureMap → Microsoft.Extensions.DependencyInjection

| Component | Status |
|-----------|--------|
| DIContainerModels | ✅ |
| DI Interfaces (6) | ✅ |
| di-lifetime-mappings.yml | ✅ |
| DIContainerDetector | ✅ |
| LifetimeMapper | ✅ |
| AutofacAnalyzer | ✅ |
| UnityAnalyzer | 📋 |
| NinjectAnalyzer | 📋 |
| StructureMapAnalyzer | 📋 |
| DIContainerTransformer | 📋 |

**Business Value:** High - Most legacy MVC5 apps use third-party DI containers

---

## Tier 2 - High Value

### Sprint 20: HTTP Modules → Middleware
**Status:** Not Planned | **Plan:** TBD

Convert Global.asax handlers and custom HTTP modules to ASP.NET Core middleware

| Feature | Confidence | Description |
|---------|-----------|-------------|
| Application_Start → Program.cs | 90% | Startup logic migration |
| Application_BeginRequest | 80% | Request pipeline middleware |
| Application_AuthenticateRequest | 75% | Auth middleware |
| Application_Error | 85% | Exception middleware |
| Custom IHttpModule | 70% | Middleware generation |

**Business Value:** High - Many apps have custom Global.asax logic

---

### Sprint 21: Caching Modernization
**Status:** Mentioned | **Plan:** TBD

System.Web.Caching → IMemoryCache/IDistributedCache

| Feature | Confidence | Description |
|---------|-----------|-------------|
| HttpRuntime.Cache | 85% | IMemoryCache replacement |
| System.Web.Caching | 85% | IMemoryCache replacement |
| OutputCacheAttribute | 75% | Response caching middleware |
| Custom cache providers | 65% | IDistributedCache |

**Business Value:** High - Caching is common in MVC5 apps

---

### Sprint 22: Background Services
**Status:** Mentioned | **Plan:** TBD

Windows Services and timers → IHostedService/BackgroundService

| Feature | Confidence | Description |
|---------|-----------|-------------|
| System.Threading.Timer | 85% | BackgroundService |
| Scheduled tasks | 80% | IHostedService + timer |
| Windows Service → Worker | 70% | Worker Service template |
| Hangfire detection | 90% | Keep Hangfire (compatible) |

**Business Value:** Medium-High - Many apps have scheduled jobs

---

### Sprint 23: JavaScript Modernization
**Status:** Not Planned | **Plan:** TBD

jQuery usage detection and modernization guidance

| Feature | Confidence | Description |
|---------|-----------|-------------|
| $.ajax detection | Info only | Suggest Fetch API |
| jQuery plugin inventory | Info only | Identify dependencies |
| ES6+ suggestions | Info only | Modern JS patterns |
| npm package suggestions | Info only | Modern replacements |

**Business Value:** Medium - Reduces technical debt, improves maintainability

---

## Tier 3 - Medium Priority

### Test Project Migration
**Status:** Not Planned

MSTest/NUnit test project compatibility checking

| Feature | Description |
|---------|-------------|
| TestContext differences | Document changes |
| Integration test setup | WebApplicationFactory |
| Mock framework compatibility | Moq, NSubstitute |

---

### Localization Modernization
**Status:** Not Planned

Resource files → IStringLocalizer

| Feature | Description |
|---------|-------------|
| .resx file handling | IStringLocalizer setup |
| Culture configuration | RequestLocalizationMiddleware |
| View localization | IViewLocalizer |

---

## Tier 4 - Low Priority

### API Versioning
Only needed for versioned Web APIs

### Advanced Security
- OWIN → ASP.NET Core authentication (partially done)
- Custom authentication schemes

### WebSockets (non-SignalR)
Rare, low priority

---

## Gap Analysis Summary

| Category | Status | Gap Level |
|----------|--------|-----------|
| Project Files (.csproj, .sln) | ✅ Complete | None |
| Configuration (web.config) | ✅ Complete | None |
| MVC Controllers | ✅ Complete | None |
| Entity Framework | ✅ Complete | None |
| WCF Services | ✅ Complete | None |
| SignalR | ✅ Complete | None |
| Modernization (CQRS, etc.) | ✅ Complete | None |
| **Razor Views** | ✅ Complete | None |
| **Static Files/wwwroot** | ✅ Complete | None |
| DI Containers | 🔄 In Progress | Foundation done |
| HTTP Modules | 📋 Planned | High |
| Caching | 📋 Planned | High |
| Background Services | 📋 Planned | Medium |
| JavaScript | 📋 Planned | Medium |
| Testing | 📋 Planned | Low |
| Localization | 📋 Planned | Low |

---

## Recommended Implementation Order

1. ✅ Razor Views Migration (Sprint 17-18) - DONE
2. ✅ Static Files/wwwroot (Sprint 19) - DONE
3. 🔄 DI Container Migration (Sprint 13-16) - IN PROGRESS
4. **Next:** HTTP Modules (Sprint 20) - High value
5. **Later:** Caching, Background Services, JavaScript

---

*Last updated: 2026-02-03*
