using NetLift.Core.Interfaces.DependencyInjection;
using NetLift.Core.Models.DependencyInjection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NetLift.Transforms.DependencyInjection.Mappers;

/// <summary>
/// Maps legacy DI framework lifetimes to Microsoft.Extensions.DependencyInjection.ServiceLifetime.
/// </summary>
public sealed class LifetimeMapper : ILifetimeMapper
{
    private readonly Dictionary<DIFrameworkType, List<LifetimeMapping>> _mappings = new();
    private LifetimeMapping _defaultMapping = new()
    {
        SourceLifetime = "Unknown",
        Framework = DIFrameworkType.Unknown,
        TargetLifetime = ServiceLifetime.Scoped,
        ConfidenceScore = 50,
        Notes = "Unknown lifetime, defaulting to scoped"
    };

    /// <summary>
    /// Creates a new LifetimeMapper with default mappings.
    /// </summary>
    public LifetimeMapper()
    {
        InitializeDefaultMappings();
    }

    /// <inheritdoc />
    public LifetimeMapping MapLifetime(string sourceLifetime, DIFrameworkType framework)
    {
        if (string.IsNullOrWhiteSpace(sourceLifetime))
            return _defaultMapping with { Framework = framework };

        if (_mappings.TryGetValue(framework, out var frameworkMappings))
        {
            var mapping = frameworkMappings.FirstOrDefault(m =>
                m.SourceLifetime.Equals(sourceLifetime, StringComparison.OrdinalIgnoreCase));

            if (mapping != null)
                return mapping;
        }

        // Try to infer from common patterns
        var inferredLifetime = InferLifetime(sourceLifetime);
        if (inferredLifetime.HasValue)
        {
            return new LifetimeMapping
            {
                SourceLifetime = sourceLifetime,
                Framework = framework,
                TargetLifetime = inferredLifetime.Value,
                ConfidenceScore = 70,
                Notes = "Inferred from naming pattern"
            };
        }

        return _defaultMapping with
        {
            SourceLifetime = sourceLifetime,
            Framework = framework
        };
    }

    /// <inheritdoc />
    public ServiceLifetime GetServiceLifetime(LifetimeMapping mapping)
    {
        return mapping.TargetLifetime;
    }

    /// <inheritdoc />
    public async Task LoadMappingsFromYamlAsync(string yamlPath)
    {
        if (!File.Exists(yamlPath))
            return;

        var yaml = await File.ReadAllTextAsync(yamlPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<Dictionary<string, object>>(yaml);

        LoadFrameworkMappings(config, "autofac", DIFrameworkType.Autofac);
        LoadFrameworkMappings(config, "unity", DIFrameworkType.Unity);
        LoadFrameworkMappings(config, "ninject", DIFrameworkType.Ninject);
        LoadFrameworkMappings(config, "structuremap", DIFrameworkType.StructureMap);

        if (config.TryGetValue("default", out var defaultObj) && defaultObj is Dictionary<object, object> defaultDict)
        {
            _defaultMapping = new LifetimeMapping
            {
                SourceLifetime = "Unknown",
                Framework = DIFrameworkType.Unknown,
                TargetLifetime = ParseServiceLifetime(defaultDict.GetValueOrDefault("target")?.ToString() ?? "Scoped"),
                ConfidenceScore = int.TryParse(defaultDict.GetValueOrDefault("confidence")?.ToString(), out var conf) ? conf : 50,
                Notes = defaultDict.GetValueOrDefault("notes")?.ToString()
            };
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LifetimeMapping> GetMappingsForFramework(DIFrameworkType framework)
    {
        return _mappings.TryGetValue(framework, out var mappings)
            ? mappings.AsReadOnly()
            : Array.Empty<LifetimeMapping>();
    }

    private void LoadFrameworkMappings(Dictionary<string, object> config, string key, DIFrameworkType framework)
    {
        if (!config.TryGetValue(key, out var value))
            return;

        if (value is not List<object> mappingsList)
            return;

        var mappings = new List<LifetimeMapping>();
        foreach (var item in mappingsList)
        {
            if (item is not Dictionary<object, object> dict)
                continue;

            var mapping = new LifetimeMapping
            {
                SourceLifetime = dict.GetValueOrDefault("source")?.ToString() ?? "",
                Framework = framework,
                TargetLifetime = ParseServiceLifetime(dict.GetValueOrDefault("target")?.ToString() ?? "Scoped"),
                ConfidenceScore = int.TryParse(dict.GetValueOrDefault("confidence")?.ToString(), out var conf) ? conf : 80,
                Notes = dict.GetValueOrDefault("notes")?.ToString()
            };

            mappings.Add(mapping);
        }

        _mappings[framework] = mappings;
    }

    private static ServiceLifetime ParseServiceLifetime(string value)
    {
        return value?.ToLowerInvariant() switch
        {
            "singleton" => ServiceLifetime.Singleton,
            "scoped" => ServiceLifetime.Scoped,
            "transient" => ServiceLifetime.Transient,
            _ => ServiceLifetime.Scoped
        };
    }

    private static ServiceLifetime? InferLifetime(string sourceLifetime)
    {
        var lower = sourceLifetime.ToLowerInvariant();

        if (lower.Contains("singleton") || lower.Contains("single"))
            return ServiceLifetime.Singleton;

        if (lower.Contains("transient") || lower.Contains("unique") || lower.Contains("perdependency"))
            return ServiceLifetime.Transient;

        if (lower.Contains("scoped") || lower.Contains("request") || lower.Contains("perlifetime") || lower.Contains("hierarchical"))
            return ServiceLifetime.Scoped;

        return null;
    }

    private void InitializeDefaultMappings()
    {
        // Autofac
        _mappings[DIFrameworkType.Autofac] =
        [
            new() { SourceLifetime = "SingleInstance", Framework = DIFrameworkType.Autofac, TargetLifetime = ServiceLifetime.Singleton, ConfidenceScore = 100 },
            new() { SourceLifetime = "InstancePerLifetimeScope", Framework = DIFrameworkType.Autofac, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 100 },
            new() { SourceLifetime = "InstancePerDependency", Framework = DIFrameworkType.Autofac, TargetLifetime = ServiceLifetime.Transient, ConfidenceScore = 100 },
            new() { SourceLifetime = "InstancePerRequest", Framework = DIFrameworkType.Autofac, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 95, Notes = "HTTP request scope maps to scoped" },
            new() { SourceLifetime = "InstancePerMatchingLifetimeScope", Framework = DIFrameworkType.Autofac, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 80, Notes = "Named scope, may need manual review" },
            new() { SourceLifetime = "InstancePerOwned", Framework = DIFrameworkType.Autofac, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 75, Notes = "Owned<T> pattern, may need manual review" },
            new() { SourceLifetime = "ExternallyOwned", Framework = DIFrameworkType.Autofac, TargetLifetime = ServiceLifetime.Transient, ConfidenceScore = 70, Notes = "External disposal" }
        ];

        // Unity
        _mappings[DIFrameworkType.Unity] =
        [
            new() { SourceLifetime = "ContainerControlledLifetimeManager", Framework = DIFrameworkType.Unity, TargetLifetime = ServiceLifetime.Singleton, ConfidenceScore = 100 },
            new() { SourceLifetime = "HierarchicalLifetimeManager", Framework = DIFrameworkType.Unity, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 100 },
            new() { SourceLifetime = "TransientLifetimeManager", Framework = DIFrameworkType.Unity, TargetLifetime = ServiceLifetime.Transient, ConfidenceScore = 100 },
            new() { SourceLifetime = "PerRequestLifetimeManager", Framework = DIFrameworkType.Unity, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 95, Notes = "HTTP request scope maps to scoped" },
            new() { SourceLifetime = "PerResolveLifetimeManager", Framework = DIFrameworkType.Unity, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 85, Notes = "Per resolve, similar to scoped" },
            new() { SourceLifetime = "PerThreadLifetimeManager", Framework = DIFrameworkType.Unity, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 75, Notes = "Thread scope has no direct equivalent" }
        ];

        // Ninject
        _mappings[DIFrameworkType.Ninject] =
        [
            new() { SourceLifetime = "InSingletonScope", Framework = DIFrameworkType.Ninject, TargetLifetime = ServiceLifetime.Singleton, ConfidenceScore = 100 },
            new() { SourceLifetime = "InRequestScope", Framework = DIFrameworkType.Ninject, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 100 },
            new() { SourceLifetime = "InTransientScope", Framework = DIFrameworkType.Ninject, TargetLifetime = ServiceLifetime.Transient, ConfidenceScore = 100 },
            new() { SourceLifetime = "InThreadScope", Framework = DIFrameworkType.Ninject, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 75, Notes = "Thread scope has no direct equivalent" },
            new() { SourceLifetime = "InScope", Framework = DIFrameworkType.Ninject, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 70, Notes = "Custom scope, needs manual review" },
            new() { SourceLifetime = "InNamedScope", Framework = DIFrameworkType.Ninject, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 70, Notes = "Named scope, needs manual review" }
        ];

        // StructureMap
        _mappings[DIFrameworkType.StructureMap] =
        [
            new() { SourceLifetime = "Singleton", Framework = DIFrameworkType.StructureMap, TargetLifetime = ServiceLifetime.Singleton, ConfidenceScore = 100 },
            new() { SourceLifetime = "ContainerScoped", Framework = DIFrameworkType.StructureMap, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 100 },
            new() { SourceLifetime = "AlwaysUnique", Framework = DIFrameworkType.StructureMap, TargetLifetime = ServiceLifetime.Transient, ConfidenceScore = 100 },
            new() { SourceLifetime = "Unique", Framework = DIFrameworkType.StructureMap, TargetLifetime = ServiceLifetime.Transient, ConfidenceScore = 100 },
            new() { SourceLifetime = "HttpContextScoped", Framework = DIFrameworkType.StructureMap, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 95, Notes = "HTTP context scope maps to scoped" },
            new() { SourceLifetime = "ThreadLocalStorage", Framework = DIFrameworkType.StructureMap, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 75, Notes = "Thread local storage" },
            new() { SourceLifetime = "Hybrid", Framework = DIFrameworkType.StructureMap, TargetLifetime = ServiceLifetime.Scoped, ConfidenceScore = 60, Notes = "Hybrid scope, needs manual review" }
        ];
    }
}
