namespace NetLift.Validation;

using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using System.Text;

/// <summary>
/// Generates comprehensive self-contained HTML reports for migration results.
/// </summary>
public class FullHtmlReportGenerator : IFullHtmlReportGenerator
{
    /// <summary>
    /// Generates a complete self-contained HTML migration report.
    /// </summary>
    /// <param name="reportData">The migration report data to convert to HTML.</param>
    /// <returns>A complete HTML document as a string.</returns>
    public string Generate(MigrationReportData reportData)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>NetLift Migration Report - {EscapeHtml(reportData.SolutionName)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetEmbeddedCss());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        sb.AppendLine(GenerateHeader(reportData));
        sb.AppendLine(GenerateConfidenceSection(reportData));
        sb.AppendLine(GenerateScoreBreakdown(reportData));
        sb.AppendLine(GenerateBuildResults(reportData));
        sb.AppendLine(GenerateTestResults(reportData));
        sb.AppendLine(GenerateIssuesSection(reportData));
        sb.AppendLine(GenerateRecommendations(reportData));

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GetEmbeddedCss()
    {
        return @"
:root {
    --primary: #9333ea;
    --primary-dark: #7c3aed;
    --bg-dark: #0f0f0f;
    --bg-card: #1a1a1a;
    --bg-secondary: #262626;
    --success: #22c55e;
    --warning: #f59e0b;
    --error: #ef4444;
    --info: #3b82f6;
    --text: #e5e5e5;
    --text-muted: #a3a3a3;
    --border: #404040;
}

* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', sans-serif;
    background: var(--bg-dark);
    color: var(--text);
    line-height: 1.6;
    padding: 1rem;
}

@media (min-width: 768px) {
    body {
        padding: 2rem;
    }
}

.container {
    max-width: 1400px;
    margin: 0 auto;
}

.header {
    background: linear-gradient(135deg, var(--primary) 0%, var(--primary-dark) 100%);
    border-radius: 16px;
    padding: 2rem;
    margin-bottom: 2rem;
    text-align: center;
    box-shadow: 0 4px 20px rgba(147, 51, 234, 0.3);
}

.header h1 {
    font-size: clamp(1.5rem, 4vw, 2.5rem);
    font-weight: 700;
    margin-bottom: 0.5rem;
    color: white;
}

.header .subtitle {
    color: rgba(255, 255, 255, 0.9);
    font-size: clamp(1rem, 2vw, 1.25rem);
    margin-bottom: 0.75rem;
}

.header .metadata {
    color: rgba(255, 255, 255, 0.7);
    font-size: 0.875rem;
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: 1rem;
    margin-top: 1rem;
}

.card {
    background: var(--bg-card);
    border-radius: 12px;
    padding: 1.5rem;
    margin-bottom: 1.5rem;
    border: 1px solid var(--border);
}

@media (min-width: 768px) {
    .card {
        padding: 2rem;
    }
}

.card h2 {
    color: var(--primary);
    font-size: clamp(1.25rem, 3vw, 1.75rem);
    margin-bottom: 1.5rem;
    font-weight: 600;
    border-bottom: 2px solid var(--primary);
    padding-bottom: 0.5rem;
}

.confidence-container {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 2rem;
    padding: 2rem 0;
}

@media (min-width: 768px) {
    .confidence-container {
        flex-direction: row;
        justify-content: center;
    }
}

.confidence-circle {
    position: relative;
    width: 200px;
    height: 200px;
}

.confidence-circle svg {
    transform: rotate(-90deg);
}

.confidence-value {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    text-align: center;
}

.confidence-score {
    font-size: 3rem;
    font-weight: 700;
    line-height: 1;
}

.confidence-label {
    font-size: 0.875rem;
    color: var(--text-muted);
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.confidence-level {
    font-size: 1.5rem;
    font-weight: 600;
    margin-top: 0.5rem;
}

.confidence-level.high { color: var(--success); }
.confidence-level.medium { color: var(--warning); }
.confidence-level.low { color: var(--error); }

.score-table {
    width: 100%;
    border-collapse: collapse;
    margin-top: 1rem;
}

.score-table thead {
    background: var(--bg-secondary);
}

.score-table th {
    padding: 0.75rem;
    text-align: left;
    color: var(--text-muted);
    font-weight: 600;
    font-size: 0.875rem;
    border-bottom: 2px solid var(--border);
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.score-table td {
    padding: 1rem 0.75rem;
    border-bottom: 1px solid var(--border);
}

.score-table tbody tr:hover {
    background: var(--bg-secondary);
}

.score-bar-container {
    width: 100%;
    height: 8px;
    background: var(--bg-secondary);
    border-radius: 4px;
    overflow: hidden;
    margin-top: 0.25rem;
}

.score-bar {
    height: 100%;
    background: var(--primary);
    border-radius: 4px;
    transition: width 0.3s ease;
}

.status-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
    gap: 1rem;
    margin-top: 1rem;
}

.status-item {
    background: var(--bg-secondary);
    padding: 1.25rem;
    border-radius: 8px;
    border-left: 4px solid var(--border);
}

.status-item.success { border-left-color: var(--success); }
.status-item.error { border-left-color: var(--error); }
.status-item.warning { border-left-color: var(--warning); }
.status-item.info { border-left-color: var(--info); }

.status-label {
    font-size: 0.875rem;
    color: var(--text-muted);
    margin-bottom: 0.5rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.status-value {
    font-size: 1.5rem;
    font-weight: 700;
}

.badge {
    display: inline-block;
    padding: 0.375rem 0.875rem;
    border-radius: 9999px;
    font-size: 0.75rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.badge-success {
    background: rgba(34, 197, 94, 0.2);
    color: #22c55e;
}

.badge-error {
    background: rgba(239, 68, 68, 0.2);
    color: #ef4444;
}

.badge-warning {
    background: rgba(245, 158, 11, 0.2);
    color: #f59e0b;
}

.badge-info {
    background: rgba(59, 130, 246, 0.2);
    color: #3b82f6;
}

.issue-list {
    margin-top: 1rem;
}

.issue-item {
    background: var(--bg-secondary);
    padding: 1.25rem;
    border-radius: 8px;
    border-left: 4px solid var(--border);
    margin-bottom: 1rem;
}

.issue-item.severity-error { border-left-color: var(--error); }
.issue-item.severity-warning { border-left-color: var(--warning); }
.issue-item.severity-info { border-left-color: var(--info); }

.issue-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-bottom: 0.75rem;
}

.issue-code {
    font-family: 'Courier New', monospace;
    font-weight: 700;
    color: var(--primary);
}

.issue-message {
    color: var(--text);
    margin-bottom: 0.5rem;
}

.issue-file {
    font-size: 0.875rem;
    color: var(--text-muted);
    font-family: 'Courier New', monospace;
}

.recommendations-list {
    list-style: none;
    padding: 0;
    margin-top: 1rem;
}

.recommendations-list li {
    background: var(--bg-secondary);
    padding: 1rem 1rem 1rem 3rem;
    border-radius: 8px;
    margin-bottom: 0.75rem;
    position: relative;
    border-left: 4px solid var(--primary);
}

.recommendations-list li::before {
    content: '✓';
    position: absolute;
    left: 1rem;
    top: 50%;
    transform: translateY(-50%);
    font-size: 1.25rem;
    color: var(--primary);
    font-weight: 700;
}

.empty-state {
    text-align: center;
    padding: 3rem 1rem;
    color: var(--text-muted);
    font-style: italic;
}

.diagnostic-item {
    background: var(--bg-dark);
    padding: 0.75rem;
    border-radius: 6px;
    margin-bottom: 0.5rem;
    font-family: 'Courier New', monospace;
    font-size: 0.875rem;
    border-left: 3px solid;
}

.diagnostic-item.error { border-left-color: var(--error); }
.diagnostic-item.warning { border-left-color: var(--warning); }

.diagnostic-code {
    color: var(--primary);
    font-weight: 700;
}

.diagnostic-message {
    color: var(--text);
}

.diagnostic-location {
    color: var(--text-muted);
    font-size: 0.75rem;
    margin-top: 0.25rem;
}

.test-failure-item {
    background: var(--bg-dark);
    padding: 1rem;
    border-radius: 6px;
    margin-bottom: 0.75rem;
    border-left: 3px solid var(--error);
}

.test-name {
    font-family: 'Courier New', monospace;
    font-weight: 700;
    color: var(--primary);
    margin-bottom: 0.5rem;
}

.test-error {
    color: var(--error);
    margin-bottom: 0.5rem;
}

.test-stack {
    font-family: 'Courier New', monospace;
    font-size: 0.75rem;
    color: var(--text-muted);
    white-space: pre-wrap;
    max-height: 200px;
    overflow-y: auto;
    background: var(--bg-dark);
    padding: 0.5rem;
    border-radius: 4px;
}

@media print {
    body {
        background: white;
        color: black;
    }

    .card {
        page-break-inside: avoid;
        border: 1px solid #ccc;
    }

    .header {
        background: linear-gradient(135deg, #9333ea 0%, #7c3aed 100%);
        -webkit-print-color-adjust: exact;
        print-color-adjust: exact;
    }
}
";
    }

    private string GenerateHeader(MigrationReportData reportData)
    {
        return $@"
    <div class=""header"">
      <h1>NetLift Migration Report</h1>
      <div class=""subtitle"">{EscapeHtml(reportData.SolutionName)}</div>
      <div class=""metadata"">
        <span>Generated: {reportData.GeneratedAt:yyyy-MM-dd HH:mm:ss}</span>
        <span>•</span>
        <span>NetLift v{EscapeHtml(reportData.NetLiftVersion)}</span>
        <span>•</span>
        <span>Target: {EscapeHtml(reportData.TargetFramework)}</span>
        <span>•</span>
        <span>{reportData.ProjectCount} Project{(reportData.ProjectCount != 1 ? "s" : "")}</span>
        <span>•</span>
        <span>{reportData.FilesTransformed} File{(reportData.FilesTransformed != 1 ? "s" : "")} Transformed</span>
      </div>
    </div>";
    }

    private string GenerateConfidenceSection(MigrationReportData reportData)
    {
        if (reportData.ConfidenceScore == null)
        {
            return @"
    <div class=""card"">
      <h2>Confidence Score</h2>
      <div class=""empty-state"">Confidence score not calculated</div>
    </div>";
        }

        var score = reportData.ConfidenceScore.OverallScore;
        var level = reportData.ConfidenceScore.Level.ToString().ToLowerInvariant();
        var circumference = 2 * Math.PI * 70; // radius = 70
        var offset = circumference - (score / 100.0 * circumference);

        var scoreColor = reportData.ConfidenceScore.Level switch
        {
            ConfidenceLevel.High => "#22c55e",
            ConfidenceLevel.Medium => "#f59e0b",
            ConfidenceLevel.Low => "#ef4444",
            _ => "#9333ea"
        };

        return $@"
    <div class=""card"">
      <h2>Confidence Score</h2>
      <div class=""confidence-container"">
        <div class=""confidence-circle"">
          <svg width=""200"" height=""200"">
            <circle cx=""100"" cy=""100"" r=""70"" fill=""none"" stroke=""#262626"" stroke-width=""12""/>
            <circle cx=""100"" cy=""100"" r=""70"" fill=""none"" stroke=""{scoreColor}"" stroke-width=""12""
                    stroke-dasharray=""{circumference}"" stroke-dashoffset=""{offset}"" stroke-linecap=""round""/>
          </svg>
          <div class=""confidence-value"">
            <div class=""confidence-score"" style=""color: {scoreColor};"">{score}</div>
            <div class=""confidence-label"">out of 100</div>
          </div>
        </div>
        <div>
          <div class=""confidence-level {level}"">{reportData.ConfidenceScore.Level} Confidence</div>
          <p style=""color: var(--text-muted); margin-top: 1rem; max-width: 400px;"">
            {GetConfidenceLevelDescription(reportData.ConfidenceScore.Level)}
          </p>
        </div>
      </div>
    </div>";
    }

    private string GenerateScoreBreakdown(MigrationReportData reportData)
    {
        if (reportData.ConfidenceScore?.Components == null || !reportData.ConfidenceScore.Components.Any())
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.AppendLine(@"
    <div class=""card"">
      <h2>Score Breakdown</h2>
      <table class=""score-table"">
        <thead>
          <tr>
            <th>Component</th>
            <th>Score</th>
            <th>Weight</th>
            <th>Weighted Score</th>
            <th>Rationale</th>
          </tr>
        </thead>
        <tbody>");

        foreach (var (name, component) in reportData.ConfidenceScore.Components.OrderByDescending(c => c.Value.Weight))
        {
            sb.AppendLine($@"
          <tr>
            <td><strong>{EscapeHtml(component.Name)}</strong></td>
            <td>
              {component.Score}/100
              <div class=""score-bar-container"">
                <div class=""score-bar"" style=""width: {component.Score}%""></div>
              </div>
            </td>
            <td>{component.Weight}%</td>
            <td><strong>{component.WeightedScore}</strong></td>
            <td style=""color: var(--text-muted);"">{EscapeHtml(component.Rationale)}</td>
          </tr>");
        }

        sb.AppendLine(@"
        </tbody>
      </table>
    </div>");

        return sb.ToString();
    }

    private string GenerateBuildResults(MigrationReportData reportData)
    {
        if (reportData.BuildResult == null)
        {
            return @"
    <div class=""card"">
      <h2>Build Results</h2>
      <div class=""empty-state"">Build validation not performed</div>
    </div>";
        }

        var build = reportData.BuildResult;
        var statusClass = build.Success ? "success" : "error";
        var statusBadge = build.Success ? "badge-success" : "badge-error";

        var sb = new StringBuilder();
        sb.AppendLine(@"
    <div class=""card"">
      <h2>Build Results</h2>
      <div class=""status-grid"">");

        sb.AppendLine($@"
        <div class=""status-item {statusClass}"">
          <div class=""status-label"">Status</div>
          <div class=""status-value""><span class=""badge {statusBadge}"">{(build.Success ? "SUCCESS" : "FAILED")}</span></div>
        </div>");

        sb.AppendLine($@"
        <div class=""status-item {(build.Errors.Count > 0 ? "error" : "success")}"">
          <div class=""status-label"">Errors</div>
          <div class=""status-value"">{build.Errors.Count}</div>
        </div>");

        sb.AppendLine($@"
        <div class=""status-item {(build.Warnings.Count > 0 ? "warning" : "success")}"">
          <div class=""status-label"">Warnings</div>
          <div class=""status-value"">{build.Warnings.Count}</div>
        </div>");

        sb.AppendLine($@"
        <div class=""status-item info"">
          <div class=""status-label"">Duration</div>
          <div class=""status-value"">{build.Duration.TotalSeconds:F1}s</div>
        </div>");

        sb.AppendLine(@"
      </div>");

        // Show sample errors
        if (build.Errors.Any())
        {
            sb.AppendLine(@"
      <h3 style=""color: var(--error); margin-top: 2rem; margin-bottom: 1rem;"">Build Errors</h3>");

            var errorSample = build.Errors.Take(10);
            foreach (var error in errorSample)
            {
                sb.AppendLine($@"
      <div class=""diagnostic-item error"">
        <div><span class=""diagnostic-code"">{EscapeHtml(error.Code)}</span>: <span class=""diagnostic-message"">{EscapeHtml(error.Message)}</span></div>");

                if (!string.IsNullOrEmpty(error.File))
                {
                    sb.AppendLine($@"        <div class=""diagnostic-location"">{EscapeHtml(error.File)}({error.Line},{error.Column})</div>");
                }

                sb.AppendLine(@"      </div>");
            }

            if (build.Errors.Count > 10)
            {
                sb.AppendLine($@"
      <div style=""color: var(--text-muted); margin-top: 0.5rem; font-style: italic;"">... and {build.Errors.Count - 10} more errors</div>");
            }
        }

        // Show sample warnings
        if (build.Warnings.Any())
        {
            sb.AppendLine(@"
      <h3 style=""color: var(--warning); margin-top: 2rem; margin-bottom: 1rem;"">Build Warnings</h3>");

            var warningSample = build.Warnings.Take(5);
            foreach (var warning in warningSample)
            {
                sb.AppendLine($@"
      <div class=""diagnostic-item warning"">
        <div><span class=""diagnostic-code"">{EscapeHtml(warning.Code)}</span>: <span class=""diagnostic-message"">{EscapeHtml(warning.Message)}</span></div>");

                if (!string.IsNullOrEmpty(warning.File))
                {
                    sb.AppendLine($@"        <div class=""diagnostic-location"">{EscapeHtml(warning.File)}({warning.Line},{warning.Column})</div>");
                }

                sb.AppendLine(@"      </div>");
            }

            if (build.Warnings.Count > 5)
            {
                sb.AppendLine($@"
      <div style=""color: var(--text-muted); margin-top: 0.5rem; font-style: italic;"">... and {build.Warnings.Count - 5} more warnings</div>");
            }
        }

        sb.AppendLine(@"
    </div>");

        return sb.ToString();
    }

    private string GenerateTestResults(MigrationReportData reportData)
    {
        if (reportData.TestResult == null)
        {
            return @"
    <div class=""card"">
      <h2>Test Results</h2>
      <div class=""empty-state"">Test execution not performed</div>
    </div>";
        }

        var test = reportData.TestResult;
        var statusClass = test.Success ? "success" : "error";
        var statusBadge = test.Success ? "badge-success" : "badge-error";
        var passRate = test.TotalTests > 0 ? (test.PassedTests * 100.0 / test.TotalTests) : 0;

        var sb = new StringBuilder();
        sb.AppendLine(@"
    <div class=""card"">
      <h2>Test Results</h2>
      <div class=""status-grid"">");

        sb.AppendLine($@"
        <div class=""status-item {statusClass}"">
          <div class=""status-label"">Status</div>
          <div class=""status-value""><span class=""badge {statusBadge}"">{(test.Success ? "PASSED" : "FAILED")}</span></div>
        </div>");

        sb.AppendLine($@"
        <div class=""status-item success"">
          <div class=""status-label"">Passed</div>
          <div class=""status-value"">{test.PassedTests}/{test.TotalTests}</div>
        </div>");

        sb.AppendLine($@"
        <div class=""status-item {(test.FailedTests > 0 ? "error" : "success")}"">
          <div class=""status-label"">Failed</div>
          <div class=""status-value"">{test.FailedTests}</div>
        </div>");

        sb.AppendLine($@"
        <div class=""status-item {(test.SkippedTests > 0 ? "warning" : "info")}"">
          <div class=""status-label"">Skipped</div>
          <div class=""status-value"">{test.SkippedTests}</div>
        </div>");

        sb.AppendLine($@"
        <div class=""status-item info"">
          <div class=""status-label"">Pass Rate</div>
          <div class=""status-value"">{passRate:F1}%</div>
        </div>");

        sb.AppendLine($@"
        <div class=""status-item info"">
          <div class=""status-label"">Duration</div>
          <div class=""status-value"">{test.Duration.TotalSeconds:F1}s</div>
        </div>");

        sb.AppendLine(@"
      </div>");

        // Show test failures
        if (test.Failures.Any())
        {
            sb.AppendLine(@"
      <h3 style=""color: var(--error); margin-top: 2rem; margin-bottom: 1rem;"">Test Failures</h3>");

            var failureSample = test.Failures.Take(10);
            foreach (var failure in failureSample)
            {
                sb.AppendLine($@"
      <div class=""test-failure-item"">
        <div class=""test-name"">{EscapeHtml(failure.TestName)}</div>
        <div class=""test-error"">{EscapeHtml(failure.ErrorMessage)}</div>");

                if (!string.IsNullOrEmpty(failure.StackTrace))
                {
                    var stackPreview = failure.StackTrace.Length > 500
                        ? failure.StackTrace.Substring(0, 500) + "..."
                        : failure.StackTrace;
                    sb.AppendLine($@"        <div class=""test-stack"">{EscapeHtml(stackPreview)}</div>");
                }

                sb.AppendLine(@"      </div>");
            }

            if (test.Failures.Count > 10)
            {
                sb.AppendLine($@"
      <div style=""color: var(--text-muted); margin-top: 0.5rem; font-style: italic;"">... and {test.Failures.Count - 10} more failures</div>");
            }
        }

        sb.AppendLine(@"
    </div>");

        return sb.ToString();
    }

    private string GenerateIssuesSection(MigrationReportData reportData)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"
    <div class=""card"">
      <h2>Migration Issues</h2>");

        if (!reportData.Issues.Any())
        {
            sb.AppendLine(@"      <div class=""empty-state"">No migration issues detected</div>");
        }
        else
        {
            var groupedIssues = reportData.Issues
                .GroupBy(i => i.Severity)
                .OrderByDescending(g => g.Key);

            sb.AppendLine(@"      <div class=""issue-list"">");

            foreach (var group in groupedIssues)
            {
                var severityClass = group.Key.ToString().ToLowerInvariant();
                var severityBadge = GetSeverityBadgeClass(group.Key);

                sb.AppendLine($@"
        <h3 style=""color: var(--{(group.Key == IssueSeverity.Error ? "error" : group.Key == IssueSeverity.Warning ? "warning" : "info")}); margin-bottom: 1rem;"">{group.Key} ({group.Count()})</h3>");

                foreach (var issue in group)
                {
                    sb.AppendLine($@"
        <div class=""issue-item severity-{severityClass}"">
          <div class=""issue-header"">
            <span class=""issue-code"">{EscapeHtml(issue.Code)}</span>
            <span class=""badge {severityBadge}"">{issue.Severity}</span>
          </div>
          <div class=""issue-message"">{EscapeHtml(issue.Message)}</div>");

                    if (!string.IsNullOrEmpty(issue.FilePath))
                    {
                        sb.AppendLine($@"          <div class=""issue-file"">{EscapeHtml(issue.FilePath)}</div>");
                    }

                    sb.AppendLine(@"        </div>");
                }
            }

            sb.AppendLine(@"      </div>");
        }

        sb.AppendLine(@"
    </div>");

        return sb.ToString();
    }

    private string GenerateRecommendations(MigrationReportData reportData)
    {
        var recommendations = reportData.ConfidenceScore?.Recommendations ?? [];

        var sb = new StringBuilder();
        sb.AppendLine(@"
    <div class=""card"">
      <h2>Recommendations</h2>");

        if (!recommendations.Any())
        {
            sb.AppendLine(@"      <div class=""empty-state"">No specific recommendations</div>");
        }
        else
        {
            sb.AppendLine(@"      <ul class=""recommendations-list"">");

            foreach (var recommendation in recommendations)
            {
                sb.AppendLine($@"        <li>{EscapeHtml(recommendation)}</li>");
            }

            sb.AppendLine(@"      </ul>");
        }

        sb.AppendLine(@"
    </div>");

        return sb.ToString();
    }

    private string GetConfidenceLevelDescription(ConfidenceLevel level)
    {
        return level switch
        {
            ConfidenceLevel.High => "The migration is highly successful with minimal issues. The code is likely production-ready after thorough testing.",
            ConfidenceLevel.Medium => "The migration completed with some issues that require attention. Manual review and additional testing are recommended.",
            ConfidenceLevel.Low => "The migration encountered significant issues. Extensive manual review and remediation are required before production use.",
            _ => "Confidence level could not be determined."
        };
    }

    private string GetSeverityBadgeClass(IssueSeverity severity)
    {
        return severity switch
        {
            IssueSeverity.Error => "badge-error",
            IssueSeverity.Warning => "badge-warning",
            IssueSeverity.Info => "badge-info",
            _ => "badge-info"
        };
    }

    private string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
