using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.DependencyInjection;
using NetLift.Core.Models.DependencyInjection;

namespace NetLift.Transforms.DependencyInjection.Analyzers;

/// <summary>
/// Analyzes Autofac service registrations using Roslyn.
/// </summary>
public sealed class AutofacAnalyzer : IAutofacAnalyzer
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
                            Dependencies = ExtractModuleDependencies(root),
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

        var tree = CSharpSyntaxTree.ParseText(content);
        var root = tree.GetRoot();

        // Find all invocation expressions
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var registration = TryParseRegistration(invocation, content, "");
            if (registration != null)
            {
                registrations.Add(registration);
            }
        }

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

        // Extract generic type from RegisterType<T>
        var implType = ExtractGenericTypeFromMethod(fullExpression, "RegisterType");
        var serviceType = ExtractGenericTypeFromMethod(fullExpression, "As") ?? implType;
        var lifetime = ExtractLifetimeFromExpression(fullExpression);
        var namedKey = ExtractNamedKeyFromExpression(fullExpression);

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
        if (HasMethodInChain(fullExpression, "PropertiesAutowired"))
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
        if (HasMethodInChain(fullExpression, "EnableInterfaceInterceptors") ||
            HasMethodInChain(fullExpression, "InterceptedBy"))
        {
            var interceptorType = ExtractInterceptorTypeFromExpression(fullExpression);
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

        // Extract typeof(...) arguments from RegisterGeneric
        var implType = ExtractTypeofArgument(invocation);
        if (string.IsNullOrEmpty(implType))
            return null;

        // Check if there's an As(typeof(...)) call
        var serviceType = ExtractTypeofFromAsMethod(fullExpression) ?? implType;

        var lifetime = ExtractLifetimeFromExpression(fullExpression);
        var mapping = _lifetimeMapper.MapLifetime(lifetime, DIFrameworkType.Autofac);
        var lineNumber = GetLineNumber(content, invocation.SpanStart);

        return new ServiceRegistrationInfo
        {
            ServiceType = serviceType,
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

        var serviceType = ExtractGenericTypeFromMethod(fullExpression, "As")
                          ?? ExtractGenericTypeFromMethod(fullExpression, "RegisterInstance")
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

        var serviceType = ExtractGenericTypeFromMethod(fullExpression, "As") ?? "object";
        var lifetime = ExtractLifetimeFromExpression(fullExpression);
        var mapping = _lifetimeMapper.MapLifetime(lifetime, DIFrameworkType.Autofac);
        var lineNumber = GetLineNumber(content, invocation.SpanStart);

        // Extract factory lambda using Roslyn
        var lambda = ExtractLambdaExpression(invocation);
        var factoryExpression = lambda?.ToString() ?? expressionText;

        // Detect dependencies in factory by finding Resolve<T>() calls
        var dependencies = ExtractResolveDependencies(lambda);

        var isSimple = lambda != null && IsSimpleLambda(lambda);

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
        var lifetime = ExtractLifetimeFromExpression(fullExpression);
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

    private static SyntaxNode GetFullChainedExpression(InvocationExpressionSyntax invocation)
    {
        SyntaxNode current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax or InvocationExpressionSyntax)
        {
            current = current.Parent;
        }
        return current;
    }

    /// <summary>
    /// Extracts generic type argument from a method call like RegisterType&lt;MyService&gt;() or As&lt;IService&gt;()
    /// </summary>
    private static string? ExtractGenericTypeFromMethod(SyntaxNode expression, string methodName)
    {
        var invocations = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax genericName &&
                genericName.Identifier.Text == methodName)
            {
                var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                return typeArg?.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts typeof argument from expressions like RegisterGeneric(typeof(MyService))
    /// </summary>
    private static string? ExtractTypeofArgument(InvocationExpressionSyntax invocation)
    {
        var typeofExpr = invocation.ArgumentList?.Arguments
            .Select(a => a.Expression)
            .OfType<TypeOfExpressionSyntax>()
            .FirstOrDefault();

        return typeofExpr?.Type.ToString();
    }

    /// <summary>
    /// Extracts typeof from As(typeof(...)) method calls
    /// </summary>
    private static string? ExtractTypeofFromAsMethod(SyntaxNode expression)
    {
        var invocations = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "As")
            {
                var typeofExpr = invocation.ArgumentList?.Arguments
                    .Select(a => a.Expression)
                    .OfType<TypeOfExpressionSyntax>()
                    .FirstOrDefault();

                if (typeofExpr != null)
                    return typeofExpr.Type.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts lifetime from method chain like .SingleInstance() or .InstancePerLifetimeScope()
    /// </summary>
    private static string ExtractLifetimeFromExpression(SyntaxNode expression)
    {
        var invocations = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.Text;
                if (methodName is "SingleInstance" or "InstancePerLifetimeScope" or "InstancePerRequest" or
                    "InstancePerDependency" or "InstancePerMatchingLifetimeScope" or "ExternallyOwned")
                {
                    return methodName;
                }
            }
        }

        return "InstancePerDependency"; // Default
    }

    /// <summary>
    /// Extracts named/keyed key from Named&lt;T&gt;("key") or Keyed&lt;T&gt;(key)
    /// </summary>
    private static string? ExtractNamedKeyFromExpression(SyntaxNode expression)
    {
        var invocations = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax genericName)
            {
                var methodName = genericName.Identifier.Text;
                if (methodName is "Named" or "Keyed")
                {
                    var arg = invocation.ArgumentList?.Arguments.FirstOrDefault();
                    if (arg?.Expression is LiteralExpressionSyntax literal)
                    {
                        return literal.Token.ValueText;
                    }
                    return arg?.Expression.ToString();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts interceptor type from InterceptedBy(typeof(MyInterceptor))
    /// </summary>
    private static string? ExtractInterceptorTypeFromExpression(SyntaxNode expression)
    {
        var invocations = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "InterceptedBy")
            {
                var typeofExpr = invocation.ArgumentList?.Arguments
                    .Select(a => a.Expression)
                    .OfType<TypeOfExpressionSyntax>()
                    .FirstOrDefault();

                if (typeofExpr != null)
                    return typeofExpr.Type.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a method exists in the call chain
    /// </summary>
    private static bool HasMethodInChain(SyntaxNode expression, string methodName)
    {
        var invocations = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();

        return invocations.Any(invocation =>
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var name = memberAccess.Name is GenericNameSyntax genericName
                    ? genericName.Identifier.Text
                    : memberAccess.Name.Identifier.Text;
                return name == methodName;
            }
            return false;
        });
    }

    /// <summary>
    /// Extracts lambda expression from Register(c => ...) calls
    /// </summary>
    private static LambdaExpressionSyntax? ExtractLambdaExpression(InvocationExpressionSyntax invocation)
    {
        return invocation.ArgumentList?.Arguments
            .Select(a => a.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();
    }

    /// <summary>
    /// Extracts dependencies from lambda by finding Resolve&lt;T&gt;() calls
    /// </summary>
    private static List<string> ExtractResolveDependencies(LambdaExpressionSyntax? lambda)
    {
        if (lambda == null)
            return [];

        var dependencies = new List<string>();
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax genericName &&
                genericName.Identifier.Text == "Resolve")
            {
                var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                if (typeArg != null)
                {
                    dependencies.Add(typeArg.ToString());
                }
            }
        }

        return dependencies.Distinct().ToList();
    }

    /// <summary>
    /// Checks if lambda is simple (no conditionals)
    /// </summary>
    private static bool IsSimpleLambda(LambdaExpressionSyntax lambda)
    {
        var descendants = lambda.DescendantNodes();
        return !descendants.Any(n => n is IfStatementSyntax or SwitchStatementSyntax or ConditionalExpressionSyntax);
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

    private static IReadOnlyList<string> ExtractModuleDependencies(SyntaxNode root)
    {
        var dependencies = new List<string>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax genericName &&
                genericName.Identifier.Text == "RegisterModule")
            {
                var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                if (typeArg != null)
                {
                    dependencies.Add(typeArg.ToString());
                }
            }
        }

        return dependencies;
    }
}
