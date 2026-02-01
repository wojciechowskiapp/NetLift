namespace NetLift.Core.Interfaces;

using NetLift.Core.Models;

/// <summary>
/// Interface for generating comprehensive HTML migration reports.
/// </summary>
public interface IFullHtmlReportGenerator
{
    /// <summary>
    /// Generates a comprehensive self-contained HTML report from migration data.
    /// </summary>
    /// <param name="reportData">The migration report data to generate HTML from.</param>
    /// <returns>A complete HTML document as a string.</returns>
    string Generate(MigrationReportData reportData);
}
