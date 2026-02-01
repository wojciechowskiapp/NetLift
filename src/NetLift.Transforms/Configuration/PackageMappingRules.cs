using NetLift.Core.Models;

namespace NetLift.Transforms.Configuration;

/// <summary>
/// Represents the complete package mapping rules configuration.
/// </summary>
public class PackageMappingRules
{
    /// <summary>
    /// Gets or sets the rules version.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the description of the rules.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets global settings for rule processing.
    /// </summary>
    public RuleSettings Settings { get; set; } = new();

    /// <summary>
    /// Gets or sets the general package mappings.
    /// </summary>
    public List<MappingRuleDto> Mappings { get; set; } = new();

    /// <summary>
    /// Gets or sets framework-specific package removals.
    /// </summary>
    public Dictionary<string, List<string>> FrameworkPackages { get; set; } = new();

    /// <summary>
    /// Gets or sets analyzer package mappings.
    /// </summary>
    public List<AnalyzerRuleDto> Analyzers { get; set; } = new();

    /// <summary>
    /// Gets or sets ASP.NET-specific migrations.
    /// </summary>
    public List<MappingRuleDto> AspnetMigrations { get; set; } = new();

    /// <summary>
    /// Gets or sets Entity Framework migrations.
    /// </summary>
    public List<MappingRuleDto> EfMigrations { get; set; } = new();

    /// <summary>
    /// Gets or sets testing framework migrations.
    /// </summary>
    public List<MappingRuleDto> TestingMigrations { get; set; } = new();

    /// <summary>
    /// Gets or sets logging framework migrations.
    /// </summary>
    public List<MappingRuleDto> LoggingMigrations { get; set; } = new();

    /// <summary>
    /// Gets or sets obsolete packages.
    /// </summary>
    public List<ObsoletePackageDto> ObsoletePackages { get; set; } = new();

    /// <summary>
    /// Gets or sets security-related migrations.
    /// </summary>
    public List<MappingRuleDto> SecurityMigrations { get; set; } = new();
}

/// <summary>
/// Global settings for rule processing.
/// </summary>
public class RuleSettings
{
    /// <summary>
    /// Gets or sets the default action when no rule is found.
    /// </summary>
    public string DefaultAction { get; set; } = "keep";

    /// <summary>
    /// Gets or sets a value indicating whether to preserve version when no mapping is found.
    /// </summary>
    public bool PreserveVersion { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to warn on major version upgrades.
    /// </summary>
    public bool WarnOnMajorUpgrade { get; set; } = true;
}

/// <summary>
/// DTO for mapping rule in YAML (matches snake_case naming).
/// </summary>
public class MappingRuleDto
{
    public string OldPackage { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? NewPackage { get; set; }
    public Dictionary<string, string>? VersionMapping { get; set; }
    public string? Reason { get; set; }
    public bool RequiresCodeChanges { get; set; }
    public string? MigrationGuide { get; set; }
    public string? Notes { get; set; }
    public string? Suggestion { get; set; }
    public List<string>? RelatedPackages { get; set; }
    public Dictionary<string, FrameworkActionDto>? FrameworkCompatibility { get; set; }
    public List<string>? SuggestedAdditional { get; set; }
    public bool SecurityUpdate { get; set; }
    public bool BreakingChanges { get; set; }
    public List<string>? AppliesTo { get; set; }

    /// <summary>
    /// Converts DTO to domain model.
    /// </summary>
    public PackageMappingRule ToDomainModel()
    {
        return new PackageMappingRule
        {
            OldPackageId = OldPackage,
            Action = ParseAction(Action),
            NewPackageId = NewPackage,
            VersionMapping = VersionMapping,
            Reason = Reason,
            RequiresCodeChanges = RequiresCodeChanges,
            MigrationGuide = MigrationGuide,
            Notes = Notes,
            Suggestion = Suggestion,
            RelatedPackages = RelatedPackages,
            FrameworkCompatibility = FrameworkCompatibility?
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToDomainModel()
                ),
            SuggestedAdditional = SuggestedAdditional,
            SecurityUpdate = SecurityUpdate,
            BreakingChanges = BreakingChanges,
            AppliesTo = AppliesTo
        };
    }

    private static MappingAction ParseAction(string action)
    {
        return action.ToLowerInvariant() switch
        {
            "keep" => MappingAction.Keep,
            "replace" => MappingAction.Replace,
            "remove" => MappingAction.Remove,
            "upgrade" => MappingAction.Upgrade,
            "manual" => MappingAction.Manual,
            _ => MappingAction.Keep
        };
    }
}

/// <summary>
/// DTO for framework-specific action.
/// </summary>
public class FrameworkActionDto
{
    public string Action { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? NewPackage { get; set; }

    public FrameworkMappingAction ToDomainModel()
    {
        return new FrameworkMappingAction
        {
            Action = ParseAction(Action),
            Version = Version,
            NewPackageId = NewPackage
        };
    }

    private static MappingAction ParseAction(string action)
    {
        return action.ToLowerInvariant() switch
        {
            "keep" => MappingAction.Keep,
            "replace" => MappingAction.Replace,
            "remove" => MappingAction.Remove,
            "upgrade" => MappingAction.Upgrade,
            "manual" => MappingAction.Manual,
            _ => MappingAction.Keep
        };
    }
}

/// <summary>
/// DTO for analyzer rule.
/// </summary>
public class AnalyzerRuleDto
{
    public string Package { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? NewPackage { get; set; }
    public string? LatestVersion { get; set; }
    public string? PrivateAssets { get; set; }
    public string? Reason { get; set; }
    public Dictionary<string, string>? VersionMapping { get; set; }

    public PackageMappingRule ToDomainModel()
    {
        var action = Action.ToLowerInvariant() switch
        {
            "keep" => MappingAction.Keep,
            "replace" => MappingAction.Replace,
            "remove" => MappingAction.Remove,
            "upgrade" => MappingAction.Upgrade,
            "manual" => MappingAction.Manual,
            _ => MappingAction.Keep
        };

        return new PackageMappingRule
        {
            OldPackageId = Package,
            Action = action,
            NewPackageId = NewPackage,
            VersionMapping = VersionMapping ?? (LatestVersion != null
                ? new Dictionary<string, string> { ["*"] = LatestVersion }
                : null),
            Reason = Reason
        };
    }
}

/// <summary>
/// DTO for obsolete package.
/// </summary>
public class ObsoletePackageDto
{
    public string Package { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Action { get; set; } = "remove";

    public PackageMappingRule ToDomainModel()
    {
        var action = Action.ToLowerInvariant() switch
        {
            "keep" => MappingAction.Keep,
            "replace" => MappingAction.Replace,
            "remove" => MappingAction.Remove,
            "upgrade" => MappingAction.Upgrade,
            "manual" => MappingAction.Manual,
            _ => MappingAction.Remove
        };

        return new PackageMappingRule
        {
            OldPackageId = Package,
            Action = action,
            Reason = Reason
        };
    }
}
