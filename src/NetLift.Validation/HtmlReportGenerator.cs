namespace NetLift.Validation;

using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using System.Text;

/// <summary>
/// Generates self-contained HTML reports from analysis results.
/// </summary>
public class HtmlReportGenerator : IHtmlReportGenerator
{
    /// <summary>
    /// Generates a complete self-contained HTML report.
    /// </summary>
    /// <param name="report">The analysis report to convert to HTML.</param>
    /// <returns>A complete HTML document as a string.</returns>
    public string Generate(AnalysisReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>NetLift Analysis - {EscapeHtml(report.SolutionName)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetEmbeddedCss());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        sb.AppendLine(GenerateHeader(report));
        sb.AppendLine(GenerateOverview(report));
        sb.AppendLine(GenerateProjectsSection(report));
        sb.AppendLine(GeneratePackagesSection(report));
        sb.AppendLine(GenerateIssuesSection(report));
        sb.AppendLine(GenerateMigrationPlan(report));

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GetEmbeddedCss()
    {
        return @"
:root {
    --primary: #8b5cf6;
    --primary-dark: #6d28d9;
    --success: #22c55e;
    --warning: #eab308;
    --error: #ef4444;
    --info: #3b82f6;
    --bg: #0f0f1e;
    --bg-secondary: #1a1a2e;
    --card: #16213e;
    --card-hover: #1e2d4f;
    --text: #e2e8f0;
    --text-muted: #94a3b8;
    --border: #2d3748;
}

* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', sans-serif;
    background: var(--bg);
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
    max-width: 1200px;
    margin: 0 auto;
}

.header {
    background: linear-gradient(135deg, var(--primary) 0%, var(--primary-dark) 100%);
    border-radius: 12px;
    padding: 2rem;
    margin-bottom: 2rem;
    text-align: center;
}

.header h1 {
    font-size: 2rem;
    font-weight: 700;
    margin-bottom: 0.5rem;
    color: white;
}

.header .timestamp {
    color: rgba(255, 255, 255, 0.8);
    font-size: 0.875rem;
}

.card {
    background: var(--card);
    border-radius: 8px;
    padding: 1.5rem;
    margin-bottom: 1.5rem;
    border: 1px solid var(--border);
}

.card h2 {
    color: var(--primary);
    font-size: 1.5rem;
    margin-bottom: 1rem;
    font-weight: 600;
}

.card h3 {
    color: var(--primary);
    font-size: 1.25rem;
    margin-bottom: 0.75rem;
    font-weight: 600;
}

.stats-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 1rem;
    margin-top: 1rem;
}

.stat-card {
    background: var(--bg-secondary);
    padding: 1rem;
    border-radius: 8px;
    border-left: 4px solid var(--primary);
}

.stat-value {
    font-size: 2rem;
    font-weight: 700;
    color: var(--primary);
    display: block;
}

.stat-label {
    color: var(--text-muted);
    font-size: 0.875rem;
    display: block;
    margin-top: 0.25rem;
}

.projects-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
    gap: 1rem;
    margin-top: 1rem;
}

.project-card {
    background: var(--bg-secondary);
    border-radius: 8px;
    padding: 1.25rem;
    border: 1px solid var(--border);
    transition: transform 0.2s, box-shadow 0.2s;
}

.project-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(139, 92, 246, 0.2);
}

.project-name {
    font-size: 1.125rem;
    font-weight: 600;
    margin-bottom: 0.75rem;
    color: var(--text);
}

.project-meta {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-bottom: 0.75rem;
}

.project-stats {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 0.5rem;
    margin-top: 0.75rem;
    font-size: 0.875rem;
    color: var(--text-muted);
}

table {
    width: 100%;
    border-collapse: collapse;
    margin-top: 1rem;
}

thead {
    background: var(--bg-secondary);
}

th {
    padding: 0.75rem;
    text-align: left;
    color: var(--text-muted);
    font-weight: 500;
    font-size: 0.875rem;
    border-bottom: 2px solid var(--border);
}

td {
    padding: 0.75rem;
    border-bottom: 1px solid var(--border);
}

tbody tr:hover {
    background: var(--bg-secondary);
}

.badge {
    display: inline-block;
    padding: 0.25rem 0.75rem;
    border-radius: 9999px;
    font-size: 0.75rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.025em;
}

.badge-low {
    background: rgba(34, 197, 94, 0.2);
    color: #22c55e;
}

.badge-medium {
    background: rgba(234, 179, 8, 0.2);
    color: #eab308;
}

.badge-high {
    background: rgba(249, 115, 22, 0.2);
    color: #f97316;
}

.badge-very-high {
    background: rgba(239, 68, 68, 0.2);
    color: #ef4444;
}

.badge-compatible {
    background: rgba(34, 197, 94, 0.2);
    color: #22c55e;
}

.badge-has-replacement {
    background: rgba(234, 179, 8, 0.2);
    color: #eab308;
}

.badge-incompatible {
    background: rgba(239, 68, 68, 0.2);
    color: #ef4444;
}

.badge-deprecated {
    background: rgba(107, 114, 128, 0.2);
    color: #9ca3af;
}

.badge-unknown {
    background: rgba(100, 116, 139, 0.2);
    color: #64748b;
}

.badge-info {
    background: rgba(59, 130, 246, 0.2);
    color: #3b82f6;
}

.badge-warning {
    background: rgba(234, 179, 8, 0.2);
    color: #eab308;
}

.badge-error {
    background: rgba(239, 68, 68, 0.2);
    color: #ef4444;
}

.badge-blocker {
    background: rgba(220, 38, 38, 0.3);
    color: #fca5a5;
}

.badge-primary {
    background: rgba(139, 92, 246, 0.2);
    color: var(--primary);
}

.issue-item {
    background: var(--bg-secondary);
    padding: 1rem;
    border-radius: 6px;
    border-left: 4px solid var(--border);
    margin-bottom: 0.75rem;
}

.issue-item.severity-info {
    border-left-color: var(--info);
}

.issue-item.severity-warning {
    border-left-color: var(--warning);
}

.issue-item.severity-error {
    border-left-color: var(--error);
}

.issue-item.severity-blocker {
    border-left-color: #dc2626;
}

.issue-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 0.5rem;
}

.issue-description {
    color: var(--text);
    margin-bottom: 0.5rem;
}

.issue-meta {
    font-size: 0.875rem;
    color: var(--text-muted);
}

.phase-card {
    background: var(--bg-secondary);
    padding: 1.25rem;
    border-radius: 8px;
    border-left: 4px solid var(--primary);
    margin-bottom: 1rem;
}

.phase-header {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    margin-bottom: 0.75rem;
}

.phase-number {
    background: var(--primary);
    color: white;
    width: 2rem;
    height: 2rem;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-weight: 700;
}

.phase-title {
    font-size: 1.125rem;
    font-weight: 600;
    color: var(--text);
}

.phase-description {
    color: var(--text-muted);
    margin-bottom: 0.75rem;
}

.phase-projects {
    margin-top: 0.75rem;
}

.phase-projects-title {
    font-size: 0.875rem;
    font-weight: 600;
    color: var(--text-muted);
    margin-bottom: 0.5rem;
}

.phase-projects-list {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
}

.manual-steps {
    margin-top: 0.75rem;
}

.manual-steps-title {
    font-size: 0.875rem;
    font-weight: 600;
    color: var(--text-muted);
    margin-bottom: 0.5rem;
}

.manual-steps-list {
    list-style: none;
    padding-left: 0;
}

.manual-steps-list li {
    padding-left: 1.5rem;
    position: relative;
    margin-bottom: 0.25rem;
    color: var(--text-muted);
    font-size: 0.875rem;
}

.manual-steps-list li::before {
    content: '→';
    position: absolute;
    left: 0;
    color: var(--primary);
}

.empty-state {
    text-align: center;
    padding: 2rem;
    color: var(--text-muted);
}

.progress-bar {
    height: 8px;
    background: var(--bg-secondary);
    border-radius: 4px;
    overflow: hidden;
    margin-top: 0.5rem;
}

.progress-fill {
    height: 100%;
    background: var(--primary);
    border-radius: 4px;
    transition: width 0.3s;
}
";
    }

    private string GenerateHeader(AnalysisReport report)
    {
        return $@"
    <div class=""header"">
      <h1>NetLift Analysis Report</h1>
      <p class=""timestamp"">Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} | Tool Version: {EscapeHtml(report.ToolVersion)}</p>
    </div>";
    }

    private string GenerateOverview(AnalysisReport report)
    {
        var complexityColor = GetComplexityBadgeClass(report.OverallComplexity?.Level ?? ComplexityLevel.Medium);
        var complexityText = report.OverallComplexity?.Level.ToString() ?? "Unknown";

        return $@"
    <div class=""card"">
      <h2>Solution Overview</h2>
      <h3>{EscapeHtml(report.SolutionName)}</h3>
      <p style=""color: var(--text-muted); margin-bottom: 1rem;"">{EscapeHtml(report.SolutionPath)}</p>

      <div class=""stats-grid"">
        <div class=""stat-card"">
          <span class=""stat-value"">{report.TotalProjects}</span>
          <span class=""stat-label"">Total Projects</span>
        </div>
        <div class=""stat-card"">
          <span class=""stat-value""><span class=""badge {complexityColor}"">{complexityText}</span></span>
          <span class=""stat-label"">Overall Complexity</span>
        </div>
        <div class=""stat-card"">
          <span class=""stat-value"">{report.EstimatedAutoMigrationPercentage}%</span>
          <span class=""stat-label"">Estimated Auto-Migration</span>
          <div class=""progress-bar"">
            <div class=""progress-fill"" style=""width: {report.EstimatedAutoMigrationPercentage}%""></div>
          </div>
        </div>
        <div class=""stat-card"">
          <span class=""stat-value"">{EscapeHtml(report.TargetFramework)}</span>
          <span class=""stat-label"">Target Framework</span>
        </div>
      </div>
    </div>";
    }

    private string GenerateProjectsSection(AnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"
    <div class=""card"">
      <h2>Projects</h2>
      <div class=""projects-grid"">");

        foreach (var project in report.Projects)
        {
            var complexityBadge = GetComplexityBadgeClass(project.Complexity?.Level ?? ComplexityLevel.Medium);
            var complexityText = project.Complexity?.Level.ToString() ?? "Unknown";

            var projectTypes = new List<string>();
            if (project.IsMvc) projectTypes.Add("MVC");
            if (project.IsWebApi) projectTypes.Add("Web API");
            if (project.IsWcfService) projectTypes.Add("WCF");
            if (project.UsesEf6) projectTypes.Add("EF6");

            sb.AppendLine($@"
        <div class=""project-card"">
          <div class=""project-name"">{EscapeHtml(project.ProjectName)}</div>
          <div class=""project-meta"">");

            foreach (var type in projectTypes)
            {
                sb.AppendLine($@"            <span class=""badge badge-primary"">{type}</span>");
            }

            sb.AppendLine($@"            <span class=""badge {complexityBadge}"">{complexityText}</span>
          </div>
          <div class=""project-stats"">
            <div>Framework: {EscapeHtml(project.CurrentFramework?.ToString() ?? "Unknown")}</div>
            <div>Dependencies: {project.DependencyCount}</div>
            <div>Files: {project.SourceFileCount}</div>
            <div>Est. LOC: {project.EstimatedLinesOfCode:N0}</div>
          </div>
        </div>");
        }

        if (report.Projects.Count == 0)
        {
            sb.AppendLine(@"        <div class=""empty-state"">No projects found</div>");
        }

        sb.AppendLine(@"      </div>
    </div>");

        return sb.ToString();
    }

    private string GeneratePackagesSection(AnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"
    <div class=""card"">
      <h2>Package Dependencies</h2>");

        // Collect all unique packages across all projects
        var allPackages = report.Projects
            .SelectMany(p => p.Dependencies)
            .GroupBy(d => d.PackageId)
            .Select(g => g.First())
            .OrderBy(d => d.PackageId)
            .ToList();

        if (allPackages.Any())
        {
            sb.AppendLine(@"
      <table>
        <thead>
          <tr>
            <th>Package</th>
            <th>Current Version</th>
            <th>Compatibility</th>
            <th>Notes</th>
          </tr>
        </thead>
        <tbody>");

            foreach (var dep in allPackages)
            {
                var compatBadge = GetCompatibilityBadgeClass(dep.Compatibility);
                var compatText = dep.Compatibility.ToString();
                var notes = "";

                if (!string.IsNullOrEmpty(dep.RecommendedVersion))
                {
                    notes = $"Upgrade to {EscapeHtml(dep.RecommendedVersion)}";
                }
                else if (!string.IsNullOrEmpty(dep.ReplacementPackage))
                {
                    notes = $"Replace with {EscapeHtml(dep.ReplacementPackage)}";
                }
                else if (!string.IsNullOrEmpty(dep.Notes))
                {
                    notes = EscapeHtml(dep.Notes);
                }

                sb.AppendLine($@"
          <tr>
            <td><strong>{EscapeHtml(dep.PackageId)}</strong></td>
            <td>{EscapeHtml(dep.CurrentVersion)}</td>
            <td><span class=""badge {compatBadge}"">{compatText}</span></td>
            <td style=""color: var(--text-muted); font-size: 0.875rem;"">{notes}</td>
          </tr>");
            }

            sb.AppendLine(@"
        </tbody>
      </table>");
        }
        else
        {
            sb.AppendLine(@"      <div class=""empty-state"">No package dependencies found</div>");
        }

        sb.AppendLine(@"    </div>");

        return sb.ToString();
    }

    private string GenerateIssuesSection(AnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"
    <div class=""card"">
      <h2>Compatibility Issues</h2>");

        if (report.Issues.Any())
        {
            var groupedIssues = report.Issues.GroupBy(i => i.Severity).OrderByDescending(g => g.Key);

            foreach (var group in groupedIssues)
            {
                var severityText = group.Key.ToString();
                var severityClass = $"severity-{group.Key.ToString().ToLowerInvariant()}";
                var severityBadge = GetSeverityBadgeClass(group.Key);

                sb.AppendLine($@"
      <h3>{severityText} ({group.Count()})</h3>");

                foreach (var issue in group)
                {
                    var location = "";
                    if (!string.IsNullOrEmpty(issue.AffectedFile))
                    {
                        location = $"File: {EscapeHtml(issue.AffectedFile)}";
                        if (issue.LineNumber.HasValue)
                        {
                            location += $" (Line {issue.LineNumber})";
                        }
                    }

                    sb.AppendLine($@"
      <div class=""issue-item {severityClass}"">
        <div class=""issue-header"">
          <span class=""badge {severityBadge}"">{EscapeHtml(issue.Category)}</span>
          <span style=""color: var(--text-muted); font-size: 0.875rem;"">{EscapeHtml(issue.AffectedProject)}</span>
        </div>
        <div class=""issue-description"">{EscapeHtml(issue.Description)}</div>");

                    if (!string.IsNullOrEmpty(location))
                    {
                        sb.AppendLine($@"        <div class=""issue-meta"">{location}</div>");
                    }

                    if (!string.IsNullOrEmpty(issue.Recommendation))
                    {
                        sb.AppendLine($@"        <div class=""issue-meta"" style=""margin-top: 0.5rem;""><strong>Recommendation:</strong> {EscapeHtml(issue.Recommendation)}</div>");
                    }

                    sb.AppendLine(@"      </div>");
                }
            }
        }
        else
        {
            sb.AppendLine(@"      <div class=""empty-state"">No compatibility issues found</div>");
        }

        sb.AppendLine(@"    </div>");

        return sb.ToString();
    }

    private string GenerateMigrationPlan(AnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"
    <div class=""card"">
      <h2>Recommended Migration Plan</h2>");

        if (report.RecommendedPhases.Any())
        {
            foreach (var phase in report.RecommendedPhases.OrderBy(p => p.Order))
            {
                sb.AppendLine($@"
      <div class=""phase-card"">
        <div class=""phase-header"">
          <div class=""phase-number"">{phase.Order}</div>
          <div class=""phase-title"">{EscapeHtml(phase.Name)}</div>
        </div>
        <div class=""phase-description"">{EscapeHtml(phase.Description)}</div>
        <div style=""color: var(--text-muted); font-size: 0.875rem;"">
          Estimated Auto-Migration: {phase.EstimatedAutoPercentage}%
          <div class=""progress-bar"">
            <div class=""progress-fill"" style=""width: {phase.EstimatedAutoPercentage}%""></div>
          </div>
        </div>");

                if (phase.AffectedProjects.Any())
                {
                    sb.AppendLine(@"
        <div class=""phase-projects"">
          <div class=""phase-projects-title"">Affected Projects:</div>
          <div class=""phase-projects-list"">");

                    foreach (var projectName in phase.AffectedProjects)
                    {
                        sb.AppendLine($@"            <span class=""badge badge-primary"">{EscapeHtml(projectName)}</span>");
                    }

                    sb.AppendLine(@"          </div>
        </div>");
                }

                if (phase.ManualSteps.Any())
                {
                    sb.AppendLine(@"
        <div class=""manual-steps"">
          <div class=""manual-steps-title"">Manual Steps:</div>
          <ul class=""manual-steps-list"">");

                    foreach (var step in phase.ManualSteps)
                    {
                        sb.AppendLine($@"            <li>{EscapeHtml(step)}</li>");
                    }

                    sb.AppendLine(@"          </ul>
        </div>");
                }

                sb.AppendLine(@"      </div>");
            }
        }
        else
        {
            sb.AppendLine(@"      <div class=""empty-state"">No migration phases defined</div>");
        }

        sb.AppendLine(@"    </div>");

        return sb.ToString();
    }

    private string GetComplexityBadgeClass(ComplexityLevel level)
    {
        return level switch
        {
            ComplexityLevel.Low => "badge-low",
            ComplexityLevel.Medium => "badge-medium",
            ComplexityLevel.High => "badge-high",
            ComplexityLevel.VeryHigh => "badge-very-high",
            _ => "badge-unknown"
        };
    }

    private string GetCompatibilityBadgeClass(PackageCompatibility compatibility)
    {
        return compatibility switch
        {
            PackageCompatibility.Compatible => "badge-compatible",
            PackageCompatibility.HasReplacement => "badge-has-replacement",
            PackageCompatibility.Incompatible => "badge-incompatible",
            PackageCompatibility.Deprecated => "badge-deprecated",
            _ => "badge-unknown"
        };
    }

    private string GetSeverityBadgeClass(IssueSeverity severity)
    {
        return severity switch
        {
            IssueSeverity.Info => "badge-info",
            IssueSeverity.Warning => "badge-warning",
            IssueSeverity.Error => "badge-error",
            IssueSeverity.Blocker => "badge-blocker",
            _ => "badge-unknown"
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
