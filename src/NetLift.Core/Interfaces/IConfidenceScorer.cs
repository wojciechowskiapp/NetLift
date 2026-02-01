namespace NetLift.Core.Interfaces;

using NetLift.Core.Models;

/// <summary>
/// Service for calculating migration confidence scores.
/// </summary>
public interface IConfidenceScorer
{
    /// <summary>
    /// Calculates the overall confidence score for a migration.
    /// </summary>
    /// <param name="context">The validation context containing build results, test results, and migration metrics.</param>
    /// <returns>A confidence score with component breakdowns and recommendations.</returns>
    ConfidenceScore CalculateScore(MigrationValidationContext context);
}
