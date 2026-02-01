using System.Text;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Mvc.Configuration;

namespace NetLift.Transforms.Mvc.Generators;

/// <summary>
/// Generates _ViewImports.cshtml content for ASP.NET Core projects.
/// Maps legacy System.Web.Mvc namespaces to ASP.NET Core equivalents.
/// </summary>
public sealed class ViewImportsGenerator : IViewImportsGenerator
{
    private readonly HashSet<string> _customNamespaces = new(StringComparer.Ordinal);
    private readonly HashSet<string> _tagHelperAssemblies = new(StringComparer.Ordinal);
    private readonly List<(string TypeName, string PropertyName)> _injectDeclarations = new();

    /// <summary>
    /// Default ASP.NET Core namespaces to include in _ViewImports.cshtml.
    /// </summary>
    private static readonly string[] DefaultNamespaces = new[]
    {
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Mvc.Rendering",
        "Microsoft.AspNetCore.Mvc.ViewFeatures"
    };

    /// <summary>
    /// Default root namespace subdirectories to include (Models, ViewModels).
    /// </summary>
    private static readonly string[] DefaultRootNamespaceSubdirectories = new[]
    {
        "Models",
        "ViewModels"
    };

    public ViewImportsGenerator()
    {
        // Add default TagHelper assembly
        _tagHelperAssemblies.Add("Microsoft.AspNetCore.Mvc.TagHelpers");
    }

    /// <inheritdoc />
    public string Generate(string? rootNamespace = null)
    {
        var sb = new StringBuilder();
        var addedNamespaces = new HashSet<string>(StringComparer.Ordinal);

        // Add TagHelper directives
        foreach (var assembly in _tagHelperAssemblies.OrderBy(a => a, StringComparer.Ordinal))
        {
            sb.AppendLine($"@addTagHelper *, {assembly}");
        }

        if (_tagHelperAssemblies.Count > 0)
        {
            sb.AppendLine();
        }

        // Add default ASP.NET Core namespaces
        foreach (var ns in DefaultNamespaces)
        {
            sb.AppendLine($"@using {ns}");
            addedNamespaces.Add(ns);
        }

        // Add root namespace and common subdirectories
        if (!string.IsNullOrWhiteSpace(rootNamespace))
        {
            sb.AppendLine($"@using {rootNamespace}");
            addedNamespaces.Add(rootNamespace);

            foreach (var subdirectory in DefaultRootNamespaceSubdirectories)
            {
                var fullNamespace = $"{rootNamespace}.{subdirectory}";
                sb.AppendLine($"@using {fullNamespace}");
                addedNamespaces.Add(fullNamespace);
            }
        }

        // Add custom namespaces (filtered, mapped, and deduplicated)
        var mappedNamespaces = _customNamespaces
            .Select(MapLegacyNamespace)
            .Where(ns => ns != null && !addedNamespaces.Contains(ns!))
            .Cast<string>()
            .OrderBy(ns => ns, StringComparer.Ordinal)
            .ToList();

        foreach (var ns in mappedNamespaces)
        {
            sb.AppendLine($"@using {ns}");
            addedNamespaces.Add(ns);
        }

        // Add inject declarations
        if (_injectDeclarations.Count > 0)
        {
            sb.AppendLine();
            foreach (var (typeName, propertyName) in _injectDeclarations.OrderBy(d => d.PropertyName, StringComparer.Ordinal))
            {
                sb.AppendLine($"@inject {typeName} {propertyName}");
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateForArea(string areaName, string? rootNamespace = null)
    {
        if (string.IsNullOrWhiteSpace(areaName))
        {
            throw new ArgumentException("Area name cannot be null or whitespace.", nameof(areaName));
        }

        var sb = new StringBuilder();
        var addedNamespaces = new HashSet<string>(StringComparer.Ordinal);

        // Add TagHelper directives
        foreach (var assembly in _tagHelperAssemblies.OrderBy(a => a, StringComparer.Ordinal))
        {
            sb.AppendLine($"@addTagHelper *, {assembly}");
        }

        if (_tagHelperAssemblies.Count > 0)
        {
            sb.AppendLine();
        }

        // Add default ASP.NET Core namespaces
        foreach (var ns in DefaultNamespaces)
        {
            sb.AppendLine($"@using {ns}");
            addedNamespaces.Add(ns);
        }

        // Add root namespace, area-specific namespace, and common subdirectories
        if (!string.IsNullOrWhiteSpace(rootNamespace))
        {
            sb.AppendLine($"@using {rootNamespace}");
            addedNamespaces.Add(rootNamespace);

            var areaNamespace = $"{rootNamespace}.Areas.{areaName}";
            sb.AppendLine($"@using {areaNamespace}");
            addedNamespaces.Add(areaNamespace);

            var areaModelsNamespace = $"{rootNamespace}.Areas.{areaName}.Models";
            sb.AppendLine($"@using {areaModelsNamespace}");
            addedNamespaces.Add(areaModelsNamespace);

            foreach (var subdirectory in DefaultRootNamespaceSubdirectories)
            {
                var fullNamespace = $"{rootNamespace}.{subdirectory}";
                sb.AppendLine($"@using {fullNamespace}");
                addedNamespaces.Add(fullNamespace);
            }
        }

        // Add custom namespaces (filtered, mapped, and deduplicated)
        var mappedNamespaces = _customNamespaces
            .Select(MapLegacyNamespace)
            .Where(ns => ns != null && !addedNamespaces.Contains(ns!))
            .Cast<string>()
            .OrderBy(ns => ns, StringComparer.Ordinal)
            .ToList();

        foreach (var ns in mappedNamespaces)
        {
            sb.AppendLine($"@using {ns}");
            addedNamespaces.Add(ns);
        }

        // Add inject declarations
        if (_injectDeclarations.Count > 0)
        {
            sb.AppendLine();
            foreach (var (typeName, propertyName) in _injectDeclarations.OrderBy(d => d.PropertyName, StringComparer.Ordinal))
            {
                sb.AppendLine($"@inject {typeName} {propertyName}");
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public void AddNamespace(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns))
        {
            return;
        }

        _customNamespaces.Add(ns);
    }

    /// <inheritdoc />
    public void AddTagHelperAssembly(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return;
        }

        _tagHelperAssemblies.Add(assemblyName);
    }

    /// <inheritdoc />
    public void AddInjectDeclaration(string typeName, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Type name cannot be null or whitespace.", nameof(typeName));
        }

        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("Property name cannot be null or whitespace.", nameof(propertyName));
        }

        _injectDeclarations.Add((typeName, propertyName));
    }

    /// <summary>
    /// Maps legacy System.Web namespaces to ASP.NET Core equivalents.
    /// Filters out System.Web.Optimization and other obsolete namespaces.
    /// </summary>
    /// <param name="ns">The namespace to map.</param>
    /// <returns>The mapped namespace, or null if it should be skipped.</returns>
    private static string? MapLegacyNamespace(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns))
        {
            return null;
        }

        // Skip System.Web.Optimization - no direct equivalent in Core
        if (ns.StartsWith("System.Web.Optimization", StringComparison.Ordinal))
        {
            return null;
        }

        // Skip other System.Web namespaces that don't have direct equivalents
        if (ns.StartsWith("System.Web.", StringComparison.Ordinal) &&
            !MvcNamespaceMappings.RequiresMapping(ns))
        {
            return null;
        }

        // Try to map using MvcNamespaceMappings
        if (MvcNamespaceMappings.RequiresMapping(ns))
        {
            return MvcNamespaceMappings.GetMapping(ns);
        }

        // Keep non-System.Web namespaces as-is
        return ns;
    }
}
