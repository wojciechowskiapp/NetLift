# Caching Modernization - Implementation Plan

> **Feature:** System.Web.Caching → IMemoryCache/IDistributedCache

---

## Scope

| Source Pattern | Target Pattern | Confidence |
|---------------|----------------|-----------|
| HttpRuntime.Cache | IMemoryCache | 85% |
| HttpContext.Cache | IMemoryCache | 85% |
| System.Web.Caching.Cache | IMemoryCache | 85% |
| OutputCacheAttribute | ResponseCaching | 75% |
| CacheDependency | ICacheEntry | 70% |

---

## Key Transformations

### HttpRuntime.Cache → IMemoryCache

**Before:**
```csharp
HttpRuntime.Cache.Insert("key", value, null,
    DateTime.Now.AddMinutes(30), Cache.NoSlidingExpiration);

var cached = HttpRuntime.Cache["key"];
```

**After:**
```csharp
_memoryCache.Set("key", value, TimeSpan.FromMinutes(30));

_memoryCache.TryGetValue("key", out var cached);
```

### OutputCache → ResponseCaching

**Before:**
```csharp
[OutputCache(Duration = 60, VaryByParam = "id")]
public ActionResult Index(int id) { }
```

**After:**
```csharp
[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
public IActionResult Index(int id) { }
```

---

## Sprint Tasks (Sprint 21)

| # | Task | Size | Description |
|---|------|------|-------------|
| 205 | CacheUsageInfo model | S | Cache usage detection |
| 206 | ICacheAnalyzer interface | S | Analysis contract |
| 207 | ICacheTransformer interface | S | Transform contract |
| 208 | CacheAnalyzer | M | Detect cache patterns |
| 209 | MemoryCacheTransformer | M | Transform to IMemoryCache |
| 210 | OutputCacheTransformer | M | Transform to ResponseCache |
| 211 | DI registration generator | S | Add IMemoryCache to DI |
| 212 | Unit tests (20+) | M | Transformation tests |

---

*Last updated: 2026-02-03*
