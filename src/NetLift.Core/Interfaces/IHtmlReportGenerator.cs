namespace NetLift.Core.Interfaces;

using NetLift.Core.Models;

/// <summary>
/// Interface for generating HTML reports from analysis results.
/// </summary>
public interface IHtmlReportGenerator
{
    /// <summary>
    /// Generates a self-contained HTML report from the analysis results.
    /// </summary>
    /// <param name="report">The analysis report to generate HTML from.</param>
    /// <returns>A complete HTML document as a string.</returns>
    string Generate(AnalysisReport report);
}
