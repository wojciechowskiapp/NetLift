using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Transforms;

/// <summary>
/// Extracts assembly information from AssemblyInfo.cs files using Roslyn.
/// Converts legacy assembly attributes to SDK-style project properties.
/// </summary>
public sealed class AssemblyInfoExtractor : IAssemblyInfoExtractor
{
    private static readonly HashSet<string> KnownAssemblyInfoFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AssemblyInfo.cs",
        "GlobalAssemblyInfo.cs",
        "SharedAssemblyInfo.cs",
        "CommonAssemblyInfo.cs"
    };

    /// <inheritdoc />
    public async Task<AssemblyInfoData> ExtractAsync(string assemblyInfoPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assemblyInfoPath))
        {
            throw new ArgumentException("Assembly info path cannot be null or empty.", nameof(assemblyInfoPath));
        }

        if (!File.Exists(assemblyInfoPath))
        {
            throw new FileNotFoundException($"AssemblyInfo.cs file not found: {assemblyInfoPath}");
        }

        var sourceText = await File.ReadAllTextAsync(assemblyInfoPath, cancellationToken);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: cancellationToken);
        var root = await syntaxTree.GetRootAsync(cancellationToken);

        var result = new AssemblyInfoData
        {
            FilePath = assemblyInfoPath
        };

        // Find all assembly-level attributes
        var attributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(IsAssemblyAttribute)
            .ToList();

        foreach (var attribute in attributes)
        {
            ExtractAttribute(attribute, result);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AssemblyInfoData> ExtractAndMergeAsync(IEnumerable<string> assemblyInfoPaths, CancellationToken cancellationToken = default)
    {
        var paths = assemblyInfoPaths?.ToList() ?? throw new ArgumentNullException(nameof(assemblyInfoPaths));

        if (paths.Count == 0)
        {
            throw new ArgumentException("At least one assembly info path must be provided.", nameof(assemblyInfoPaths));
        }

        if (paths.Count == 1)
        {
            return await ExtractAsync(paths[0], cancellationToken);
        }

        var extractedData = new List<AssemblyInfoData>();
        foreach (var path in paths)
        {
            var data = await ExtractAsync(path, cancellationToken);
            extractedData.Add(data);
        }

        return MergeAssemblyInfoData(extractedData);
    }

    /// <inheritdoc />
    public bool CanExtract(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var fileName = Path.GetFileName(filePath);
        return KnownAssemblyInfoFileNames.Contains(fileName) && File.Exists(filePath);
    }

    private static bool IsAssemblyAttribute(AttributeSyntax attribute)
    {
        // Check if attribute is applied at assembly level by looking at the parent AttributeList
        var attributeList = attribute.Parent as AttributeListSyntax;
        return attributeList?.Target?.Identifier.Text == "assembly";
    }

    private static void ExtractAttribute(AttributeSyntax attribute, AssemblyInfoData result)
    {
        var attributeName = GetAttributeName(attribute.Name);
        var firstArgument = attribute.ArgumentList?.Arguments.FirstOrDefault();
        var value = ExtractArgumentValue(firstArgument);

        switch (attributeName)
        {
            case "AssemblyTitle":
                result.Title = value;
                break;

            case "AssemblyDescription":
                result.Description = value;
                break;

            case "AssemblyCompany":
                result.Company = value;
                break;

            case "AssemblyProduct":
                result.Product = value;
                break;

            case "AssemblyCopyright":
                result.Copyright = value;
                break;

            case "AssemblyTrademark":
                result.Trademark = value;
                break;

            case "AssemblyCulture":
                result.Culture = value;
                break;

            case "AssemblyVersion":
                result.AssemblyVersion = value;
                break;

            case "AssemblyFileVersion":
                result.FileVersion = value;
                break;

            case "AssemblyInformationalVersion":
                result.InformationalVersion = value;
                break;

            case "AssemblyConfiguration":
                result.Configuration = value;
                break;

            case "Guid":
                result.Guid = value;
                break;

            case "ComVisible":
                if (bool.TryParse(value, out var comVisible))
                {
                    result.ComVisible = comVisible;
                }
                break;

            case "NeutralResourcesLanguage":
                result.NeutralLanguage = value;
                break;

            case "InternalsVisibleTo":
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Handle public key tokens by extracting just the assembly name
                    var assemblyName = value.Split(',')[0].Trim();
                    result.InternalsVisibleTo.Add(assemblyName);
                }
                break;

            default:
                // Custom attribute - preserve it
                var customAttribute = new CustomAttribute
                {
                    Name = attributeName,
                    FullSyntax = attribute.ToString()
                };

                // Extract all arguments
                if (attribute.ArgumentList != null)
                {
                    foreach (var arg in attribute.ArgumentList.Arguments)
                    {
                        var argValue = ExtractArgumentValue(arg);
                        if (!string.IsNullOrWhiteSpace(argValue))
                        {
                            customAttribute.Arguments.Add(argValue);
                        }
                    }
                }

                result.CustomAttributes.Add(customAttribute);
                break;
        }
    }

    private static string GetAttributeName(NameSyntax nameSyntax)
    {
        var name = nameSyntax.ToString();

        // Remove "Attribute" suffix if present
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - "Attribute".Length);
        }

        return name;
    }

    private static string ExtractArgumentValue(AttributeArgumentSyntax? argument)
    {
        if (argument?.Expression == null)
        {
            return string.Empty;
        }

        return argument.Expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText ?? literal.Token.ToString(),
            MemberAccessExpressionSyntax memberAccess => memberAccess.ToString(),
            InvocationExpressionSyntax invocation => invocation.ToString(),
            _ => argument.Expression.ToString()
        };
    }

    private static AssemblyInfoData MergeAssemblyInfoData(List<AssemblyInfoData> dataList)
    {
        var merged = new AssemblyInfoData
        {
            FilePath = string.Join(";", dataList.Select(d => d.FilePath))
        };

        foreach (var data in dataList)
        {
            // Project-specific values override shared values (last one wins)
            merged.Title ??= data.Title;
            merged.Description ??= data.Description;
            merged.Company ??= data.Company;
            merged.Product ??= data.Product;
            merged.Copyright ??= data.Copyright;
            merged.Trademark ??= data.Trademark;
            merged.Culture ??= data.Culture;
            merged.AssemblyVersion ??= data.AssemblyVersion;
            merged.FileVersion ??= data.FileVersion;
            merged.InformationalVersion ??= data.InformationalVersion;
            merged.Configuration ??= data.Configuration;
            merged.Guid ??= data.Guid;
            merged.ComVisible ??= data.ComVisible;
            merged.NeutralLanguage ??= data.NeutralLanguage;

            // Merge collections
            merged.InternalsVisibleTo.AddRange(data.InternalsVisibleTo);
            merged.CustomAttributes.AddRange(data.CustomAttributes);
        }

        // Deduplicate collections
        merged.InternalsVisibleTo.RemoveAll(string.IsNullOrWhiteSpace);
        var distinctInternalsVisibleTo = merged.InternalsVisibleTo.Distinct().ToList();
        merged.InternalsVisibleTo.Clear();
        merged.InternalsVisibleTo.AddRange(distinctInternalsVisibleTo);

        return merged;
    }
}
