using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using NetLift.Transforms.Configuration;

namespace NetLift.Transforms.Services;

/// <summary>
/// Provides package mapping services for migrating legacy NuGet packages to modern equivalents.
/// </summary>
public class PackageMappingService : IPackageMappingService
{
    private readonly Dictionary<string, PackageMappingRule> _mappingIndex;
    private readonly PackageMappingRules _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageMappingService"/> class with built-in rules.
    /// </summary>
    public PackageMappingService()
    {
        _rules = CreateBuiltInRules();
        _mappingIndex = BuildMappingIndex(_rules);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageMappingService"/> class with custom rules.
    /// </summary>
    /// <param name="rules">The package mapping rules to use.</param>
    public PackageMappingService(PackageMappingRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _mappingIndex = BuildMappingIndex(_rules);
    }

    /// <inheritdoc />
    public PackageMappingResult GetMappedPackage(string packageId, string version, string targetFramework)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);

        // Try to find a mapping rule
        if (!_mappingIndex.TryGetValue(packageId.ToLowerInvariant(), out var rule))
        {
            return PackageMappingResult.NoMapping(packageId, version);
        }

        // Check if there's a framework-specific override
        if (rule.FrameworkCompatibility?.TryGetValue(targetFramework, out var frameworkAction) == true)
        {
            return CreateResultFromFrameworkAction(packageId, version, targetFramework, rule, frameworkAction);
        }

        // Check if rule applies to this framework
        if (rule.AppliesTo != null && !rule.AppliesTo.Contains(targetFramework) && !rule.AppliesTo.Contains("*"))
        {
            return PackageMappingResult.NoMapping(packageId, version);
        }

        return CreateResult(packageId, version, targetFramework, rule);
    }

    /// <inheritdoc />
    public bool RequiresMapping(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        return _mappingIndex.ContainsKey(packageId.ToLowerInvariant());
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PackageMappingRule> GetAllMappingRules()
    {
        return _mappingIndex.Values.ToList();
    }

    /// <inheritdoc />
    public bool IsObsolete(string packageId, string targetFramework)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);

        if (!_mappingIndex.TryGetValue(packageId.ToLowerInvariant(), out var rule))
        {
            return false;
        }

        // Check framework-specific action
        if (rule.FrameworkCompatibility?.TryGetValue(targetFramework, out var frameworkAction) == true)
        {
            return frameworkAction.Action == MappingAction.Remove;
        }

        return rule.Action == MappingAction.Remove;
    }

    private PackageMappingResult CreateResult(
        string packageId,
        string version,
        string targetFramework,
        PackageMappingRule rule)
    {
        var warnings = new List<string>();

        // Get recommended version
        string? recommendedVersion = null;
        if (rule.VersionMapping != null)
        {
            if (rule.VersionMapping.TryGetValue(targetFramework, out var frameworkVersion))
            {
                recommendedVersion = frameworkVersion;
            }
            else if (rule.VersionMapping.TryGetValue("*", out var wildcardVersion))
            {
                recommendedVersion = wildcardVersion;
            }
        }

        // Add warnings
        if (rule.RequiresCodeChanges)
        {
            warnings.Add("This package migration requires code changes.");
        }

        if (rule.SecurityUpdate)
        {
            warnings.Add("This is a security-related update and should be applied as soon as possible.");
        }

        if (rule.BreakingChanges)
        {
            warnings.Add("This migration includes breaking changes. Review the migration guide.");
        }

        return new PackageMappingResult
        {
            OriginalPackageId = packageId,
            OriginalVersion = version,
            Action = rule.Action,
            NewPackageId = rule.NewPackageId,
            RecommendedVersion = recommendedVersion,
            Reason = rule.Reason,
            RequiresCodeChanges = rule.RequiresCodeChanges,
            MigrationGuide = rule.MigrationGuide,
            Notes = rule.Notes,
            SuggestedAdditional = rule.SuggestedAdditional,
            SecurityUpdate = rule.SecurityUpdate,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    private PackageMappingResult CreateResultFromFrameworkAction(
        string packageId,
        string version,
        string targetFramework,
        PackageMappingRule rule,
        FrameworkMappingAction frameworkAction)
    {
        var warnings = new List<string>();

        if (rule.RequiresCodeChanges)
        {
            warnings.Add("This package migration requires code changes.");
        }

        if (rule.SecurityUpdate)
        {
            warnings.Add("This is a security-related update and should be applied as soon as possible.");
        }

        return new PackageMappingResult
        {
            OriginalPackageId = packageId,
            OriginalVersion = version,
            Action = frameworkAction.Action,
            NewPackageId = frameworkAction.NewPackageId ?? rule.NewPackageId,
            RecommendedVersion = frameworkAction.Version,
            Reason = rule.Reason,
            RequiresCodeChanges = rule.RequiresCodeChanges,
            MigrationGuide = rule.MigrationGuide,
            Notes = rule.Notes,
            SuggestedAdditional = rule.SuggestedAdditional,
            SecurityUpdate = rule.SecurityUpdate,
            Warnings = warnings.Count > 0 ? warnings : null
        };
    }

    private Dictionary<string, PackageMappingRule> BuildMappingIndex(PackageMappingRules rules)
    {
        var index = new Dictionary<string, PackageMappingRule>(StringComparer.OrdinalIgnoreCase);

        // Add all mappings from different categories
        var allMappings = new[]
        {
            rules.Mappings?.Select(m => m.ToDomainModel()),
            rules.AspnetMigrations?.Select(m => m.ToDomainModel()),
            rules.EfMigrations?.Select(m => m.ToDomainModel()),
            rules.TestingMigrations?.Select(m => m.ToDomainModel()),
            rules.LoggingMigrations?.Select(m => m.ToDomainModel()),
            rules.SecurityMigrations?.Select(m => m.ToDomainModel()),
            rules.Analyzers?.Select(a => a.ToDomainModel()),
            rules.ObsoletePackages?.Select(o => o.ToDomainModel())
        }
        .Where(m => m != null)
        .SelectMany(m => m!);

        foreach (var mapping in allMappings)
        {
            index[mapping.OldPackageId.ToLowerInvariant()] = mapping;
        }

        return index;
    }

    private static PackageMappingRules CreateBuiltInRules()
    {
        var rules = new PackageMappingRules
        {
            Version = "1.0",
            Description = "Built-in NuGet package migration rules for NetLift"
        };

        // ASP.NET Migrations
        rules.AspnetMigrations = new List<MappingRuleDto>
        {
            new()
            {
                OldPackage = "Microsoft.AspNet.Mvc",
                Action = "remove",
                Reason = "ASP.NET MVC 5 functionality is built into ASP.NET Core framework",
                RequiresCodeChanges = true,
                MigrationGuide = "https://docs.microsoft.com/aspnet/core/migration/mvc",
                Notes = "Namespace changes required: System.Web.Mvc → Microsoft.AspNetCore.Mvc"
            },
            new()
            {
                OldPackage = "Microsoft.AspNet.WebApi",
                Action = "remove",
                Reason = "ASP.NET Web API functionality is built into ASP.NET Core framework",
                RequiresCodeChanges = true,
                MigrationGuide = "https://docs.microsoft.com/aspnet/core/migration/webapi",
                Notes = "Namespace changes required: System.Web.Http → Microsoft.AspNetCore.Mvc"
            },
            new()
            {
                OldPackage = "Microsoft.AspNet.WebApi.Core",
                Action = "remove",
                Reason = "Functionality is built into ASP.NET Core framework"
            },
            new()
            {
                OldPackage = "Microsoft.AspNet.WebApi.Client",
                Action = "remove",
                Reason = "Use System.Net.Http.Json instead"
            },
            new()
            {
                OldPackage = "Microsoft.Owin",
                Action = "remove",
                Reason = "Not needed in ASP.NET Core - use middleware instead",
                RequiresCodeChanges = true
            },
            new()
            {
                OldPackage = "Microsoft.Owin.Security",
                Action = "remove",
                Reason = "Use ASP.NET Core authentication middleware",
                RequiresCodeChanges = true
            }
        };

        // Entity Framework Migrations
        rules.EfMigrations = new List<MappingRuleDto>
        {
            new()
            {
                OldPackage = "EntityFramework",
                Action = "replace",
                NewPackage = "Microsoft.EntityFrameworkCore",
                VersionMapping = new Dictionary<string, string>
                {
                    ["net6.0"] = "6.0.33",
                    ["net8.0"] = "8.0.0"
                },
                Reason = "EF6 is legacy - migrate to Entity Framework Core",
                RequiresCodeChanges = true,
                MigrationGuide = "https://docs.microsoft.com/ef/core/porting/",
                Notes = "Consider keeping EF6 for compatibility. EF Core requires significant code changes."
            }
        };

        // System.Web.* packages
        rules.ObsoletePackages = new List<ObsoletePackageDto>
        {
            new()
            {
                Package = "System.Web",
                Reason = "System.Web is not available in .NET Core/5+. Use ASP.NET Core equivalents.",
                Action = "remove"
            },
            new()
            {
                Package = "System.Web.Mvc",
                Reason = "Part of legacy ASP.NET. Use ASP.NET Core MVC.",
                Action = "remove"
            },
            new()
            {
                Package = "System.Web.Http",
                Reason = "Part of legacy ASP.NET Web API. Use ASP.NET Core.",
                Action = "remove"
            },
            new()
            {
                Package = "Microsoft.Bcl",
                Reason = "Functionality is built into modern .NET",
                Action = "remove"
            },
            new()
            {
                Package = "Microsoft.Bcl.Async",
                Reason = "Async/await is built into modern .NET",
                Action = "remove"
            },
            new()
            {
                Package = "Microsoft.Net.Compilers",
                Reason = "Roslyn compilers are built into .NET SDK",
                Action = "remove"
            },
            new()
            {
                Package = "Microsoft.CodeDom.Providers.DotNetCompilerPlatform",
                Reason = "Not needed with SDK-style projects",
                Action = "remove"
            }
        };

        // JSON serialization
        rules.Mappings = new List<MappingRuleDto>
        {
            new()
            {
                OldPackage = "Newtonsoft.Json",
                Action = "keep",
                Reason = "Newtonsoft.Json is still widely used. System.Text.Json is built into modern .NET but may require code changes.",
                Notes = "Consider migrating to System.Text.Json for better performance, but Newtonsoft.Json is still supported."
            }
        };

        // Logging
        rules.LoggingMigrations = new List<MappingRuleDto>
        {
            new()
            {
                OldPackage = "log4net",
                Action = "manual",
                Reason = "Consider migrating to Microsoft.Extensions.Logging with Serilog or NLog",
                Suggestion = "Use Microsoft.Extensions.Logging.Abstractions with Serilog.Extensions.Logging or NLog.Extensions.Logging"
            },
            new()
            {
                OldPackage = "NLog",
                Action = "keep",
                SuggestedAdditional = new List<string> { "NLog.Extensions.Logging" },
                Reason = "NLog works with .NET Core/5+ with the Extensions.Logging integration"
            },
            new()
            {
                OldPackage = "Serilog",
                Action = "keep",
                SuggestedAdditional = new List<string>
                {
                    "Serilog.Extensions.Logging",
                    "Serilog.Sinks.Console",
                    "Serilog.Sinks.File"
                },
                Reason = "Serilog works with .NET Core/5+ with the Extensions.Logging integration"
            }
        };

        // Testing frameworks
        rules.TestingMigrations = new List<MappingRuleDto>
        {
            new()
            {
                OldPackage = "NUnit",
                Action = "upgrade",
                VersionMapping = new Dictionary<string, string> { ["*"] = "4.1.0" }
            },
            new()
            {
                OldPackage = "xunit",
                Action = "upgrade",
                VersionMapping = new Dictionary<string, string> { ["*"] = "2.6.6" }
            },
            new()
            {
                OldPackage = "Moq",
                Action = "upgrade",
                VersionMapping = new Dictionary<string, string> { ["*"] = "4.20.70" }
            },
            new()
            {
                OldPackage = "FluentAssertions",
                Action = "upgrade",
                VersionMapping = new Dictionary<string, string> { ["*"] = "6.12.0" }
            }
        };

        // Security packages
        rules.SecurityMigrations = new List<MappingRuleDto>
        {
            new()
            {
                OldPackage = "System.IdentityModel.Tokens.Jwt",
                Action = "upgrade",
                VersionMapping = new Dictionary<string, string> { ["*"] = "7.3.1" },
                SecurityUpdate = true
            },
            new()
            {
                OldPackage = "Microsoft.IdentityModel.Tokens",
                Action = "upgrade",
                VersionMapping = new Dictionary<string, string> { ["*"] = "7.3.1" },
                SecurityUpdate = true
            }
        };

        // Analyzers
        rules.Analyzers = new List<AnalyzerRuleDto>
        {
            new()
            {
                Package = "StyleCop.Analyzers",
                Action = "keep",
                LatestVersion = "1.2.0-beta.556",
                Reason = "StyleCop.Analyzers works with modern .NET"
            },
            new()
            {
                Package = "Microsoft.CodeAnalysis.FxCopAnalyzers",
                Action = "replace",
                NewPackage = "Microsoft.CodeAnalysis.NetAnalyzers",
                Reason = "FxCopAnalyzers is deprecated",
                VersionMapping = new Dictionary<string, string> { ["*"] = "8.0.0" }
            }
        };

        return rules;
    }
}
