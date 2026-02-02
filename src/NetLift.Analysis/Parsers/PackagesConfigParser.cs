using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Analysis.Parsers;

/// <summary>
/// Parses packages.config files to extract NuGet package dependencies.
/// </summary>
public class PackagesConfigParser : IPackagesConfigParser
{
    /// <inheritdoc />
    public List<PackageReference> Parse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new List<PackageReference>();
        }

        if (!File.Exists(filePath))
        {
            return new List<PackageReference>();
        }

        try
        {
            var doc = XDocument.Load(filePath);

            return doc.Descendants("package")
                .Select(ParsePackageElement)
                .Where(p => !string.IsNullOrWhiteSpace(p.Id))
                .ToList();
        }
        catch (System.Xml.XmlException)
        {
            // Malformed XML - return empty list
            return new List<PackageReference>();
        }
        catch (IOException)
        {
            // File access issue - return empty list
            return new List<PackageReference>();
        }
        catch (UnauthorizedAccessException)
        {
            // Permission denied - return empty list
            return new List<PackageReference>();
        }
    }

    private static PackageReference ParsePackageElement(XElement packageElement)
    {
        return new PackageReference
        {
            Id = packageElement.Attribute("id")?.Value ?? string.Empty,
            Version = packageElement.Attribute("version")?.Value ?? string.Empty,
            TargetFramework = packageElement.Attribute("targetFramework")?.Value,
            IsDevelopmentDependency =
                string.Equals(
                    packageElement.Attribute("developmentDependency")?.Value,
                    "true",
                    StringComparison.OrdinalIgnoreCase)
        };
    }
}
