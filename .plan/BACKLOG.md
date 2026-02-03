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

### Sprint 13-16: DI Container Migration
**Status:** Planned | **Plan:** [DI_CONTAINER_MIGRATION_PLAN.md](./backlog/sprint-12/DI_CONTAINER_MIGRATION_PLAN.md)

Autofac, Unity, Ninject, StructureMap → Microsoft.Extensions.DependencyInjection

| Sprint | Tasks | Focus |
|--------|-------|-------|
| 13 | 12 | Foundation models, Autofac analysis |
| 14 | 12 | Unity, Ninject, StructureMap analysis |
| 15 | 10 | Transformation & generation |
| 16 | 10 | Advanced scenarios, polish |

**Business Value:** High - Most legacy MVC5 apps use third-party DI containers

---

### Sprint 17-18: Razor Views Migration
**Status:** Gap Identified | **Plan:** [RAZOR_VIEWS_PLAN.md](./backlog/sprint-17-18/RAZOR_VIEWS_PLAN.md)

Transform MVC5 Razor views to ASP.NET Core Razor syntax

| Feature | Confidence | Description |
|---------|-----------|-------------|
| Html.ActionLink → asp-action | 95% | Tag helper replacement |
| Html.BeginForm → form tag helper | 95% | Form tag helper |
| Html.EditorFor → asp-for | 90% | Input tag helpers |
| Html.ValidationSummary → asp-validation | 90% | Validation tag helpers |
| Bundling references → wwwroot | 85% | Script/link tag updates |
| _ViewStart.cshtml → _ViewImports | 95% | View configuration |
| @Scripts.Render/@Styles.Render | 80% | Bundle reference removal |

**Business Value:** Critical - Every MVC app has Razor views, currently not migrated!

---

### Sprint 19: Static Files & wwwroot Migration
**Status:** Gap Identified | **Plan:** [STATIC_FILES_PLAN.md](./backlog/sprint-19/STATIC_FILES_PLAN.md)

Migrate Content/Scripts folders to wwwroot structure

| Feature | Confidence | Description |
|---------|-----------|-------------|
| Content → wwwroot/css | 95% | CSS file relocation |
| Scripts → wwwroot/js | 95% | JS file relocation |
| Images → wwwroot/images | 95% | Image relocation |
| Static file middleware | 100% | Program.cs configuration |
| Path references | 85% | Update ~/Content to ~/css |

**Business Value:** Critical - All web apps need static file serving in .NET Core

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
| SignalR | ✅ 95% Complete | Minor (DI reg) |
| Modernization (CQRS, etc.) | ✅ Complete | None |
| **Razor Views** | ❌ Not Started | **Critical** |
| **Static Files/wwwroot** | ❌ Not Started | **Critical** |
| DI Containers | 📋 Planned | Detailed plan exists |
| HTTP Modules | ❌ Not Started | High |
| Caching | ❌ Not Started | High |
| Background Services | ❌ Not Started | Medium |
| JavaScript | ❌ Not Started | Medium |
| Testing | ❌ Not Started | Low |
| Localization | ❌ Not Started | Low |

---

## Recommended Implementation Order

1. **Now:** Finish SignalR DI Registration (Sprint 12)
2. **Next:** Razor Views Migration (Sprint 17-18) - Critical gap!
3. **Next:** Static Files/wwwroot (Sprint 19) - Critical gap!
4. **Then:** DI Container Migration (Sprint 13-16) - High value
5. **Then:** HTTP Modules (Sprint 20) - High value
6. **Later:** Caching, Background Services, JavaScript

---

*Last updated: 2026-02-03*
