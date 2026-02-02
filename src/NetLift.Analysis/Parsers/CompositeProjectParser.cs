using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Analysis.Parsers;

/// <summary>
/// Composite project parser that delegates to the appropriate parser based on project format.
/// Automatically selects between OldFormatProjectParser and SdkProjectParser.
/// </summary>
public class CompositeProjectParser : IProjectParser
{
    private readonly OldFormatProjectParser _oldFormatParser;
    private readonly SdkProjectParser _sdkParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeProjectParser"/> class.
    /// </summary>
    public CompositeProjectParser()
    {
        _oldFormatParser = new OldFormatProjectParser();
        _sdkParser = new SdkProjectParser();
    }

    /// <inheritdoc/>
    public bool CanParse(string projectPath)
    {
        return _oldFormatParser.CanParse(projectPath) || _sdkParser.CanParse(projectPath);
    }

    /// <inheritdoc/>
    public async Task<ProjectInfo> AnalyzeAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        // Try old format first (this is the primary migration scenario)
        if (_oldFormatParser.CanParse(projectPath))
        {
            return await _oldFormatParser.AnalyzeAsync(projectPath, cancellationToken);
        }

        // Fall back to SDK-style parser
        if (_sdkParser.CanParse(projectPath))
        {
            return await _sdkParser.AnalyzeAsync(projectPath, cancellationToken);
        }

        // If neither parser can handle it, throw an exception
        throw new InvalidOperationException(
            $"Unable to parse project file: {projectPath}. " +
            "The file is neither a valid old-style nor SDK-style .csproj file.");
    }
}
