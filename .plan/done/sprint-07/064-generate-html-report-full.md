# [TASK-064] Generate HTML Report (Full)

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P0 |
| **Estimate** | L |
| **Sprint** | 7 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-061, TASK-062, TASK-063
- **Blocks:** TASK-066

---

## Description

Generate a comprehensive HTML report that includes analysis results, migration details, build/test results, confidence score, and actionable recommendations. The report should be standalone, visually appealing, and shareable.

---

## Acceptance Criteria

- [ ] Generates standalone HTML file (no external dependencies)
- [ ] Includes all migration data (analysis, transforms, validation)
- [ ] Shows confidence score with visual breakdown
- [ ] Lists build errors, warnings, and test failures
- [ ] Provides actionable next steps
- [ ] Responsive design for mobile/desktop viewing
- [ ] Dark theme with purple accents
- [ ] Exportable to PDF via browser print
- [ ] Unit tests for HTML generation
- [ ] Integration test validates HTML structure

---

## Technical Notes

### Interface:

```csharp
namespace NetLift.Reporting;

public interface IHtmlReportGenerator
{
    Task<string> GenerateAsync(
        MigrationReportData data,
        string outputPath);
}

public record MigrationReportData
{
    public AnalysisReport Analysis { get; init; } = null!;
    public MigrationReport Migration { get; init; } = null!;
    public BuildResult? BuildResult { get; init; }
    public TestResult? TestResult { get; init; }
    public ConfidenceScore ConfidenceScore { get; init; } = null!;
    public DateTime GeneratedAt { get; init; }
    public string NetLiftVersion { get; init; } = "";
}
```

### Implementation:

```csharp
public class HtmlReportGenerator : IHtmlReportGenerator
{
    public async Task<string> GenerateAsync(
        MigrationReportData data,
        string outputPath)
    {
        var html = GenerateHtmlContent(data);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, html);

        return outputPath;
    }

    private static string GenerateHtmlContent(MigrationReportData data)
    {
        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NetLift Migration Report - {{data.Analysis.SolutionName}}</title>
    <style>
        :root {
            --primary: #9333ea;
            --primary-light: #a855f7;
            --bg-dark: #0f0f0f;
            --bg-card: #1a1a1a;
            --text: #e5e5e5;
            --text-muted: #a3a3a3;
            --success: #22c55e;
            --warning: #f59e0b;
            --error: #ef4444;
            --border: #333;
        }

        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: var(--bg-dark);
            color: var(--text);
            line-height: 1.6;
            padding: 2rem;
        }

        .container {
            max-width: 1200px;
            margin: 0 auto;
        }

        header {
            text-align: center;
            margin-bottom: 3rem;
            padding-bottom: 2rem;
            border-bottom: 2px solid var(--primary);
        }

        h1 {
            font-size: 2.5rem;
            margin-bottom: 0.5rem;
            background: linear-gradient(135deg, var(--primary), var(--primary-light));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
        }

        .subtitle {
            color: var(--text-muted);
            font-size: 1rem;
        }

        .card {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 8px;
            padding: 1.5rem;
            margin-bottom: 2rem;
        }

        .card h2 {
            font-size: 1.5rem;
            margin-bottom: 1rem;
            color: var(--primary-light);
        }

        .confidence-score {
            text-align: center;
            padding: 2rem;
        }

        .score-circle {
            width: 150px;
            height: 150px;
            border-radius: 50%;
            background: conic-gradient(
                var(--primary) {{data.ConfidenceScore.OverallScore * 3.6}}deg,
                var(--border) 0deg
            );
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 1rem;
            position: relative;
        }

        .score-circle::before {
            content: '{{data.ConfidenceScore.OverallScore}}';
            position: absolute;
            width: 120px;
            height: 120px;
            background: var(--bg-card);
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 3rem;
            font-weight: bold;
        }

        .score-level {
            font-size: 1.2rem;
            color: {{GetLevelColor(data.ConfidenceScore.Level)}};
            font-weight: 600;
        }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 1rem;
            margin: 1rem 0;
        }

        .stat {
            background: var(--bg-dark);
            padding: 1rem;
            border-radius: 6px;
            border-left: 3px solid var(--primary);
        }

        .stat-label {
            color: var(--text-muted);
            font-size: 0.875rem;
            text-transform: uppercase;
        }

        .stat-value {
            font-size: 1.5rem;
            font-weight: 600;
            margin-top: 0.25rem;
        }

        .component-table {
            width: 100%;
            border-collapse: collapse;
            margin: 1rem 0;
        }

        .component-table th,
        .component-table td {
            padding: 0.75rem;
            text-align: left;
            border-bottom: 1px solid var(--border);
        }

        .component-table th {
            background: var(--bg-dark);
            color: var(--primary-light);
            font-weight: 600;
        }

        .progress-bar {
            background: var(--border);
            border-radius: 4px;
            height: 8px;
            overflow: hidden;
        }

        .progress-fill {
            height: 100%;
            background: var(--primary);
            transition: width 0.3s ease;
        }

        .issue-list {
            list-style: none;
        }

        .issue-item {
            padding: 0.75rem;
            margin: 0.5rem 0;
            background: var(--bg-dark);
            border-radius: 4px;
            border-left: 3px solid var(--error);
        }

        .issue-item.warning {
            border-left-color: var(--warning);
        }

        .recommendations {
            background: linear-gradient(135deg, rgba(147, 51, 234, 0.1), rgba(168, 85, 247, 0.1));
            border: 1px solid var(--primary);
            border-radius: 8px;
            padding: 1.5rem;
        }

        .recommendations ul {
            margin-left: 1.5rem;
            margin-top: 1rem;
        }

        .recommendations li {
            margin: 0.5rem 0;
        }

        .badge {
            display: inline-block;
            padding: 0.25rem 0.75rem;
            border-radius: 12px;
            font-size: 0.875rem;
            font-weight: 600;
        }

        .badge.success { background: var(--success); color: #000; }
        .badge.warning { background: var(--warning); color: #000; }
        .badge.error { background: var(--error); color: #fff; }

        footer {
            text-align: center;
            margin-top: 3rem;
            padding-top: 2rem;
            border-top: 1px solid var(--border);
            color: var(--text-muted);
            font-size: 0.875rem;
        }

        @media (max-width: 768px) {
            body { padding: 1rem; }
            h1 { font-size: 2rem; }
            .stats-grid { grid-template-columns: 1fr; }
        }

        @media print {
            body { background: white; color: black; }
            .card { border-color: #ccc; }
        }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <h1>NetLift Migration Report</h1>
            <div class="subtitle">{{data.Analysis.SolutionName}} • Generated {{data.GeneratedAt:yyyy-MM-dd HH:mm}} • NetLift v{{data.NetLiftVersion}}</div>
        </header>

        <div class="card confidence-score">
            <h2>Migration Confidence Score</h2>
            <div class="score-circle"></div>
            <div class="score-level">{{data.ConfidenceScore.Level}} Confidence</div>
        </div>

        <div class="card">
            <h2>Score Breakdown</h2>
            <table class="component-table">
                <thead>
                    <tr>
                        <th>Component</th>
                        <th>Score</th>
                        <th>Weight</th>
                        <th>Contribution</th>
                        <th>Rationale</th>
                    </tr>
                </thead>
                <tbody>
                    {{GenerateScoreComponentRows(data.ConfidenceScore)}}
                </tbody>
            </table>
        </div>

        <div class="card">
            <h2>Migration Summary</h2>
            <div class="stats-grid">
                <div class="stat">
                    <div class="stat-label">Projects</div>
                    <div class="stat-value">{{data.Analysis.ProjectCount}}</div>
                </div>
                <div class="stat">
                    <div class="stat-label">Target Framework</div>
                    <div class="stat-value">{{data.Analysis.TargetFramework}}</div>
                </div>
                <div class="stat">
                    <div class="stat-label">Files Transformed</div>
                    <div class="stat-value">{{data.Migration.TransformedFiles.Count}}</div>
                </div>
                <div class="stat">
                    <div class="stat-label">Complexity</div>
                    <div class="stat-value">{{data.Analysis.MigrationComplexity}}</div>
                </div>
            </div>
        </div>

        {{GenerateBuildResultSection(data.BuildResult)}}
        {{GenerateTestResultSection(data.TestResult)}}
        {{GenerateIssuesSection(data.Migration.Issues)}}

        <div class="card recommendations">
            <h2>Recommendations</h2>
            <ul>
                {{GenerateRecommendations(data.ConfidenceScore.Recommendations)}}
            </ul>
        </div>

        <footer>
            <p>Generated by NetLift - .NET Framework to .NET 8 Migration Tool</p>
            <p>For support, visit https://github.com/yourusername/netlift</p>
        </footer>
    </div>
</body>
</html>
""";
    }

    private static string GenerateScoreComponentRows(ConfidenceScore score)
    {
        var sb = new StringBuilder();
        foreach (var (_, component) in score.Components)
        {
            sb.AppendLine($"""
                    <tr>
                        <td>{component.Name}</td>
                        <td>
                            <div class="progress-bar">
                                <div class="progress-fill" style="width: {component.Score}%"></div>
                            </div>
                            {component.Score}/100
                        </td>
                        <td>{component.Weight}%</td>
                        <td>{component.WeightedScore}</td>
                        <td>{component.Rationale}</td>
                    </tr>
                """);
        }
        return sb.ToString();
    }

    private static string GenerateBuildResultSection(BuildResult? buildResult)
    {
        if (buildResult == null) return "";

        var statusBadge = buildResult.Success
            ? "<span class='badge success'>Success</span>"
            : "<span class='badge error'>Failed</span>";

        var errors = buildResult.Errors.Take(10)
            .Select(e => $"<li class='issue-item'><strong>{e.Code}:</strong> {e.Message}<br/><small>{e.File}({e.Line},{e.Column})</small></li>")
            .ToList();

        return $"""
        <div class="card">
            <h2>Build Validation {statusBadge}</h2>
            <div class="stats-grid">
                <div class="stat">
                    <div class="stat-label">Duration</div>
                    <div class="stat-value">{buildResult.Duration.TotalSeconds:F1}s</div>
                </div>
                <div class="stat">
                    <div class="stat-label">Errors</div>
                    <div class="stat-value">{buildResult.Errors.Count}</div>
                </div>
                <div class="stat">
                    <div class="stat-label">Warnings</div>
                    <div class="stat-value">{buildResult.Warnings.Count}</div>
                </div>
            </div>
            {(errors.Any() ? $"<h3>Build Errors</h3><ul class='issue-list'>{string.Join("", errors)}</ul>" : "")}
        </div>
        """;
    }

    private static string GenerateTestResultSection(TestResult? testResult)
    {
        if (testResult == null || testResult.TotalTests == 0) return "";

        var statusBadge = testResult.Success
            ? "<span class='badge success'>Passed</span>"
            : "<span class='badge error'>Failed</span>";

        return $"""
        <div class="card">
            <h2>Test Results {statusBadge}</h2>
            <div class="stats-grid">
                <div class="stat">
                    <div class="stat-label">Total Tests</div>
                    <div class="stat-value">{testResult.TotalTests}</div>
                </div>
                <div class="stat">
                    <div class="stat-label">Passed</div>
                    <div class="stat-value">{testResult.PassedTests}</div>
                </div>
                <div class="stat">
                    <div class="stat-label">Failed</div>
                    <div class="stat-value">{testResult.FailedTests}</div>
                </div>
                <div class="stat">
                    <div class="stat-label">Skipped</div>
                    <div class="stat-value">{testResult.SkippedTests}</div>
                </div>
            </div>
        </div>
        """;
    }

    private static string GenerateIssuesSection(IReadOnlyList<MigrationIssue> issues)
    {
        if (!issues.Any()) return "";

        var issueItems = issues.Take(20)
            .Select(i => $"<li class='issue-item {(i.Severity == IssueSeverity.Warning ? "warning" : "")}'>" +
                         $"<strong>{i.Code}:</strong> {i.Message}<br/><small>{i.FilePath}</small></li>")
            .ToList();

        return $"""
        <div class="card">
            <h2>Migration Issues ({issues.Count})</h2>
            <ul class='issue-list'>
                {string.Join("", issueItems)}
            </ul>
        </div>
        """;
    }

    private static string GenerateRecommendations(IReadOnlyList<string> recommendations)
    {
        return string.Join("", recommendations.Select(r => $"<li>{r}</li>"));
    }

    private static string GetLevelColor(ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.High => "var(--success)",
        ConfidenceLevel.Medium => "var(--warning)",
        ConfidenceLevel.Low => "var(--error)",
        _ => "var(--text)"
    };
}
```

### Unit tests:

```csharp
public class HtmlReportGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_CreatesValidHtml()
    {
        var data = CreateTestReportData();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-report.html");

        var generator = new HtmlReportGenerator();
        await generator.GenerateAsync(data, outputPath);

        Assert.True(File.Exists(outputPath));
        var html = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains(data.Analysis.SolutionName, html);

        File.Delete(outputPath);
    }

    [Fact]
    public async Task GenerateAsync_IncludesAllSections()
    {
        var data = CreateTestReportData();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-report2.html");

        var generator = new HtmlReportGenerator();
        await generator.GenerateAsync(data, outputPath);

        var html = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Confidence Score", html);
        Assert.Contains("Score Breakdown", html);
        Assert.Contains("Migration Summary", html);
        Assert.Contains("Recommendations", html);

        File.Delete(outputPath);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
