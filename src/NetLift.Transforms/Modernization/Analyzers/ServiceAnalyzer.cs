using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;

namespace NetLift.Transforms.Modernization.Analyzers;

/// <summary>
/// Analyzes service classes to extract method information and dependencies using Roslyn.
/// Detects service classes, methods, dependencies (DbContext, repositories), and identifies state-modifying operations.
/// </summary>
public sealed class ServiceAnalyzer : IServiceAnalyzer
{
    private static readonly HashSet<string> StateModifyingOperations = new(StringComparer.Ordinal)
    {
        "SaveChanges", "SaveChangesAsync",
        "Add", "AddAsync", "AddRange", "AddRangeAsync",
        "Update", "UpdateAsync", "UpdateRange",
        "Remove", "RemoveAsync", "RemoveRange",
        "Delete", "DeleteAsync",
        "Insert", "InsertAsync",
        "Create", "CreateAsync",
        "Modify", "ModifyAsync",
        "ExecuteSqlRaw", "ExecuteSqlRawAsync",
        "ExecuteSqlCommand", "ExecuteSqlCommandAsync"
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceInfo>> AnalyzeServicesAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var services = new List<ServiceInfo>();

        if (!Directory.Exists(projectPath))
        {
            return services;
        }

        // Find all C# files in the project
        var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase) &&
                       !f.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase));

        foreach (var filePath in csFiles)
        {
            try
            {
                var serviceInfo = await AnalyzeServiceFileAsync(filePath, cancellationToken);
                if (serviceInfo != null)
                {
                    services.Add(serviceInfo);
                }
            }
            catch
            {
                // Skip files that cannot be read or parsed
                continue;
            }
        }

        return services;
    }

    /// <inheritdoc />
    public async Task<ServiceInfo?> AnalyzeServiceFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return null;
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        // Find service class
        var serviceClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(IsServiceClass);

        if (serviceClass == null)
        {
            return null;
        }

        // Extract namespace
        var namespaceDecl = serviceClass.Ancestors()
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();

        var fileScopedNamespace = serviceClass.Ancestors()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        var namespaceName = namespaceDecl?.Name.ToString()
            ?? fileScopedNamespace?.Name.ToString()
            ?? string.Empty;

        // Extract implemented interfaces
        var implementedInterfaces = ExtractImplementedInterfaces(serviceClass);

        // Extract dependencies from constructor
        var dependencies = ExtractDependencies(serviceClass);

        // Check if uses DbContext
        var usesDbContext = dependencies.Any(d => d.IsDbContext);
        var dbContextTypeName = dependencies.FirstOrDefault(d => d.IsDbContext)?.Type;

        // Extract methods
        var methods = ExtractMethods(serviceClass, dependencies);

        return new ServiceInfo
        {
            FilePath = filePath,
            ClassName = serviceClass.Identifier.Text,
            Namespace = namespaceName,
            ImplementedInterfaces = implementedInterfaces,
            Dependencies = dependencies,
            UsesDbContext = usesDbContext,
            DbContextTypeName = dbContextTypeName,
            Methods = methods
        };
    }

    /// <inheritdoc />
    public ServiceMethodInfo? FindServiceMethod(
        IReadOnlyList<ServiceInfo> services,
        string callExpression)
    {
        if (string.IsNullOrWhiteSpace(callExpression))
        {
            return null;
        }

        // Parse call expression (e.g., "service.GetItem(id)" or "GetItem(id)")
        var parts = callExpression.Split('.');
        var methodName = parts.Length > 1 ? parts[^1] : parts[0];

        // Remove arguments if present
        var parenIndex = methodName.IndexOf('(');
        if (parenIndex > 0)
        {
            methodName = methodName.Substring(0, parenIndex);
        }

        // Search for the method in all services
        foreach (var service in services)
        {
            var method = service.Methods.FirstOrDefault(m =>
                m.Name.Equals(methodName, StringComparison.Ordinal));

            if (method != null)
            {
                return method;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines if a class is a service class.
    /// </summary>
    private bool IsServiceClass(ClassDeclarationSyntax classDecl)
    {
        // Check if class name ends with "Service"
        if (classDecl.Identifier.Text.EndsWith("Service", StringComparison.Ordinal))
        {
            return true;
        }

        // Check if implements an interface with "Service" in the name
        if (classDecl.BaseList != null)
        {
            foreach (var baseType in classDecl.BaseList.Types)
            {
                var typeName = baseType.Type.ToString();
                if (typeName.Contains("Service", StringComparison.Ordinal) &&
                    typeName.StartsWith("I", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts implemented interfaces from a class declaration.
    /// </summary>
    private IReadOnlyList<string> ExtractImplementedInterfaces(ClassDeclarationSyntax classDecl)
    {
        var interfaces = new List<string>();

        if (classDecl.BaseList == null)
        {
            return interfaces;
        }

        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeName = baseType.Type.ToString();

            // Only include interfaces (typically start with 'I')
            if (typeName.StartsWith("I", StringComparison.Ordinal) ||
                IsKnownInterface(typeName))
            {
                interfaces.Add(typeName);
            }
        }

        return interfaces;
    }

    /// <summary>
    /// Checks if a type name is a known interface type.
    /// </summary>
    private bool IsKnownInterface(string typeName)
    {
        // Common interface patterns
        return typeName.Contains("<") || // Generic interfaces like IEnumerable<T>
               typeName.Contains("Service", StringComparison.Ordinal) ||
               typeName.Contains("Repository", StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts dependencies from constructor parameters.
    /// </summary>
    private IReadOnlyList<ServiceDependency> ExtractDependencies(ClassDeclarationSyntax classDecl)
    {
        var dependencies = new List<ServiceDependency>();

        // Find constructor(s)
        var constructors = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));

        // Use the constructor with the most parameters (primary DI constructor)
        var primaryConstructor = constructors
            .OrderByDescending(c => c.ParameterList.Parameters.Count)
            .FirstOrDefault();

        if (primaryConstructor == null)
        {
            return dependencies;
        }

        foreach (var parameter in primaryConstructor.ParameterList.Parameters)
        {
            var paramName = parameter.Identifier.Text;
            var paramType = parameter.Type?.ToString() ?? "object";

            var dependency = new ServiceDependency
            {
                Name = paramName,
                Type = paramType,
                IsDbContext = IsDbContextType(paramType),
                IsRepository = IsRepositoryType(paramType),
                IsLogger = IsLoggerType(paramType)
            };

            dependencies.Add(dependency);
        }

        return dependencies;
    }

    /// <summary>
    /// Determines if a type is a DbContext.
    /// </summary>
    private bool IsDbContextType(string typeName)
    {
        return typeName.Contains("DbContext", StringComparison.Ordinal) ||
               typeName.Contains("Context", StringComparison.Ordinal) &&
               !typeName.Equals("HttpContext", StringComparison.Ordinal) &&
               !typeName.Equals("RequestContext", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines if a type is a repository.
    /// </summary>
    private bool IsRepositoryType(string typeName)
    {
        return typeName.Contains("Repository", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines if a type is a logger.
    /// </summary>
    private bool IsLoggerType(string typeName)
    {
        return typeName.Contains("ILogger", StringComparison.Ordinal) ||
               typeName.Contains("ILog", StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts methods from a service class.
    /// </summary>
    private IReadOnlyList<ServiceMethodInfo> ExtractMethods(
        ClassDeclarationSyntax classDecl,
        IReadOnlyList<ServiceDependency> dependencies)
    {
        var methods = new List<ServiceMethodInfo>();

        // Get public methods (excluding constructors)
        var publicMethods = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));

        foreach (var method in publicMethods)
        {
            var methodInfo = ExtractMethodInfo(method, dependencies);
            methods.Add(methodInfo);
        }

        return methods;
    }

    /// <summary>
    /// Extracts detailed information about a service method.
    /// </summary>
    private ServiceMethodInfo ExtractMethodInfo(
        MethodDeclarationSyntax method,
        IReadOnlyList<ServiceDependency> dependencies)
    {
        var name = method.Identifier.Text;
        var returnType = method.ReturnType.ToString();
        var isAsync = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
        var parameters = ExtractParameters(method);
        var modifiesState = DetectsStateModification(method);
        var extractedLogic = ExtractMethodLogic(method, dependencies);

        return new ServiceMethodInfo
        {
            Name = name,
            ReturnType = returnType,
            Parameters = parameters,
            IsAsync = isAsync,
            ModifiesState = modifiesState,
            ExtractedLogic = extractedLogic
        };
    }

    /// <summary>
    /// Extracts parameters from a method.
    /// </summary>
    private IReadOnlyList<MethodParameter> ExtractParameters(MethodDeclarationSyntax method)
    {
        var parameters = new List<MethodParameter>();

        foreach (var param in method.ParameterList.Parameters)
        {
            var parameter = new MethodParameter
            {
                Name = param.Identifier.Text,
                Type = param.Type?.ToString() ?? "object",
                IsNullable = IsNullableType(param.Type),
                HasDefaultValue = param.Default != null,
                DefaultValue = param.Default?.Value.ToString()
            };

            parameters.Add(parameter);
        }

        return parameters;
    }

    /// <summary>
    /// Determines if a type is nullable.
    /// </summary>
    private bool IsNullableType(TypeSyntax? type)
    {
        if (type == null)
        {
            return false;
        }

        // Check for nullable reference types (string?)
        if (type is NullableTypeSyntax)
        {
            return true;
        }

        // Check for Nullable<T>
        if (type is GenericNameSyntax genericName &&
            genericName.Identifier.Text == "Nullable")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Detects if a method modifies state (Create, Update, Delete, Save operations).
    /// </summary>
    private bool DetectsStateModification(MethodDeclarationSyntax method)
    {
        // Check method body for state-modifying operations
        if (method.Body != null)
        {
            var invocations = method.Body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var methodName = ExtractInvocationMethodName(invocation);
                if (methodName != null && StateModifyingOperations.Contains(methodName))
                {
                    return true;
                }
            }
        }

        // Check expression body for state-modifying operations
        if (method.ExpressionBody != null)
        {
            var invocations = method.ExpressionBody.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var methodName = ExtractInvocationMethodName(invocation);
                if (methodName != null && StateModifyingOperations.Contains(methodName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts method logic for later transformation.
    /// </summary>
    private ExtractedLogic ExtractMethodLogic(
        MethodDeclarationSyntax method,
        IReadOnlyList<ServiceDependency> dependencies)
    {
        var variables = new List<VariableInfo>();
        var statements = new List<StatementInfo>();
        var serviceCalls = new List<MethodCallInfo>();
        var dbOperations = new List<DbContextOperation>();
        var usedDependencies = new List<string>();
        var confidence = 100;
        var warnings = new List<string>();

        if (method.Body == null && method.ExpressionBody == null)
        {
            return new ExtractedLogic
            {
                Variables = variables,
                Statements = statements,
                ServiceCalls = serviceCalls,
                DbOperations = dbOperations,
                UsedDependencies = usedDependencies,
                HasModelStateValidation = false,
                HasViewBagUsage = false,
                Confidence = confidence,
                Warnings = warnings
            };
        }

        // Extract from method body
        if (method.Body != null)
        {
            ExtractFromBlock(method.Body, variables, statements, serviceCalls, dbOperations,
                usedDependencies, dependencies, warnings);
        }

        // Extract from expression body
        if (method.ExpressionBody != null)
        {
            ExtractFromExpression(method.ExpressionBody.Expression, statements, serviceCalls,
                dbOperations, usedDependencies, dependencies);
        }

        // Extract return statement
        ReturnInfo? returnInfo = null;
        var returnStatement = method.Body?.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .LastOrDefault();

        if (returnStatement != null)
        {
            returnInfo = new ReturnInfo
            {
                Expression = returnStatement.Expression?.ToString() ?? string.Empty,
                IsViewReturn = false,
                IsRedirect = false,
                IsErrorReturn = false,
                ReturnedModel = returnStatement.Expression?.ToString()
            };
        }
        else if (method.ExpressionBody != null)
        {
            returnInfo = new ReturnInfo
            {
                Expression = method.ExpressionBody.Expression.ToString(),
                IsViewReturn = false,
                IsRedirect = false,
                IsErrorReturn = false,
                ReturnedModel = method.ExpressionBody.Expression.ToString()
            };
        }

        // Calculate confidence based on complexity
        if (statements.Count > 20)
        {
            confidence = Math.Min(confidence, 85);
            warnings.Add("Method is complex with many statements");
        }

        if (serviceCalls.Count > 10)
        {
            confidence = Math.Min(confidence, 80);
            warnings.Add("Method has many service calls");
        }

        return new ExtractedLogic
        {
            Variables = variables,
            Statements = statements,
            ServiceCalls = serviceCalls,
            DbOperations = dbOperations,
            ReturnStatement = returnInfo,
            UsedDependencies = usedDependencies,
            HasModelStateValidation = false, // Not applicable to services
            HasViewBagUsage = false, // Not applicable to services
            Confidence = confidence,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Extracts logic from a block statement.
    /// </summary>
    private void ExtractFromBlock(
        BlockSyntax block,
        List<VariableInfo> variables,
        List<StatementInfo> statements,
        List<MethodCallInfo> serviceCalls,
        List<DbContextOperation> dbOperations,
        List<string> usedDependencies,
        IReadOnlyList<ServiceDependency> dependencies,
        List<string> warnings)
    {
        foreach (var statement in block.Statements)
        {
            var lineNumber = statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            // Extract variable declarations
            if (statement is LocalDeclarationStatementSyntax localDecl)
            {
                foreach (var variable in localDecl.Declaration.Variables)
                {
                    variables.Add(new VariableInfo
                    {
                        Name = variable.Identifier.Text,
                        Type = localDecl.Declaration.Type.ToString(),
                        Initializer = variable.Initializer?.Value.ToString(),
                        LineNumber = lineNumber
                    });
                }
            }

            // Determine statement type
            var statementType = statement switch
            {
                LocalDeclarationStatementSyntax => StatementType.Declaration,
                ExpressionStatementSyntax => StatementType.Assignment,
                IfStatementSyntax => StatementType.If,
                ReturnStatementSyntax => StatementType.Return,
                ForEachStatementSyntax => StatementType.ForEach,
                ForStatementSyntax => StatementType.For,
                WhileStatementSyntax => StatementType.While,
                TryStatementSyntax => StatementType.Try,
                ThrowStatementSyntax => StatementType.Throw,
                _ => StatementType.Other
            };

            statements.Add(new StatementInfo
            {
                Type = statementType,
                SourceCode = statement.ToString(),
                LineNumber = lineNumber,
                NeedsAsyncTransform = ContainsAsyncCall(statement)
            });

            // Extract method invocations
            var invocations = statement.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                ExtractMethodCall(invocation, serviceCalls, dbOperations, usedDependencies, dependencies);
            }
        }
    }

    /// <summary>
    /// Extracts logic from an expression.
    /// </summary>
    private void ExtractFromExpression(
        ExpressionSyntax expression,
        List<StatementInfo> statements,
        List<MethodCallInfo> serviceCalls,
        List<DbContextOperation> dbOperations,
        List<string> usedDependencies,
        IReadOnlyList<ServiceDependency> dependencies)
    {
        statements.Add(new StatementInfo
        {
            Type = StatementType.Return,
            SourceCode = expression.ToString(),
            LineNumber = expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            NeedsAsyncTransform = ContainsAsyncCall(expression)
        });

        // Extract method invocations
        var invocations = expression.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            ExtractMethodCall(invocation, serviceCalls, dbOperations, usedDependencies, dependencies);
        }
    }

    /// <summary>
    /// Extracts method call information.
    /// </summary>
    private void ExtractMethodCall(
        InvocationExpressionSyntax invocation,
        List<MethodCallInfo> serviceCalls,
        List<DbContextOperation> dbOperations,
        List<string> usedDependencies,
        IReadOnlyList<ServiceDependency> dependencies)
    {
        var methodName = ExtractInvocationMethodName(invocation);
        var target = ExtractInvocationTarget(invocation);

        if (methodName == null || target == null)
        {
            return;
        }

        // Track used dependencies
        var dependency = dependencies.FirstOrDefault(d =>
            target.Contains(d.Name, StringComparison.Ordinal));

        if (dependency != null && !usedDependencies.Contains(dependency.Name))
        {
            usedDependencies.Add(dependency.Name);
        }

        // Check if this is a DbContext call
        var isDbContextCall = dependency?.IsDbContext ?? false;

        var methodCall = new MethodCallInfo
        {
            Target = target,
            MethodName = methodName,
            Arguments = ExtractArguments(invocation),
            SourceCode = invocation.ToString(),
            IsDbContextCall = isDbContextCall,
            ShouldBeAsync = ShouldBeAsync(methodName),
            AsyncEquivalent = GetAsyncEquivalent(methodName)
        };

        serviceCalls.Add(methodCall);

        // Extract DbContext operations
        if (isDbContextCall)
        {
            var dbOperation = ExtractDbOperation(invocation, methodName);
            if (dbOperation != null)
            {
                dbOperations.Add(dbOperation);
            }
        }
    }

    /// <summary>
    /// Extracts the method name from an invocation expression.
    /// </summary>
    private string? ExtractInvocationMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };
    }

    /// <summary>
    /// Extracts the target object from an invocation expression.
    /// </summary>
    private string? ExtractInvocationTarget(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Expression.ToString();
        }

        return null;
    }

    /// <summary>
    /// Extracts arguments from an invocation.
    /// </summary>
    private IReadOnlyList<string> ExtractArguments(InvocationExpressionSyntax invocation)
    {
        return invocation.ArgumentList.Arguments
            .Select(arg => arg.Expression.ToString())
            .ToList();
    }

    /// <summary>
    /// Determines if a method should be called asynchronously.
    /// </summary>
    private bool ShouldBeAsync(string methodName)
    {
        var asyncMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "ToList", "ToArray", "FirstOrDefault", "First",
            "SingleOrDefault", "Single", "Any", "All", "Count",
            "Find", "SaveChanges", "Add", "Remove", "Update"
        };

        return asyncMethods.Contains(methodName);
    }

    /// <summary>
    /// Gets the async equivalent method name.
    /// </summary>
    private string? GetAsyncEquivalent(string methodName)
    {
        return methodName switch
        {
            "ToList" => "ToListAsync",
            "ToArray" => "ToArrayAsync",
            "FirstOrDefault" => "FirstOrDefaultAsync",
            "First" => "FirstAsync",
            "SingleOrDefault" => "SingleOrDefaultAsync",
            "Single" => "SingleAsync",
            "Any" => "AnyAsync",
            "All" => "AllAsync",
            "Count" => "CountAsync",
            "Find" => "FindAsync",
            "SaveChanges" => "SaveChangesAsync",
            "Add" => "AddAsync",
            "Remove" => "Remove", // No async version
            "Update" => "Update", // No async version
            _ => null
        };
    }

    /// <summary>
    /// Checks if a syntax node contains async method calls.
    /// </summary>
    private bool ContainsAsyncCall(SyntaxNode node)
    {
        var invocations = node.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var methodName = ExtractInvocationMethodName(invocation);
            if (methodName != null && ShouldBeAsync(methodName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts DbContext operation information.
    /// </summary>
    private DbContextOperation? ExtractDbOperation(
        InvocationExpressionSyntax invocation,
        string methodName)
    {
        var operationType = methodName switch
        {
            "Find" or "FindAsync" => DbOperationType.Find,
            "Add" or "AddAsync" or "AddRange" or "AddRangeAsync" => DbOperationType.Add,
            "Update" or "UpdateRange" => DbOperationType.Update,
            "Remove" or "RemoveRange" => DbOperationType.Remove,
            "SaveChanges" or "SaveChangesAsync" => DbOperationType.SaveChanges,
            "Entry" => DbOperationType.Entry,
            _ => DbOperationType.Query
        };

        // Extract LINQ operations if this is a query
        var linqOperations = new List<string>();
        if (operationType == DbOperationType.Query)
        {
            var currentNode = invocation.Parent;
            while (currentNode is MemberAccessExpressionSyntax memberAccess)
            {
                linqOperations.Add(memberAccess.Name.Identifier.Text);
                currentNode = currentNode.Parent;
            }
        }

        return new DbContextOperation
        {
            OperationType = operationType,
            SourceCode = invocation.ToString(),
            LinqOperations = linqOperations
        };
    }
}
