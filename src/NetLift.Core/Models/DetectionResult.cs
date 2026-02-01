namespace NetLift.Core.Models;

/// <summary>
/// Represents the result of detecting a specific project type or feature.
/// </summary>
public class DetectionResult
{
    /// <summary>
    /// Gets or sets whether the type or feature was detected.
    /// </summary>
    public bool Detected { get; set; }

    /// <summary>
    /// Gets or sets the confidence score (0-100).
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// Gets or sets the list of indicators that contributed to the detection.
    /// </summary>
    public List<string> Indicators { get; set; } = new();
}
