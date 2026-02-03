using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.DependencyInjection;
using NetLift.Core.Models.DependencyInjection;

namespace NetLift.Transforms.DependencyInjection.Analyzers;

/// <summary>
/// Analyzes Autofac service registrations using Roslyn.
/// </summary>
public sealed partial class AutofacAnalyzer : IAutofacAnalyzer
{
    private readonly ILifetimeMapper _lifetimeMapper;

    /// <summary>
    /// Creates a new AutofacAnalyzer.
    /// </summary>
    public AutofacAnalyzer(ILifetimeMapper lifetimeMapper)
    {
        _lifetimeMapper = lifetimeMapper;
    }

    /// <inheritdoc />
    public DIFrameworkType SupportedFramework => DIFrameworkType.Autofac;

    /// <inheritdoc />
    public async Task<List<ServiceRegistrationInfo>> AnalyzeRegistrationsAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        var content = await File.ReadAllTextAsync(filePath);
        return await AnalyzeRegistrationsFromContentAsync(content, filePath);
    }

    /// <inheritdoc />
    public async Task<List<ServiceRegistrationInfo>> AnalyzeRegistrationsFromContentAsync(string content, string filePath)
    {
        var registrations = new List<ServiceRegistrationInfo>();

        if (!content.Contains("Autofac") && !content.Contains("ContainerBuilder"))
            return registrations;

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = await tree.GetRootAsync();

        // Find all invocation expressions
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var registration = TryParseRegistration(invocation, content, filePath);
            if (registration != null)
            {
                registrations.Add(registration);
            }
        }

        return registrations;
    }

    /// <inheritdoc />
    public async Task<LifetimeMapping> MapLifetimeAsync(string sourceLifetime)
    {
        return await Task.FromResult(_lifetimeMapper.MapLifetime(sourceLifetime, DIFrameworkType.Autofac));
    }

    /// <inheritdoc />
    public int CalculateConfidence(ServiceRegistrationInfo registration)
    {
        var confidence = 95;

        // Reduce confidence for complex scenarios
        if (registration.Method == RegistrationMethod.Factory)
        {
            confidence -= registration.Factory?.IsSimple == true ? 5 : 20;
        }

        if (registration.Method == RegistrationMethod.AssemblyScanning)
        {
            confidence -= 20;
        }

        if (!string.IsNullOrEmpty(registration.NamedKey))
        {
            confidence -= 5;
        }

        if (registration.PropertyInjection != null)
        {
            confidence -= registration.PropertyInjection.CanConvertToConstructor ? 15 : 30;
        }

        if (registration.Interceptor != null)
        {
            confidence -= 40;
        }

        return Math.Max(confidence, 0);
    }

    /// <inheritdoc />
    public async Task<List<ModuleInfo>> ParseModulesAsync(string projectPath)
    {
        var modules = new List<ModuleInfo>();
        var projectDir = Path.GetDirectoryName(projectPath);

        if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
            return modules;

        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);

                // Check if file contains an Autofac module
                if (!content.Contains(": Module") && !content.Contains(":Module"))
                    continue;

                var tree = CSharpSyntaxTree.ParseText(content);
                var root = await tree.GetRootAsync();

                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classDecl in classDeclarations)
                {
                    if (IsAutofacModule(classDecl))
                    {
                        var registrations = await ParseModuleAsync(file);
                        var moduleName = classDecl.Identifier.Text;

                        modules.Add(new ModuleInfo
                        {
                            ModuleName = moduleName,
                            ModuleTypeName = GetFullTypeName(classDecl),
                            FilePath = file,
                            Registrations = registrations,
                            Dependencies = ExtractModuleDependencies(content),
                            RegistrationOrder = modules.Count
                        });
                    }
                }
            }
            catch
            {
                // Skip files that can't be parsed
            }
        }

        return modules;
    }

    /// <inheritdoc />
    public async Task<List<ServiceRegistrationInfo>> ParseModuleAsync(string filePath)
    {
        return await AnalyzeRegistrationsAsync(filePath);
    }

    /// <inheritdoc />
    public List<ServiceRegistrationInfo> ParseContainerBuilder(string content)
    {
        var registrations = new List<ServiceRegistrationInfo>();

        if (!content.Contains("ContainerBuilder") && !content.Contains("RegisterType"))
            return registrations;

        // Parse using regex for common patterns
        registrations.AddRange(ParseRegisterTypePatterns(content));
        registrations.AddRange(ParseRegisterGenericPatterns(content));
        registrations.AddRange(ParseRegisterInstancePatterns(content));
        registrations.AddRange(ParseFactoryPatterns(content));

        return registrations;
    }

    private ServiceRegistrationInfo? TryParseRegistration(InvocationExpressionSyntax invocation, string content, string filePath)
    {
        var expressionText = invocation.ToString();

        // RegisterType<T>().As<I>()
        if (expressionText.Contains("RegisterType"))
        {
            return ParseRegisterTypeInvocation(invocation, content, filePath);
        }

        // RegisterGeneric
        if (expressionText.Contains("RegisterGeneric"))
        {
            return ParseRegisterGenericInvocation(invocation, content, filePath);
        }

        // RegisterInstance
        if (expressionText.Contains("RegisterInstance"))
        {
            return ParseRegisterInstanceInvocation(invocation, content, filePath);
        }

        // Register(c => ...)
        if (expressionText.Contains("Register(") && expressionText.Contains("=>"))
        {
            return ParseFactoryInvocation(invocation, content, filePath);
        }

        // RegisterAssemblyTypes
        if (expressionText.Contains("RegisterAssemblyTypes"))
        {
            return ParseAssemblyScanningInvocation(invocation, content, filePath);
        }

        return null;
    }

    private ServiceRegistrationInfo? ParseRegisterTypeInvocation(InvocationExpressionSyntax invocation, string content, string filePath)
    {
        var fullExpression = GetFullChainedExpression(invocation);
        var expressionText = fullExpression.ToString();

        var implType = ExtractGenericArgument(expressionText, "RegisterType");
        var serviceType = ExtractGenericArgument(expressionText, "As") ?? implType;
        var lifetime = ExtractLifetime(expressionText);
        var namedKey = ExtractNamedKey(expressionText);

        if (string.IsNullOrEmpty(implType))
            return null;

        var mapping = _lifetimeMapper.MapLifetime(lifetime, DIFrameworkType.Autofac);
        var lineNumber = GetLineNumber(content, invocation.SpanStart);

        var registration = new ServiceRegistrationInfo
        {
            ServiceType = serviceType ?? implType,
            ImplementationType = implType,
            Lifetime = mapping.TargetLifetime,
            Method = RegistrationMethod.Type,
            NamedKey = namedKey,
            SourceCode = expressionText,
            SourceFile = filePath,
            SourceLine = lineNumber,
            ConfidenceScore = mapping.ConfidenceScore,
            Notes = string.IsNullOrEmpty(mapping.Notes) ? [] : [mapping.Notes]
        };

        // Check for property injection
        if (expressionText.Contains("PropertiesAutowired"))
        {
            registration = registration with
            {
                PropertyInjection = new PropertyInjectionInfo
                {
                    TargetType = implType,
                    IsAutoWired = true,
                    CanConvertToConstructor = true,
                    ConfidenceScore = 70
                }
            };
        }

        // Check for interceptors
        if (expressionText.Contains("EnableInterfaceInterceptors") || expressionText.Contains("InterceptedBy"))
        {
            var interceptorType = ExtractInterceptorType(expressionText);
            registration = registration with
            {
                Interceptor = new InterceptorInfo
                {
                    InterceptorType = interceptorType ?? "Unknown",
                    Pattern = InterceptorPattern.AutofacInterceptor,
                    CanAutoMigrate = false,
                    MigrationApproach = "Use Scrutor Decorator or Castle.DynamicProxy",
                    ConfidenceScore = 40
                }
            };
        }

        return registration with { ConfidenceScore = CalculateConfidence(registration) };
    }

    private ServiceRegistrationInfo? ParseRegisterGenericInvocation(InvocationExpressionSyntax invocation, string content, string filePath)
    {
        var fullExpression = GetFullChainedExpression(invocation);
        var expressionText = fullExpression.ToString();

        // Extract typeof(...) arguments
        var implTypeMatch = TypeofRegex().Match(expressionText);
        var implType = implTypeMatch.Success ? implTypeMatch.Groups[1].Value : null;

        var serviceType = implType;
        var asMatch = AsTypeofRegex().Match(expressionText);
        if (asMatch.Success)
        {
            serviceType = asMatch.Groups[1].Value;
        }

        if (string.IsNullOrEmpty(implType))
            return null;

        var lifetime = ExtractLifetime(expressionText);
        var mapping = _lifetimeMapper.MapLifetime(lifetime, DIFrameworkType.Autofac);
        var lineNumber = GetLineNumber(content, invocation.SpanStart);

        return new ServiceRegistrationInfo
        {
            ServiceType = serviceType ?? implType,
            ImplementationType = implType,
            Lifetime = mapping.TargetLifetime,
            Method = RegistrationMethod.Generic,
            SourceCode = expressionText,
            SourceFile = filePath,
            SourceLine = lineNumber,
            ConfidenceScore = mapping.ConfidenceScore,
            Notes = string.IsNullOrEmpty(mapping.Notes) ? [] : [mapping.Notes]
        };
    }

    private ServiceRegistrationInfo? ParseRegisterInstanceInvocation(InvocationExpressionSyntax invocation, string content, string filePath)
    {
        var fullExpression = GetFullChainedExpression(invocation);
        var expressionText = fullExpression.ToString();
        var lineNumber = GetLineNumber(content, invocation.SpanStart);

        var serviceType = ExtractGenericArgument(expressionText, "As")
                          ?? ExtractGenericArgument(expressionText, "RegisterInstance")
                          ?? "object";

        return new ServiceRegistrationInfo
        {
            ServiceType = serviceType,
            ImplementationType = serviceType,
            Lifetime = ServiceLifetime.Singleton,
            Method = RegistrationMethod.Instance,
            SourceCode = expressionText,
            SourceFile = filePath,
            SourceLine = lineNumber,
            ConfidenceScore = 95,
            Notes = ["Instance registration - will be singleton"]
        };
    }

    private ServiceRegistrationInfo? ParseFactoryInvocation(InvocationExpressionSyntax invocation, string content, string filePath)
    {
        var fullExpression = GetFullChainedExpression(invocation);
        var expressionText = fullExpression.ToString();

        var serviceType = ExtractGenericArgument(expressionText, "As") ?? "object";
        var lifetime = ExtractLifetime(expressionText);
        var mapping = _lifetimeMapper.MapLifetime(lifetime, DIFrameworkType.Autofac);
        var lineNumber = GetLineNumber(content, invocation.SpanStart);

        // Extract factory lambda
        var lambdaMatch = LambdaRegex().Match(expressionText);
        var factoryExpression = lambdaMatch.Success ? lambdaMatch.Groups[1].Value : expressionText;

        // Detect dependencies in factory
        var dependencies = ResolveRegex().Matches(factoryExpression)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        var isSimple = !factoryExpression.Contains("if") &&
                       !factoryExpression.Contains("switch") &&
                       !factoryExpression.Contains("?");

        return new ServiceRegistrationInfo
        {
            ServiceType = serviceType,
            ImplementationType = serviceType,
            Lifetime = mapping.TargetLifetime,
            Method = RegistrationMethod.Factory,
            Factory = new FactoryRegistrationInfo
            {
                FactoryExpression = factoryExpression,
                Dependencies = dependencies,
                IsSimple = isSimple,
                ConfidenceScore = isSimple ? 85 : 65
            },
            SourceCode = expressionText,
            SourceFile = filePath,
            SourceLine = lineNumber,
            ConfidenceScore = isSimple ? 85 : 65,
            Notes = isSimple ? [] : ["Complex factory, needs manual review"]
        };
    }

    private ServiceRegistrationInfo? ParseAssemblyScanningInvocation(InvocationExpressionSyntax invocation, string content, string filePath)
    {
        var fullExpression = GetFullChainedExpression(invocation);
        var expressionText = fullExpression.ToString();
        var lifetime = ExtractLifetime(expressionText);
        var mapping = _lifetimeMapper.MapLifetime(lifetime, DIFrameworkType.Autofac);
        var lineNumber = GetLineNumber(content, invocation.SpanStart);

        return new ServiceRegistrationInfo
        {
            ServiceType = "*",
            ImplementationType = "Assembly Scanning",
            Lifetime = mapping.TargetLifetime,
            Method = RegistrationMethod.AssemblyScanning,
            SourceCode = expressionText,
            SourceFile = filePath,
            SourceLine = lineNumber,
            ConfidenceScore = 70,
            Notes = ["Assembly scanning requires Scrutor package"]
        };
    }

    private List<ServiceRegistrationInfo> ParseRegisterTypePatterns(string content)
    {
        var registrations = new List<ServiceRegistrationInfo>();
        var matches = RegisterTypeRegex().Matches(content);

        foreach (Match match in matches)
        {
            var implType = match.Groups[1].Value;
            var serviceType = match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value)
                ? match.Groups[2].Value
                : implType;

            registrations.Add(new ServiceRegistrationInfo
            {
                ServiceType = serviceType,
                ImplementationType = implType,
                Lifetime = ServiceLifetime.Transient,
                Method = RegistrationMethod.Type,
                SourceCode = match.Value,
                ConfidenceScore = 90
            });
        }

        return registrations;
    }

    private List<ServiceRegistrationInfo> ParseRegisterGenericPatterns(string content)
    {
        var registrations = new List<ServiceRegistrationInfo>();
        var matches = RegisterGenericRegex().Matches(content);

        foreach (Match match in matches)
        {
            registrations.Add(new ServiceRegistrationInfo
            {
                ServiceType = match.Groups[1].Value,
                ImplementationType = match.Groups[1].Value,
                Lifetime = ServiceLifetime.Transient,
                Method = RegistrationMethod.Generic,
                SourceCode = match.Value,
                ConfidenceScore = 90
            });
        }

        return registrations;
    }

    private List<ServiceRegistrationInfo> ParseRegisterInstancePatterns(string content)
    {
        var registrations = new List<ServiceRegistrationInfo>();
        var matches = RegisterInstanceRegex().Matches(content);

        foreach (Match match in matches)
        {
            registrations.Add(new ServiceRegistrationInfo
            {
                ServiceType = match.Groups[1].Value,
                ImplementationType = match.Groups[1].Value,
                Lifetime = ServiceLifetime.Singleton,
                Method = RegistrationMethod.Instance,
                SourceCode = match.Value,
                ConfidenceScore = 95
            });
        }

        return registrations;
    }

    private List<ServiceRegistrationInfo> ParseFactoryPatterns(string content)
    {
        // Complex pattern - rely on Roslyn parsing
        return [];
    }

    private static SyntaxNode GetFullChainedExpression(InvocationExpressionSyntax invocation)
    {
        SyntaxNode current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax or InvocationExpressionSyntax)
        {
            current = current.Parent;
        }
        return current;
    }

    private static string? ExtractGenericArgument(string expression, string methodName)
    {
        var pattern = $@"{methodName}<([^>]+)>";
        var match = Regex.Match(expression, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ExtractLifetime(string expression)
    {
        if (expression.Contains("SingleInstance"))
            return "SingleInstance";
        if (expression.Contains("InstancePerLifetimeScope"))
            return "InstancePerLifetimeScope";
        if (expression.Contains("InstancePerRequest"))
            return "InstancePerRequest";
        if (expression.Contains("InstancePerDependency"))
            return "InstancePerDependency";
        if (expression.Contains("InstancePerMatchingLifetimeScope"))
            return "InstancePerMatchingLifetimeScope";
        if (expression.Contains("ExternallyOwned"))
            return "ExternallyOwned";

        return "InstancePerDependency"; // Default
    }

    private static string? ExtractNamedKey(string expression)
    {
        var namedMatch = Regex.Match(expression, @"Named<[^>]+>\(""([^""]+)""\)");
        if (namedMatch.Success)
            return namedMatch.Groups[1].Value;

        var keyedMatch = Regex.Match(expression, @"Keyed<[^>]+>\(([^)]+)\)");
        if (keyedMatch.Success)
            return keyedMatch.Groups[1].Value;

        return null;
    }

    private static string? ExtractInterceptorType(string expression)
    {
        var match = Regex.Match(expression, @"InterceptedBy\(typeof\(([^)]+)\)\)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int GetLineNumber(string content, int position)
    {
        return content[..position].Count(c => c == '\n') + 1;
    }

    private static bool IsAutofacModule(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList == null)
            return false;

        return classDecl.BaseList.Types.Any(t =>
            t.ToString().Contains("Module") ||
            t.ToString().Contains("Autofac.Module"));
    }

    private static string GetFullTypeName(ClassDeclarationSyntax classDecl)
    {
        var namespaceName = classDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
                           ?? classDecl.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
                           ?? "";

        return string.IsNullOrEmpty(namespaceName)
            ? classDecl.Identifier.Text
            : $"{namespaceName}.{classDecl.Identifier.Text}";
    }

    private static IReadOnlyList<string> ExtractModuleDependencies(string content)
    {
        var dependencies = new List<string>();
        var matches = RegisterModuleRegex().Matches(content);

        foreach (Match match in matches)
        {
            dependencies.Add(match.Groups[1].Value);
        }

        return dependencies;
    }

    [GeneratedRegex(@"RegisterType<([^>]+)>.*?(?:\.As<([^>]+)>)?", RegexOptions.Compiled)]
    private static partial Regex RegisterTypeRegex();

    [GeneratedRegex(@"RegisterGeneric\(typeof\(([^)]+)\)\)", RegexOptions.Compiled)]
    private static partial Regex RegisterGenericRegex();

    [GeneratedRegex(@"RegisterInstance<([^>]+)>", RegexOptions.Compiled)]
    private static partial Regex RegisterInstanceRegex();

    [GeneratedRegex(@"typeof\(([^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex TypeofRegex();

    [GeneratedRegex(@"\.As\(typeof\(([^)]+)\)\)", RegexOptions.Compiled)]
    private static partial Regex AsTypeofRegex();

    [GeneratedRegex(@"Register\([^)]*=>\s*(.+)\)", RegexOptions.Compiled)]
    private static partial Regex LambdaRegex();

    [GeneratedRegex(@"\.Resolve<([^>]+)>\(\)", RegexOptions.Compiled)]
    private static partial Regex ResolveRegex();

    [GeneratedRegex(@"RegisterModule<([^>]+)>", RegexOptions.Compiled)]
    private static partial Regex RegisterModuleRegex();
}
