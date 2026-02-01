# NetLift - Task Dependency Graph

> Wizualizacja zależności między zadaniami

---

## Sprint 1: Foundation

```
TASK-001 (Solution Structure) ─────┬──────┬──────┬──────┬──────┐
         P0, BLOCKER              │      │      │      │      │
                                  ▼      ▼      ▼      ▼      ▼
                           TASK-002  TASK-003  TASK-011 TASK-012 TASK-013
                           (Roslyn)  (CLI)    (Fixture) (Tests) (Git)
                              │         │
                              │         │
          ┌───────────────────┼─────────┘
          │                   │
          ▼                   │
     TASK-004 ◄───────────────┤
     (Solution Parser)        │
          │                   │
          │    TASK-005 ◄─────┘
          │    (Project Parser)
          │         │
          │         │    TASK-006
          │         │    (packages.config)
          │         │         │
          ▼         ▼         ▼
     ┌────┴─────────┴─────────┴────┐
     │        TASK-007             │
     │   (Dependency Graph)        │
     └─────────────┬───────────────┘
                   │
                   ▼
              TASK-008
         (Project Type Detector)
                   │
                   ▼
              TASK-009
         (Analysis Report Model)
                   │
                   ▼
              TASK-010
         (Analyze Command)
                   │
                   ▼
              TASK-014
         (HTML Report)
```

### Parallel Groups (Sprint 1):

**Group A (can start immediately):**
- TASK-001 ← BLOCKER, do this first!

**Group B (after TASK-001):**
- TASK-002, TASK-003, TASK-011, TASK-012, TASK-013 ← parallel

**Group C (after TASK-002):**
- TASK-004, TASK-005, TASK-006 ← parallel

**Group D (after Group C):**
- TASK-007 → TASK-008 → TASK-009 → TASK-010 → TASK-014 ← sequential

---

## Sprint 2: Project Files

```
TASK-010 (Analyze Command) ────────────────────────────────────┐
                                                               │
TASK-005 (Project Parser) ─────┬───────────────────────────────┤
                               │                               │
                               ▼                               ▼
                         TASK-015                        TASK-021
                    (csproj Converter)              (Migrate Command)
                               │                               ▲
          ┌────────────────────┼────────────────────┐          │
          ▼                    ▼                    ▼          │
     TASK-016             TASK-017             TASK-018        │
  (AssemblyInfo)       (PackageRef)        (NuGet Rules)       │
          │                    │                    │          │
          └────────────────────┴────────────────────┴──────────┘

TASK-013 (Git) ────────────────┬────────────────────┐
                               ▼                    ▼
                         TASK-022              TASK-023
                    (Branch Strategy)     (Commit Generator)
```

---

## Sprint 3: Configuration

```
TASK-005 (Project Parser) ─────────────────────────────────────┐
                                                               │
                    ┌──────────────────────────────────────────┤
                    ▼                    ▼                     ▼
              TASK-025             TASK-026              TASK-027
         (ConnectionStrings)    (AppSettings)         (system.web)
                    │                    │                     │
                    └──────────┬─────────┴─────────────────────┘
                               ▼
                         TASK-028
                    (appsettings.json)
                               │
                    ┌──────────┴──────────┐
                    ▼                     ▼
              TASK-029              TASK-030
         (appsettings.env)      (Program.cs)
                                      │
                    ┌─────────────────┼─────────────────┐
                    ▼                 ▼                 ▼
              TASK-031          TASK-032          (Phase 3 Done)
         (Auth Config)      (Session Config)
```

---

## Sprint 4: MVC Controllers

```
TASK-002 (Roslyn) ─────────────────────────────────────────────┐
                                                               │
                    ┌──────────┬──────────┬──────────┐         │
                    ▼          ▼          ▼          ▼         │
              TASK-033   TASK-034   TASK-035   TASK-036        │
           (Namespace) (BaseClass) (ActionResult) (HttpContext) │
                    │          │          │          │         │
                    └──────────┴──────────┴──────────┴─────────┘
                                      │
                                      ▼
                               TASK-037
                          (Parse RouteConfig)
                                      │
                         ┌────────────┴────────────┐
                         ▼                         ▼
                   TASK-038                  TASK-039
              (Attribute Routing)           (Filters)
                         │                         │
                         └────────────┬────────────┘
                                      ▼
                               TASK-040
                          (View Imports)
                                      │
                                      ▼
                               TASK-041
                             (Areas)
```

---

## Sprint 5: Entity Framework

```
TASK-002 (Roslyn) ─────────────────────────────────────────────┐
                                                               │
                                      ▼                        │
                               TASK-043                        │
                          (Detect DbContext)                   │
                                      │                        │
                         ┌────────────┴────────────┐           │
                         ▼                         ▼           │
                   TASK-044                  TASK-045          │
              (Constructor DI)          (HasRequired)          │
                         │                         │           │
                         │            ┌────────────┘           │
                         │            ▼                        │
                         │      TASK-046                       │
                         │     (HasMany)                       │
                         │            │                        │
                    ┌────┴────────────┴────────────┐           │
                    ▼            ▼                 ▼           │
              TASK-047     TASK-048          TASK-049          │
            (Include)    (SqlQuery)       (Lazy Loading)       │
```

---

## Sprint 6: WCF Migration

```
TASK-002 (Roslyn) ─────────────────────────────────────────────┐
                                                               │
                    ┌──────────────────┬───────────────────────┤
                    ▼                  ▼                       │
              TASK-051           TASK-052                      │
         (ServiceContract)   (DataContract)                    │
                    │                  │                       │
                    │     TASK-053 ◄───┘                       │
                    │    (WCF Config)                          │
                    │         │                                │
                    └─────────┼────────────────────┐           │
                              ▼                    ▼           │
                        TASK-054             TASK-056          │
                      (Proto Gen)         (REST API Gen)       │
                              │                    │           │
                              ▼                    │           │
                        TASK-055                   │           │
                     (gRPC Service)                │           │
                              │                    │           │
                    ┌─────────┴────────────────────┘           │
                    ▼                                          │
              TASK-057                                         │
         (Extract Logic)                                       │
                    │                                          │
          ┌─────────┴─────────┐                                │
          ▼                   ▼                                │
    TASK-058            TASK-059                               │
  (FaultContract)    (Duplex Warning)                          │
```

---

## Sprint 7: Validation

```
All Previous Sprints ──────────────────────────────────────────┐
                                                               │
                    ┌──────────────────────────────────────────┤
                    ▼                  ▼                       │
              TASK-061           TASK-062                      │
         (Build Validator)    (Test Runner)                    │
                    │                  │                       │
                    └────────┬─────────┘                       │
                             ▼                                 │
                       TASK-063                                │
                  (Confidence Score)                           │
                             │                                 │
                    ┌────────┴────────┐                        │
                    ▼                 ▼                        │
              TASK-064          TASK-065                       │
           (HTML Report)    (Error Handling)                   │
                    │                 │                        │
                    └────────┬────────┘                        │
                             ▼                                 │
                       TASK-066                                │
                    (E2E Testing)                              │
                             │                                 │
                             ▼                                 │
                        MVP DONE! 🎉                           │
```

---

## Legend

```
──────►  Dependency (must complete before)
   │     Grouping
   ▼     Direction of work
  P0     Priority 0 (Blocker)
  P1     Priority 1 (Core)
  P2     Priority 2 (Nice to have)
```

---

## Recommended Execution Order

### Week 1-2:
1. TASK-001 (blocker)
2. TASK-002 + TASK-003 + TASK-011 + TASK-012 (parallel)
3. TASK-004 + TASK-005 + TASK-006 (parallel)

### Week 3-4:
4. TASK-007 → TASK-008 → TASK-009 → TASK-010
5. TASK-015 + TASK-016 + TASK-017 (parallel)

### Week 5-6:
6. TASK-025 + TASK-026 + TASK-027 (parallel)
7. TASK-028 → TASK-030

### Week 7-8:
8. TASK-033 + TASK-034 + TASK-035 + TASK-036 (parallel)
9. TASK-037 → TASK-038 + TASK-039 → TASK-040 → TASK-041

### Week 9-10:
10. TASK-043 → TASK-044 + TASK-045 → TASK-046 → TASK-047 + TASK-048 + TASK-049

### Week 11-12:
11. TASK-051 + TASK-052 + TASK-053 (parallel)
12. TASK-054 + TASK-056 → TASK-055 → TASK-057 → TASK-058 + TASK-059

### Week 13-14:
13. TASK-061 + TASK-062 → TASK-063 → TASK-064 + TASK-065 → TASK-066

---

*Last updated: 2025-01-31*
