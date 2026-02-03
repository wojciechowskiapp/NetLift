using NetLift.Core.Interfaces.DependencyInjection;
using NetLift.Core.Models;
using NetLift.Core.Models.DependencyInjection;

namespace NetLift.Transforms.DependencyInjection.Detectors;

/// <summary>
/// Detects which DI framework is being used in a project or solution.
/// </summary>
public sealed class DIContainerDetector : IDIContainerDetector
{
    private static readonly Dictionary<string, DIFrameworkType> PackageToFramework = new(StringComparer.OrdinalIgnoreCase)
    {
        // Autofac
        ["Autofac"] = DIFrameworkType.Autofac,
        ["Autofac.Mvc5"] = DIFrameworkType.Autofac,
        ["Autofac.WebApi2"] = DIFrameworkType.Autofac,
        ["Autofac.Integration.Mvc"] = DIFrameworkType.Autofac,
        ["Autofac.Integration.WebApi"] = DIFrameworkType.Autofac,
        ["Autofac.Integration.Owin"] = DIFrameworkType.Autofac,

        // Unity
        ["Unity"] = DIFrameworkType.Unity,
        ["Unity.Container"] = DIFrameworkType.Unity,
        ["Unity.Mvc5"] = DIFrameworkType.Unity,
        ["Unity.AspNet.WebApi"] = DIFrameworkType.Unity,
        ["Unity.WebAPI"] = DIFrameworkType.Unity,
        ["Unity.Interception"] = DIFrameworkType.Unity,

        // Ninject
        ["Ninject"] = DIFrameworkType.Ninject,
        ["Ninject.Web.Mvc"] = DIFrameworkType.Ninject,
        ["Ninject.Web.WebApi"] = DIFrameworkType.Ninject,
        ["Ninject.MVC5"] = DIFrameworkType.Ninject,
        ["Ninject.Web.Common"] = DIFrameworkType.Ninject,
        ["Ninject.Web.Common.WebHost"] = DIFrameworkType.Ninject,

        // StructureMap
        ["StructureMap"] = DIFrameworkType.StructureMap,
        ["StructureMap.MVC5"] = DIFrameworkType.StructureMap,
        ["StructureMap.WebApi2"] = DIFrameworkType.StructureMap,
        ["StructureMap.Web"] = DIFrameworkType.StructureMap
    };

    private static readonly Dictionary<DIFrameworkType, string[]> FrameworkFilePatterns = new()
    {
        [DIFrameworkType.Autofac] = ["*Module.cs", "AutofacConfig.cs", "ContainerConfig.cs"],
        [DIFrameworkType.Unity] = ["UnityConfig.cs", "UnityWebApiActivator.cs"],
        [DIFrameworkType.Ninject] = ["NinjectWebCommon.cs", "NinjectConfig.cs", "*NinjectModule.cs"],
        [DIFrameworkType.StructureMap] = ["StructuremapMvc.cs", "*Registry.cs", "IoC.cs"]
    };

    /// <inheritdoc />
    public async Task<DIContainerInfo> DetectAsync(SolutionInfo solution)
    {
        var detectedFrameworks = new HashSet<DIFrameworkType>();
        var configFiles = new List<string>();
        var detectedPatterns = new List<string>();

        foreach (var project in solution.Projects)
        {
            var projectFrameworks = await GetUsedFrameworksAsync(project.AbsolutePath);
            foreach (var framework in projectFrameworks)
            {
                detectedFrameworks.Add(framework);
                var files = await FindConfigurationFilesAsync(project.AbsolutePath, framework);
                configFiles.AddRange(files);
            }
        }

        var primaryFramework = detectedFrameworks.Count switch
        {
            0 => DIFrameworkType.Unknown,
            1 => detectedFrameworks.First(),
            _ => DIFrameworkType.Mixed
        };

        var hasPropertyInjection = false;
        var hasInterceptors = false;
        var hasAssemblyScanning = false;

        // Scan config files for patterns
        foreach (var file in configFiles)
        {
            if (File.Exists(file))
            {
                var content = await File.ReadAllTextAsync(file);

                if (content.Contains("PropertiesAutowired") ||
                    content.Contains("[Dependency]") ||
                    content.Contains("[Inject]"))
                {
                    hasPropertyInjection = true;
                    detectedPatterns.Add("Property Injection");
                }

                if (content.Contains("EnableInterfaceInterceptors") ||
                    content.Contains("InterceptedBy") ||
                    content.Contains("IInterceptionBehavior"))
                {
                    hasInterceptors = true;
                    detectedPatterns.Add("Interceptors");
                }

                if (content.Contains("RegisterAssemblyTypes") ||
                    content.Contains("Scan(") ||
                    content.Contains("FromAssembly"))
                {
                    hasAssemblyScanning = true;
                    detectedPatterns.Add("Assembly Scanning");
                }
            }
        }

        var complexity = DetermineComplexity(hasPropertyInjection, hasInterceptors, hasAssemblyScanning);
        var confidence = CalculateConfidence(primaryFramework, complexity);

        return new DIContainerInfo
        {
            Framework = primaryFramework,
            ConfigurationFiles = configFiles,
            Registrations = [],
            Modules = [],
            Complexity = complexity,
            ConfidenceScore = confidence,
            DetectedPatterns = detectedPatterns.Distinct().ToList(),
            HasPropertyInjection = hasPropertyInjection,
            HasInterceptors = hasInterceptors,
            HasAssemblyScanning = hasAssemblyScanning,
            RequiresScrutor = hasAssemblyScanning
        };
    }

    /// <inheritdoc />
    public async Task<List<DIFrameworkType>> GetUsedFrameworksAsync(string projectPath, IEnumerable<PackageReference>? packages = null)
    {
        var frameworks = new HashSet<DIFrameworkType>();

        // Check package references if provided
        if (packages != null)
        {
            var packageFramework = DetectFromPackages(packages);
            if (packageFramework != DIFrameworkType.Unknown)
            {
                frameworks.Add(packageFramework);
            }
        }

        // Check for code patterns
        var projectDir = Path.GetDirectoryName(projectPath);
        if (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir))
        {
            var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);
            foreach (var file in csFiles.Take(100)) // Limit for performance
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);

                    if (content.Contains("using Autofac;") || content.Contains("ContainerBuilder"))
                        frameworks.Add(DIFrameworkType.Autofac);

                    if (content.Contains("using Unity;") || content.Contains("IUnityContainer"))
                        frameworks.Add(DIFrameworkType.Unity);

                    if (content.Contains("using Ninject;") || content.Contains("IKernel kernel"))
                        frameworks.Add(DIFrameworkType.Ninject);

                    if (content.Contains("using StructureMap;") || (content.Contains("For<") && content.Contains("Use<")))
                        frameworks.Add(DIFrameworkType.StructureMap);
                }
                catch
                {
                    // Skip files that can't be read
                }
            }
        }

        return frameworks.ToList();
    }

    /// <inheritdoc />
    public async Task<List<string>> FindConfigurationFilesAsync(string projectPath, DIFrameworkType framework)
    {
        var files = new List<string>();
        var projectDir = Path.GetDirectoryName(projectPath);

        if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
            return files;

        if (!FrameworkFilePatterns.TryGetValue(framework, out var patterns))
            return files;

        foreach (var pattern in patterns)
        {
            try
            {
                var matchingFiles = Directory.GetFiles(projectDir, pattern, SearchOption.AllDirectories);
                files.AddRange(matchingFiles);
            }
            catch
            {
                // Skip patterns that fail
            }
        }

        // Also scan for framework-specific code patterns in common locations
        var commonLocations = new[] { "App_Start", "Infrastructure", "Configuration", "DI", "IoC" };
        foreach (var location in commonLocations)
        {
            var path = Path.Combine(projectDir, location);
            if (Directory.Exists(path))
            {
                try
                {
                    var csFiles = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
                    foreach (var file in csFiles)
                    {
                        var content = await File.ReadAllTextAsync(file);
                        if (ContainsFrameworkCode(content, framework))
                        {
                            if (!files.Contains(file))
                                files.Add(file);
                        }
                    }
                }
                catch
                {
                    // Skip directories that can't be read
                }
            }
        }

        return files.Distinct().ToList();
    }

    /// <inheritdoc />
    public DIFrameworkType DetectFromPackages(IEnumerable<PackageReference> packages)
    {
        var detectedFrameworks = new HashSet<DIFrameworkType>();

        foreach (var package in packages)
        {
            if (PackageToFramework.TryGetValue(package.Id, out var framework))
            {
                detectedFrameworks.Add(framework);
            }
        }

        return detectedFrameworks.Count switch
        {
            0 => DIFrameworkType.Unknown,
            1 => detectedFrameworks.First(),
            _ => DIFrameworkType.Mixed
        };
    }

    private static bool ContainsFrameworkCode(string content, DIFrameworkType framework)
    {
        return framework switch
        {
            DIFrameworkType.Autofac => content.Contains("ContainerBuilder") ||
                                       content.Contains("RegisterType") ||
                                       content.Contains(": Module"),
            DIFrameworkType.Unity => content.Contains("IUnityContainer") ||
                                     content.Contains("RegisterType"),
            DIFrameworkType.Ninject => content.Contains("IKernel") ||
                                       content.Contains("Bind<") ||
                                       content.Contains(": NinjectModule"),
            DIFrameworkType.StructureMap => (content.Contains("For<") && content.Contains("Use<")) ||
                                            content.Contains(": Registry"),
            _ => false
        };
    }

    private static DIComplexityLevel DetermineComplexity(bool hasPropertyInjection, bool hasInterceptors, bool hasAssemblyScanning)
    {
        var complexityScore = 0;
        if (hasPropertyInjection) complexityScore++;
        if (hasInterceptors) complexityScore += 2;
        if (hasAssemblyScanning) complexityScore++;

        return complexityScore switch
        {
            0 => DIComplexityLevel.Simple,
            1 => DIComplexityLevel.Moderate,
            2 => DIComplexityLevel.Complex,
            _ => DIComplexityLevel.VeryComplex
        };
    }

    private static int CalculateConfidence(DIFrameworkType framework, DIComplexityLevel complexity)
    {
        if (framework == DIFrameworkType.Unknown)
            return 0;

        var baseConfidence = framework == DIFrameworkType.Mixed ? 60 : 90;

        return complexity switch
        {
            DIComplexityLevel.Simple => baseConfidence,
            DIComplexityLevel.Moderate => baseConfidence - 5,
            DIComplexityLevel.Complex => baseConfidence - 15,
            DIComplexityLevel.VeryComplex => baseConfidence - 25,
            _ => baseConfidence
        };
    }
}
