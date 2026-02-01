using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Transforms.Converters;

/// <summary>
/// Converts packages.config references to modern PackageReference format.
/// </summary>
public class PackageReferenceConverter : IPackageReferenceConverter
{
    // Packages that are now built into .NET and should be removed
    private static readonly Dictionary<string, string> ObsoletePackages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.Bcl"] = "Functionality is now built into .NET",
        ["Microsoft.Bcl.Async"] = "Async/await is now built into .NET",
        ["Microsoft.Bcl.Build"] = "No longer needed in modern .NET",
        ["Microsoft.Net.Compilers"] = "Roslyn compilers are built into SDK",
        ["Microsoft.CodeDom.Providers.DotNetCompilerPlatform"] = "Not needed with Roslyn SDK",
        ["Microsoft.Web.Infrastructure"] = "Not needed in modern .NET",
        ["Microsoft.AspNet.Razor"] = "Razor engine is built into ASP.NET Core"
    };

    // Packages that need to be replaced with modern equivalents
    private static readonly Dictionary<string, (string? NewPackageId, string? SuggestedVersion, string Reason)> PackageReplacements =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Microsoft.AspNet.Mvc"] = ("Microsoft.AspNetCore.Mvc", "2.2.0", "ASP.NET MVC 5 → ASP.NET Core MVC"),
            ["Microsoft.AspNet.WebApi"] = ("Microsoft.AspNetCore.Mvc", "2.2.0", "WebAPI is unified with MVC in ASP.NET Core"),
            ["Microsoft.AspNet.WebApi.Client"] = ("Microsoft.AspNetCore.Mvc", "2.2.0", "WebAPI client functionality in ASP.NET Core"),
            ["Microsoft.AspNet.WebPages"] = ("Microsoft.AspNetCore.Mvc.Razor", "2.2.0", "Razor pages are part of ASP.NET Core MVC"),
            ["System.Net.Http"] = ("System.Net.Http", null, "Updated version for modern .NET"),
            ["System.ValueTuple"] = (null, null, "ValueTuple is built into modern .NET") // Will be removed
        };

    // Analyzers and development-only packages that need PrivateAssets
    private static readonly HashSet<string> AnalyzerPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.CodeAnalysis",
        "Microsoft.CodeAnalysis.CSharp",
        "Microsoft.CodeAnalysis.Analyzers",
        "StyleCop.Analyzers",
        "SonarAnalyzer.CSharp",
        "Roslynator.Analyzers"
    };

    /// <inheritdoc />
    public PackageConversionResult Convert(PackagesConfig packagesConfig, string targetFramework)
    {
        if (packagesConfig == null)
        {
            throw new ArgumentNullException(nameof(packagesConfig));
        }

        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            throw new ArgumentException("Target framework cannot be null or empty.", nameof(targetFramework));
        }

        var result = new PackageConversionResult();

        foreach (var package in packagesConfig.Packages)
        {
            ProcessPackage(package, targetFramework, result);
        }

        // Remove duplicates, keeping the highest version
        result.Packages = DeduplicatePackages(result.Packages);

        return result;
    }

    private void ProcessPackage(PackageReference package, string targetFramework, PackageConversionResult result)
    {
        // Check if package is obsolete (should be removed)
        if (IsObsoletePackage(package.Id, out var obsoleteReason))
        {
            result.RemovedPackages.Add(package);
            result.Warnings.Add(new ConversionWarning
            {
                Severity = WarningSeverity.Info,
                PackageId = package.Id,
                Message = $"Package '{package.Id}' removed: {obsoleteReason}"
            });
            return;
        }

        // Check if package needs replacement
        if (PackageReplacements.TryGetValue(package.Id, out var replacement))
        {
            if (replacement.NewPackageId == null)
            {
                // Package should be removed (e.g., System.ValueTuple)
                result.RemovedPackages.Add(package);
                result.Warnings.Add(new ConversionWarning
                {
                    Severity = WarningSeverity.Info,
                    PackageId = package.Id,
                    Message = $"Package '{package.Id}' removed: {replacement.Reason}"
                });
                return;
            }

            var newPackage = new PackageReference
            {
                Id = replacement.NewPackageId,
                Version = replacement.SuggestedVersion ?? package.Version,
                IsDevelopmentDependency = package.IsDevelopmentDependency
            };

            result.Packages.Add(newPackage);
            result.Replacements.Add(new PackageReplacement
            {
                OldPackage = package,
                NewPackage = newPackage,
                Reason = replacement.Reason
            });

            result.Warnings.Add(new ConversionWarning
            {
                Severity = WarningSeverity.Warning,
                PackageId = package.Id,
                Message = $"Package '{package.Id}' replaced with '{newPackage.Id}': {replacement.Reason}"
            });
            return;
        }

        // Check if this is a System.* package that might be in framework
        if (IsFrameworkPackage(package.Id, targetFramework))
        {
            result.RemovedPackages.Add(package);
            result.Warnings.Add(new ConversionWarning
            {
                Severity = WarningSeverity.Info,
                PackageId = package.Id,
                Message = $"Package '{package.Id}' is now part of {targetFramework} framework"
            });
            return;
        }

        // Keep the package as-is
        result.Packages.Add(package);
    }

    private bool IsObsoletePackage(string packageId, out string reason)
    {
        if (ObsoletePackages.TryGetValue(packageId, out var msg))
        {
            reason = msg;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private bool IsFrameworkPackage(string packageId, string targetFramework)
    {
        // For .NET 5+ and .NET Core 3.0+, most System.* packages are in the framework
        if (!targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check if it's a .NET Core/5+ target (not .NET Framework)
        var isModernDotNet = targetFramework.StartsWith("net5", StringComparison.OrdinalIgnoreCase) ||
                            targetFramework.StartsWith("net6", StringComparison.OrdinalIgnoreCase) ||
                            targetFramework.StartsWith("net7", StringComparison.OrdinalIgnoreCase) ||
                            targetFramework.StartsWith("net8", StringComparison.OrdinalIgnoreCase) ||
                            targetFramework.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase) ||
                            targetFramework.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase);

        if (!isModernDotNet)
        {
            return false;
        }

        // Common System.* packages that are now in framework
        var frameworkPackages = new[]
        {
            "System.Runtime",
            "System.Collections",
            "System.Linq",
            "System.Threading.Tasks",
            "System.IO",
            "System.Xml",
            "System.Text.RegularExpressions",
            "System.Net.Http",
            "System.Reflection",
            "System.ComponentModel",
            "System.Diagnostics.Debug",
            "System.Runtime.Extensions",
            "System.Collections.Concurrent",
            "System.Threading"
        };

        return frameworkPackages.Contains(packageId, StringComparer.OrdinalIgnoreCase);
    }

    private List<PackageReference> DeduplicatePackages(List<PackageReference> packages)
    {
        return packages
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => GetHighestVersion(g.ToList()))
            .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private PackageReference GetHighestVersion(List<PackageReference> packages)
    {
        if (packages.Count == 1)
        {
            return packages[0];
        }

        // Try to parse versions and get the highest
        var packagesWithParsedVersions = packages
            .Select(p => new
            {
                Package = p,
                ParsedVersion = TryParseVersion(p.Version),
                IsPreRelease = IsPreReleaseVersion(p.Version)
            })
            .Where(x => x.ParsedVersion != null)
            .ToList();

        if (packagesWithParsedVersions.Any())
        {
            // Prefer stable versions over pre-release, then highest version
            return packagesWithParsedVersions
                .OrderBy(x => x.IsPreRelease) // false (stable) comes before true (pre-release)
                .ThenByDescending(x => x.ParsedVersion)
                .First()
                .Package;
        }

        // If we can't parse versions, return the first one
        return packages[0];
    }

    private Version? TryParseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
        {
            return null;
        }

        // Handle pre-release versions by taking only the numeric part
        var numericPart = versionString.Split('-', '+')[0];

        // Handle versions with less than 4 parts
        if (Version.TryParse(numericPart, out var version))
        {
            return version;
        }

        return null;
    }

    private bool IsPreReleaseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
        {
            return false;
        }

        // Pre-release versions contain '-' or '+'
        return versionString.Contains('-') || versionString.Contains('+');
    }

    /// <summary>
    /// Determines if a package is an analyzer and needs PrivateAssets="all".
    /// </summary>
    /// <param name="packageId">The package identifier.</param>
    /// <returns>True if the package is an analyzer; otherwise, false.</returns>
    public static bool IsAnalyzerPackage(string packageId)
    {
        if (AnalyzerPackages.Contains(packageId))
        {
            return true;
        }

        return packageId.Contains("Analyzer", StringComparison.OrdinalIgnoreCase) ||
               packageId.Contains("CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
               packageId.EndsWith(".Analyzers", StringComparison.OrdinalIgnoreCase);
    }
}
