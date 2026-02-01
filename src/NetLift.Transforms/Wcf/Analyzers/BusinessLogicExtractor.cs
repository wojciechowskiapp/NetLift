using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Wcf;

namespace NetLift.Transforms.Wcf.Analyzers;

/// <summary>
/// Extracts business logic from WCF service implementations using Roslyn.
/// Converts concrete dependencies to DI, synchronous methods to async, and generates clean service layer code.
/// </summary>
public sealed class BusinessLogicExtractor : IBusinessLogicExtractor
{
    private readonly List<string> _diagnostics = new();
    private int _confidenceScore = 100;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Diagnostics => _diagnostics.AsReadOnly();

    /// <inheritdoc />
    public int ConfidenceScore => _confidenceScore;

    /// <inheritdoc />
    public ExtractedServiceInfo Extract(string sourceCode, WcfServiceContract contract)
    {
        _diagnostics.Clear();
        _confidenceScore = 100;

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be empty", nameof(sourceCode));
        }

        if (contract == null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            // Create compilation for semantic analysis
            var compilation = CSharpCompilation.Create("WcfExtraction")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree);

            var model = compilation.GetSemanticModel(tree);

            // Find the class implementing the service contract
            var implementationClass = FindImplementationClass(root, contract, model);
            if (implementationClass == null)
            {
                _confidenceScore = 0;
                throw new InvalidOperationException(
                    $"Could not find class implementing service contract '{contract.InterfaceName}'");
            }

            var classSymbol = model.GetDeclaredSymbol(implementationClass);
            if (classSymbol == null)
            {
                _confidenceScore = 0;
                throw new InvalidOperationException("Could not get semantic model for implementation class");
            }

            // Extract namespace
            var namespaceName = GetNamespace(implementationClass);

            // Extract dependencies from constructor and field initializations
            var dependencies = ExtractDependencies(implementationClass, model);

            // Extract methods and convert to async
            var methods = ExtractMethods(implementationClass, contract, model);

            // Generate interface code
            var interfaceCode = GenerateInterfaceCode(contract, namespaceName, methods);

            // Generate implementation code
            var implementationCode = GenerateImplementationCode(
                contract,
                namespaceName,
                classSymbol.Name.Replace("Service", "").TrimEnd('s') + "Service",
                dependencies,
                methods,
                implementationClass,
                model);

            var warnings = new List<string>();
            if (methods.Any(m => m.HasTransactionScope))
            {
                warnings.Add("TransactionScope usage detected - requires manual review for distributed transaction handling");
                _confidenceScore = Math.Min(_confidenceScore, 75);
            }

            if (methods.Any(m => m.HasFaultException))
            {
                warnings.Add("FaultException usage detected - should be replaced with custom domain exceptions");
                _confidenceScore = Math.Min(_confidenceScore, 85);
            }

            return new ExtractedServiceInfo
            {
                InterfaceName = "I" + classSymbol.Name.Replace("Service", "").TrimEnd('s') + "Service",
                ClassName = classSymbol.Name.Replace("Service", "").TrimEnd('s') + "Service",
                Namespace = namespaceName,
                InterfaceCode = interfaceCode,
                ImplementationCode = implementationCode,
                Dependencies = dependencies,
                Methods = methods,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"Error extracting business logic: {ex.Message}");
            _confidenceScore = 0;
            throw;
        }
    }

    /// <summary>
    /// Finds the class implementing the service contract interface.
    /// </summary>
    private ClassDeclarationSyntax? FindImplementationClass(
        SyntaxNode root,
        WcfServiceContract contract,
        SemanticModel model)
    {
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classes)
        {
            // First check via syntax (works even without semantic model)
            if (classDecl.BaseList != null)
            {
                var baseTypes = classDecl.BaseList.Types.Select(t => t.Type.ToString());
                if (baseTypes.Any(bt =>
                    bt == contract.InterfaceName ||
                    bt.EndsWith("." + contract.InterfaceName)))
                {
                    return classDecl;
                }
            }

            // Also check via semantic model if available
            var symbol = model.GetDeclaredSymbol(classDecl);
            if (symbol != null)
            {
                var implementsContract = symbol.AllInterfaces.Any(i =>
                    i.Name == contract.InterfaceName ||
                    i.ToDisplayString() == contract.FullyQualifiedName);

                if (implementsContract)
                {
                    return classDecl;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the namespace from a class declaration.
    /// </summary>
    private static string GetNamespace(ClassDeclarationSyntax classDecl)
    {
        // Try file-scoped namespace first
        var fileScopedNamespace = classDecl.Ancestors()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (fileScopedNamespace != null)
        {
            return fileScopedNamespace.Name.ToString();
        }

        // Try traditional namespace
        var namespaceDecl = classDecl.Ancestors()
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return namespaceDecl?.Name.ToString() ?? "DefaultNamespace";
    }

    /// <summary>
    /// Extracts dependencies from constructor parameters and field initializations.
    /// </summary>
    private List<ExtractedDependency> ExtractDependencies(
        ClassDeclarationSyntax classDecl,
        SemanticModel model)
    {
        var dependencies = new List<ExtractedDependency>();
        var seenTypes = new HashSet<string>();

        // Extract from constructor parameters
        var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();
        foreach (var ctor in constructors)
        {
            foreach (var param in ctor.ParameterList.Parameters)
            {
                var paramSymbol = model.GetDeclaredSymbol(param);
                if (paramSymbol?.Type == null)
                {
                    continue;
                }

                var typeName = paramSymbol.Type.Name;
                if (seenTypes.Contains(typeName))
                {
                    continue;
                }

                seenTypes.Add(typeName);

                var isLogger = IsLoggerType(paramSymbol.Type);
                var interfaceType = GetInterfaceType(paramSymbol.Type);

                dependencies.Add(new ExtractedDependency
                {
                    TypeName = typeName,
                    ParameterName = ToCamelCase(param.Identifier.Text),
                    InterfaceType = interfaceType,
                    IsLogger = isLogger
                });
            }
        }

        // Extract from field initializations (e.g., private readonly IRepo _repo = new Repo())
        var fields = classDecl.Members.OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                // Check if field is initialized with 'new' expression
                if (variable.Initializer?.Value is ObjectCreationExpressionSyntax objectCreation)
                {
                    var typeInfo = model.GetTypeInfo(objectCreation);
                    if (typeInfo.Type == null)
                    {
                        continue;
                    }

                    var typeName = typeInfo.Type.Name;
                    if (seenTypes.Contains(typeName))
                    {
                        continue;
                    }

                    seenTypes.Add(typeName);

                    var isLogger = IsLoggerType(typeInfo.Type);
                    var interfaceType = GetInterfaceType(typeInfo.Type);

                    dependencies.Add(new ExtractedDependency
                    {
                        TypeName = typeName,
                        ParameterName = ToCamelCase(variable.Identifier.Text.TrimStart('_')),
                        InterfaceType = interfaceType,
                        IsLogger = isLogger
                    });

                    _diagnostics.Add($"Detected field initialization '{typeName}' - will be converted to DI");
                }
            }
        }

        // Scan method bodies for 'new' instantiations of dependencies
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var objectCreations = method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
            foreach (var objectCreation in objectCreations)
            {
                var typeInfo = model.GetTypeInfo(objectCreation);
                if (typeInfo.Type == null)
                {
                    continue;
                }

                var typeName = typeInfo.Type.Name;

                // Skip framework types and primitives
                if (IsFrameworkType(typeInfo.Type) || seenTypes.Contains(typeName))
                {
                    continue;
                }

                // Only extract types that look like repositories, services, or data access
                if (!LooksLikeDependency(typeName))
                {
                    continue;
                }

                seenTypes.Add(typeName);

                var isLogger = IsLoggerType(typeInfo.Type);
                var interfaceType = GetInterfaceType(typeInfo.Type);

                dependencies.Add(new ExtractedDependency
                {
                    TypeName = typeName,
                    ParameterName = ToCamelCase(typeName),
                    InterfaceType = interfaceType,
                    IsLogger = isLogger
                });

                _diagnostics.Add($"Detected inline instantiation 'new {typeName}()' - will be converted to DI");
                _confidenceScore = Math.Min(_confidenceScore, 85);
            }
        }

        return dependencies;
    }

    /// <summary>
    /// Extracts methods and converts them to async signatures.
    /// </summary>
    private List<ExtractedMethod> ExtractMethods(
        ClassDeclarationSyntax classDecl,
        WcfServiceContract contract,
        SemanticModel model)
    {
        var extractedMethods = new List<ExtractedMethod>();

        // Get public methods that match contract operations
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));

        foreach (var method in methods)
        {
            var methodSymbol = model.GetDeclaredSymbol(method);
            if (methodSymbol == null)
            {
                continue;
            }

            // Check if method is part of the contract
            var contractOp = contract.Operations.FirstOrDefault(op => op.Name == methodSymbol.Name);
            if (contractOp == null)
            {
                continue;
            }

            var returnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var asyncReturnType = ConvertToAsyncReturnType(returnType);
            var asyncName = methodSymbol.Name.EndsWith("Async") ? methodSymbol.Name : methodSymbol.Name + "Async";

            // Extract parameters
            var parameters = methodSymbol.Parameters.Select(p => new MethodParameter
            {
                Name = p.Name,
                Type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            }).ToList();

            // Extract XML documentation
            var documentation = GetXmlDocumentation(method);

            // Detect TransactionScope usage
            var hasTransactionScope = method.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Any(oc => model.GetTypeInfo(oc).Type?.Name == "TransactionScope");

            // Detect FaultException usage
            var hasFaultException = method.DescendantNodes()
                .OfType<ThrowStatementSyntax>()
                .Any(t => t.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                    .Any(oc => model.GetTypeInfo(oc).Type?.Name == "FaultException"));

            extractedMethods.Add(new ExtractedMethod
            {
                Name = methodSymbol.Name,
                AsyncName = asyncName,
                ReturnType = returnType,
                AsyncReturnType = asyncReturnType,
                Documentation = documentation,
                Parameters = parameters,
                HasTransactionScope = hasTransactionScope,
                HasFaultException = hasFaultException
            });
        }

        return extractedMethods;
    }

    /// <summary>
    /// Generates the interface code with async methods.
    /// </summary>
    private string GenerateInterfaceCode(
        WcfServiceContract contract,
        string namespaceName,
        List<ExtractedMethod> methods)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Service interface extracted from WCF service contract '{contract.InterfaceName}'.");
        sb.AppendLine("/// </summary>");

        var interfaceName = "I" + contract.InterfaceName.TrimStart('I');
        sb.AppendLine($"public interface {interfaceName}");
        sb.AppendLine("{");

        foreach (var method in methods)
        {
            if (!string.IsNullOrWhiteSpace(method.Documentation))
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// {method.Documentation}");
                sb.AppendLine("    /// </summary>");
            }

            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            if (!string.IsNullOrEmpty(parameters))
            {
                parameters += ", ";
            }
            parameters += "CancellationToken cancellationToken = default";

            sb.AppendLine($"    {method.AsyncReturnType} {method.AsyncName}({parameters});");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the implementation code with DI constructor and async methods.
    /// </summary>
    private string GenerateImplementationCode(
        WcfServiceContract contract,
        string namespaceName,
        string className,
        List<ExtractedDependency> dependencies,
        List<ExtractedMethod> methods,
        ClassDeclarationSyntax originalClass,
        SemanticModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");

        // Add logger namespace if needed
        if (dependencies.Any(d => d.IsLogger))
        {
            sb.AppendLine("using Microsoft.Extensions.Logging;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Service implementation extracted from WCF service '{contract.InterfaceName}'.");
        sb.AppendLine("/// </summary>");

        var interfaceName = "I" + contract.InterfaceName.TrimStart('I');
        sb.AppendLine($"public sealed class {className} : {interfaceName}");
        sb.AppendLine("{");

        // Generate fields for dependencies
        foreach (var dep in dependencies)
        {
            var fieldName = "_" + dep.ParameterName;
            sb.AppendLine($"    private readonly {dep.InterfaceType} {fieldName};");
        }

        if (dependencies.Any())
        {
            sb.AppendLine();
        }

        // Generate constructor
        sb.AppendLine($"    public {className}(");
        var ctorParams = dependencies.Select((d, i) =>
        {
            var comma = i < dependencies.Count - 1 ? "," : "";
            return $"        {d.InterfaceType} {d.ParameterName}{comma}";
        });
        sb.AppendLine(string.Join(Environment.NewLine, ctorParams));
        sb.AppendLine("    )");
        sb.AppendLine("    {");

        foreach (var dep in dependencies)
        {
            var fieldName = "_" + dep.ParameterName;
            sb.AppendLine($"        {fieldName} = {dep.ParameterName} ?? throw new ArgumentNullException(nameof({dep.ParameterName}));");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate methods
        foreach (var method in methods)
        {
            if (!string.IsNullOrWhiteSpace(method.Documentation))
            {
                sb.AppendLine("    /// <inheritdoc />");
            }

            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            if (!string.IsNullOrEmpty(parameters))
            {
                parameters += ", ";
            }
            parameters += "CancellationToken cancellationToken = default";

            sb.AppendLine($"    public {method.AsyncReturnType} {method.AsyncName}({parameters})");
            sb.AppendLine("    {");

            if (method.HasTransactionScope)
            {
                sb.AppendLine("        // TODO: TransactionScope detected - review distributed transaction handling");
                sb.AppendLine("        // Consider using ambient transactions or sagas for distributed scenarios");
            }

            if (method.HasFaultException)
            {
                sb.AppendLine("        // TODO: FaultException detected - replace with custom domain exceptions");
            }

            sb.AppendLine("        throw new NotImplementedException(\"TODO: Migrate business logic from WCF implementation\");");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts XML documentation from a method.
    /// </summary>
    private static string? GetXmlDocumentation(MethodDeclarationSyntax method)
    {
        var trivia = method.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                       t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .FirstOrDefault();

        if (trivia == default)
        {
            return null;
        }

        var structure = trivia.GetStructure();
        if (structure == null)
        {
            return null;
        }

        var summaryElement = structure.DescendantNodes()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag?.Name?.ToString() == "summary");

        if (summaryElement != null)
        {
            var content = summaryElement.Content.ToString().Trim();
            return string.Join(" ", content.Split(new[] { '\r', '\n', ' ' },
                StringSplitOptions.RemoveEmptyEntries));
        }

        return null;
    }

    /// <summary>
    /// Converts a return type to its async equivalent.
    /// </summary>
    private static string ConvertToAsyncReturnType(string returnType)
    {
        if (returnType == "void")
        {
            return "Task";
        }

        if (returnType.StartsWith("Task<") || returnType == "Task")
        {
            return returnType;
        }

        return $"Task<{returnType}>";
    }

    /// <summary>
    /// Gets the interface type for a given type (either existing interface or generated name).
    /// </summary>
    private static string GetInterfaceType(ITypeSymbol type)
    {
        // If it's already an interface, use it
        if (type.TypeKind == TypeKind.Interface)
        {
            return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        // Check if the type implements any interfaces
        var interfaces = type.AllInterfaces;
        if (interfaces.Length > 0)
        {
            // Prefer interface with similar name (e.g., CustomerRepository -> ICustomerRepository)
            var matchingInterface = interfaces.FirstOrDefault(i =>
                i.Name == "I" + type.Name);

            if (matchingInterface != null)
            {
                return matchingInterface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }

            // Otherwise use the first interface
            return interfaces[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        // For logger types, use standard ILogger<T>
        if (IsLoggerTypeName(type.Name))
        {
            return "ILogger";
        }

        // Generate interface name (e.g., CustomerRepository -> ICustomerRepository)
        var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (!typeName.StartsWith("I"))
        {
            return "I" + typeName;
        }

        return typeName;
    }

    /// <summary>
    /// Checks if a type is a logger type.
    /// </summary>
    private static bool IsLoggerType(ITypeSymbol type)
    {
        return IsLoggerTypeName(type.Name) || type.AllInterfaces.Any(i => IsLoggerTypeName(i.Name));
    }

    /// <summary>
    /// Checks if a type name indicates a logger.
    /// </summary>
    private static bool IsLoggerTypeName(string typeName)
    {
        return typeName is "ILogger" or "Logger" or "ILog" or "Log";
    }

    /// <summary>
    /// Checks if a type is a framework type that shouldn't be extracted as dependency.
    /// </summary>
    private static bool IsFrameworkType(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        if (ns == null)
        {
            return false;
        }

        return ns.StartsWith("System.") ||
               ns.StartsWith("Microsoft.") ||
               ns == "System";
    }

    /// <summary>
    /// Checks if a type name looks like a dependency that should be injected.
    /// </summary>
    private static bool LooksLikeDependency(string typeName)
    {
        var lowerName = typeName.ToLowerInvariant();
        return lowerName.Contains("repository") ||
               lowerName.Contains("service") ||
               lowerName.Contains("manager") ||
               lowerName.Contains("provider") ||
               lowerName.Contains("factory") ||
               lowerName.Contains("context") ||
               lowerName.EndsWith("dal") ||
               lowerName.EndsWith("dao");
    }

    /// <summary>
    /// Converts a string to camelCase.
    /// </summary>
    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
