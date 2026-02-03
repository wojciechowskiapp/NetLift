# Background Services Migration - Implementation Plan

> **Feature:** Windows Services and timers → IHostedService/BackgroundService

---

## Scope

| Source Pattern | Target Pattern | Confidence |
|---------------|----------------|-----------|
| System.Threading.Timer | BackgroundService | 85% |
| Task.Run loops | BackgroundService | 80% |
| Windows Service | Worker Service | 70% |
| Hangfire | Keep (compatible) | 90% |
| Quartz.NET | Keep (compatible) | 90% |

---

## Key Transformations

### Timer → BackgroundService

**Before:**
```csharp
public class MyTask
{
    private Timer _timer;

    public void Start()
    {
        _timer = new Timer(DoWork, null,
            TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    private void DoWork(object state)
    {
        // Background work
    }
}
```

**After:**
```csharp
public class MyTaskService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task DoWorkAsync()
    {
        // Background work
    }
}
```

---

## Sprint Tasks (Sprint 22)

| # | Task | Size | Description |
|---|------|------|-------------|
| 213 | BackgroundTaskInfo model | S | Timer/task detection |
| 214 | IBackgroundTaskAnalyzer interface | S | Analysis contract |
| 215 | IBackgroundServiceGenerator interface | S | Generation contract |
| 216 | TimerPatternDetector | M | Find Timer patterns |
| 217 | BackgroundServiceGenerator | M | Generate BackgroundService |
| 218 | HostedServiceRegistration | S | DI registration |
| 219 | Unit tests (15+) | M | Transformation tests |

---

*Last updated: 2026-02-03*
