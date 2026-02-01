# [TASK-014] Implement Basic HTML Report

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P2 |
| **Estimate** | M |
| **Sprint** | 1 |
| **Agent** | Claude Code |
| **Started** | 2026-01-31 |
| **Completed** | 2026-01-31 |

## Dependencies

- **Depends on:** TASK-009, TASK-010
- **Blocks:** None (nice to have for Sprint 1)

---

## Description

Generate a human-readable HTML report from the analysis results that can be shared with stakeholders.

---

## Acceptance Criteria

- [x] Generates self-contained HTML file (no external dependencies)
- [x] Shows solution overview
- [x] Shows project list with types and complexity
- [x] Shows dependency graph (simple text/table for now)
- [x] Shows issues list with severity
- [x] Shows recommended migration phases
- [x] Looks professional (minimal CSS)
- [x] Can be opened in any browser

---

## Technical Notes

### HtmlReportGenerator:

```csharp
public class HtmlReportGenerator
{
    public string Generate(AnalysisReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>NetLift Analysis - {report.SolutionName}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetEmbeddedCss());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine(GenerateHeader(report));

        // Overview
        sb.AppendLine(GenerateOverview(report));

        // Projects table
        sb.AppendLine(GenerateProjectsTable(report));

        // Issues
        sb.AppendLine(GenerateIssuesSection(report));

        // Migration plan
        sb.AppendLine(GenerateMigrationPlan(report));

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
```

### Minimal CSS (embedded):

```css
:root {
    --primary: #6366f1;  /* Purple - user preference */
    --success: #22c55e;
    --warning: #eab308;
    --error: #ef4444;
    --bg: #1a1a2e;
    --card: #16213e;
    --text: #e2e8f0;
    --muted: #94a3b8;
}

* { margin: 0; padding: 0; box-sizing: border-box; }

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
    background: var(--bg);
    color: var(--text);
    line-height: 1.6;
    padding: 2rem;
}

.container { max-width: 1200px; margin: 0 auto; }

.card {
    background: var(--card);
    border-radius: 8px;
    padding: 1.5rem;
    margin-bottom: 1.5rem;
}

h1, h2, h3 { color: var(--primary); margin-bottom: 1rem; }

table { width: 100%; border-collapse: collapse; }
th, td { padding: 0.75rem; text-align: left; border-bottom: 1px solid #333; }
th { color: var(--muted); font-weight: 500; }

.badge {
    display: inline-block;
    padding: 0.25rem 0.75rem;
    border-radius: 9999px;
    font-size: 0.875rem;
}
.badge-success { background: var(--success); color: #000; }
.badge-warning { background: var(--warning); color: #000; }
.badge-error { background: var(--error); color: #fff; }

.progress-bar {
    height: 8px;
    background: #333;
    border-radius: 4px;
    overflow: hidden;
}
.progress-fill {
    height: 100%;
    background: var(--primary);
}
```

### Report sections:

**Header:**
```html
<header class="card">
  <h1>🚀 NetLift Analysis Report</h1>
  <p class="muted">Generated: 2025-01-31 10:00:00</p>
</header>
```

**Overview:**
```html
<section class="card">
  <h2>Solution Overview</h2>
  <div class="stats">
    <div class="stat">
      <span class="value">5</span>
      <span class="label">Projects</span>
    </div>
    <div class="stat">
      <span class="value">Medium</span>
      <span class="label">Complexity</span>
    </div>
    <div class="stat">
      <span class="value">65%</span>
      <span class="label">Auto-migratable</span>
    </div>
  </div>
</section>
```

**Projects table:**
```html
<section class="card">
  <h2>Projects</h2>
  <table>
    <thead>
      <tr>
        <th>Project</th>
        <th>Type</th>
        <th>Framework</th>
        <th>Complexity</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td>MyWeb</td>
        <td><span class="badge badge-primary">MVC</span></td>
        <td>.NET 4.8</td>
        <td><span class="badge badge-warning">Medium</span></td>
      </tr>
    </tbody>
  </table>
</section>
```

### Output:

Save to `netlift-report/analysis.html` alongside JSON report.

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
| 2026-01-31 | Claude Code | Implemented IHtmlReportGenerator interface |
| 2026-01-31 | Claude Code | Implemented HtmlReportGenerator with embedded CSS |
| 2026-01-31 | Claude Code | Added --html option to AnalyzeCommand |
| 2026-01-31 | Claude Code | Created comprehensive unit tests (28 tests) |
| 2026-01-31 | Claude Code | All tests passing (195 total) - Task completed |
