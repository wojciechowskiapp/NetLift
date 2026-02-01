using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NetLift.Transforms.Configuration;

/// <summary>
/// Parses package mapping rules from YAML configuration files.
/// </summary>
public class PackageMappingRulesParser
{
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageMappingRulesParser"/> class.
    /// </summary>
    public PackageMappingRulesParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Loads and parses rules from a YAML file.
    /// </summary>
    /// <param name="yamlPath">The path to the YAML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed package mapping rules.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the YAML file is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the YAML is invalid.</exception>
    public async Task<PackageMappingRules> LoadRulesAsync(string yamlPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(yamlPath))
        {
            throw new FileNotFoundException($"Package mapping rules file not found: {yamlPath}", yamlPath);
        }

        var yaml = await File.ReadAllTextAsync(yamlPath, cancellationToken);
        return ParseRules(yaml);
    }

    /// <summary>
    /// Parses rules from a YAML string.
    /// </summary>
    /// <param name="yaml">The YAML content.</param>
    /// <returns>The parsed package mapping rules.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the YAML is invalid.</exception>
    public PackageMappingRules ParseRules(string yaml)
    {
        try
        {
            var rules = _deserializer.Deserialize<PackageMappingRules>(yaml);
            ValidateRules(rules);
            return rules;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException($"Invalid YAML format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the parsed rules for consistency and required fields.
    /// </summary>
    private void ValidateRules(PackageMappingRules rules)
    {
        var allMappings = new[]
        {
            rules.Mappings,
            rules.AspnetMigrations,
            rules.EfMigrations,
            rules.TestingMigrations,
            rules.LoggingMigrations,
            rules.SecurityMigrations
        }.SelectMany(m => m ?? Enumerable.Empty<MappingRuleDto>());

        foreach (var mapping in allMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.OldPackage))
            {
                throw new InvalidOperationException("Mapping rule must have an old_package specified.");
            }

            if (string.IsNullOrWhiteSpace(mapping.Action))
            {
                throw new InvalidOperationException($"Mapping rule for '{mapping.OldPackage}' must have an action specified.");
            }

            var actionLower = mapping.Action.ToLowerInvariant();
            if (actionLower == "replace" && string.IsNullOrWhiteSpace(mapping.NewPackage))
            {
                throw new InvalidOperationException(
                    $"Mapping rule for '{mapping.OldPackage}' has action 'replace' but no new_package specified.");
            }
        }

        // Validate analyzers
        if (rules.Analyzers != null)
        {
            foreach (var analyzer in rules.Analyzers)
            {
                if (string.IsNullOrWhiteSpace(analyzer.Package))
                {
                    throw new InvalidOperationException("Analyzer rule must have a package specified.");
                }

                if (string.IsNullOrWhiteSpace(analyzer.Action))
                {
                    throw new InvalidOperationException($"Analyzer rule for '{analyzer.Package}' must have an action specified.");
                }
            }
        }

        // Validate obsolete packages
        if (rules.ObsoletePackages != null)
        {
            foreach (var obsolete in rules.ObsoletePackages)
            {
                if (string.IsNullOrWhiteSpace(obsolete.Package))
                {
                    throw new InvalidOperationException("Obsolete package rule must have a package specified.");
                }
            }
        }
    }
}
