namespace NetLift.Core.Interfaces;

/// <summary>
/// Generates _ViewImports.cshtml content for ASP.NET Core projects.
/// Includes @addTagHelper directives, @using statements, and @inject declarations.
/// </summary>
public interface IViewImportsGenerator
{
    /// <summary>
    /// Generates a complete _ViewImports.cshtml file content.
    /// </summary>
    /// <param name="rootNamespace">The root namespace of the application for generating application-specific usings.</param>
    /// <returns>A formatted _ViewImports.cshtml file content.</returns>
    string Generate(string? rootNamespace = null);

    /// <summary>
    /// Generates a _ViewImports.cshtml file content for a specific area.
    /// </summary>
    /// <param name="areaName">The name of the area.</param>
    /// <param name="rootNamespace">The root namespace of the application.</param>
    /// <returns>A formatted _ViewImports.cshtml file content with area-specific usings.</returns>
    string GenerateForArea(string areaName, string? rootNamespace = null);

    /// <summary>
    /// Adds a custom namespace to be included in the @using directives.
    /// </summary>
    /// <param name="ns">The namespace to add.</param>
    void AddNamespace(string ns);

    /// <summary>
    /// Adds a TagHelper assembly to be included in @addTagHelper directives.
    /// </summary>
    /// <param name="assemblyName">The assembly name containing tag helpers.</param>
    void AddTagHelperAssembly(string assemblyName);

    /// <summary>
    /// Adds an @inject declaration for dependency injection in views.
    /// </summary>
    /// <param name="typeName">The fully qualified type name to inject.</param>
    /// <param name="propertyName">The property name to use in views.</param>
    void AddInjectDeclaration(string typeName, string propertyName);
}
