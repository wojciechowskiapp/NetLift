using NetLift.Core.Models.Wcf;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Detects WCF duplex/callback patterns that cannot be automatically migrated to .NET Core.
/// </summary>
public interface IDuplexDetector
{
    /// <summary>
    /// Analyzes WCF service contracts and configuration to detect duplex patterns.
    /// </summary>
    /// <param name="contracts">The list of WCF service contracts to analyze.</param>
    /// <param name="config">The WCF service configuration containing bindings and endpoints.</param>
    /// <returns>A report containing duplex warnings and migration guidance.</returns>
    DuplexWarningReport Detect(
        IReadOnlyList<WcfServiceContract> contracts,
        WcfServiceConfiguration? config);

    /// <summary>
    /// Gets diagnostic messages generated during duplex detection.
    /// Useful for debugging and understanding detection logic.
    /// </summary>
    IReadOnlyCollection<string> Diagnostics { get; }
}
