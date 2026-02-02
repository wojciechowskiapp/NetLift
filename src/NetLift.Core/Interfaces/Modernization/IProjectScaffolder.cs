namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Scaffolds Clean Architecture project structure with folders and common files.
/// </summary>
public interface IProjectScaffolder
{
    /// <summary>
    /// Scaffolds Clean Architecture folder structure and generates common infrastructure files.
    /// </summary>
    /// <param name="projectPath">The root path where the project structure will be created.</param>
    /// <param name="rootNamespace">The root namespace for generated files (e.g., "ContosoUniversity").</param>
    /// <param name="options">Configuration options for scaffolding.</param>
    /// <returns>A result containing created directories, files, and any errors.</returns>
    ScaffoldResult Scaffold(string projectPath, string rootNamespace, ScaffoldOptions options);
}

/// <summary>
/// Configuration options for scaffolding Clean Architecture structure.
/// </summary>
public sealed record ScaffoldOptions
{
    /// <summary>
    /// Gets or initializes a value indicating whether to create the Domain layer.
    /// </summary>
    public bool CreateDomainLayer { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether to create the Application layer.
    /// </summary>
    public bool CreateApplicationLayer { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether to create the Infrastructure layer.
    /// </summary>
    public bool CreateInfrastructureLayer { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether to generate common/helper files.
    /// </summary>
    public bool GenerateCommonFiles { get; init; } = true;
}

/// <summary>
/// Represents the result of a scaffolding operation.
/// </summary>
public sealed record ScaffoldResult
{
    /// <summary>
    /// Gets whether the scaffolding was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the list of created directories.
    /// </summary>
    public IReadOnlyList<string> CreatedDirectories { get; init; } = [];

    /// <summary>
    /// Gets the list of created files with metadata.
    /// </summary>
    public IReadOnlyList<Models.Modernization.GeneratedFileInfo> CreatedFiles { get; init; } = [];

    /// <summary>
    /// Gets the list of errors encountered during scaffolding.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}
