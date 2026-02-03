using NetLift.Core.Models.DependencyInjection;

namespace NetLift.Core.Interfaces.DependencyInjection;

/// <summary>
/// Analyzes and transforms property injection patterns.
/// </summary>
public interface IPropertyInjectionAnalyzer
{
    /// <summary>
    /// Analyzes a type for property injection patterns.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <param name="typeName">The type name to analyze.</param>
    /// <returns>Property injection information.</returns>
    Task<PropertyInjectionInfo> AnalyzeAsync(string content, string typeName);

    /// <summary>
    /// Analyzes a file for property injection patterns.
    /// </summary>
    /// <param name="filePath">The file path to analyze.</param>
    /// <returns>List of property injection infos for types in the file.</returns>
    Task<List<PropertyInjectionInfo>> AnalyzeFileAsync(string filePath);

    /// <summary>
    /// Determines if property injection can be converted to constructor injection.
    /// </summary>
    /// <param name="info">The property injection info.</param>
    /// <returns>True if conversion is possible.</returns>
    bool CanConvertToConstructorInjection(PropertyInjectionInfo info);

    /// <summary>
    /// Generates a constructor from property dependencies.
    /// </summary>
    /// <param name="info">The property injection info.</param>
    /// <returns>The generated constructor code.</returns>
    string GenerateConstructor(PropertyInjectionInfo info);

    /// <summary>
    /// Transforms property injection to constructor injection.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <param name="typeName">The type to transform.</param>
    /// <returns>The transformed source code.</returns>
    string TransformToConstructorInjection(string content, string typeName);

    /// <summary>
    /// Detects [Dependency] or [Inject] attributes on properties.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <param name="typeName">The type to analyze.</param>
    /// <returns>List of property dependencies.</returns>
    IReadOnlyList<PropertyDependency> DetectInjectedProperties(string content, string typeName);
}
