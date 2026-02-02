using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;

namespace NetLift.Transforms.Modernization.Analyzers;

/// <summary>
/// Analyzes ASP.NET MVC controllers to extract action methods and patterns using Roslyn.
/// Detects controller classes, action methods, parameters, and classifies operations as commands or queries.
/// </summary>
public sealed class ControllerAnalyzer : IControllerAnalyzer
{
    private static readonly HashSet<string> ControllerBaseClasses = new(StringComparer.Ordinal)
    {
        "Controller",
        "ControllerBase",
        "ApiController"
    };

    private static readonly HashSet<string> HttpMethodAttributes = new(StringComparer.Ordinal)
    {
        "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch", "HttpHead", "HttpOptions"
    };

    private static readonly HashSet<string> ControllerAttributes = new(StringComparer.Ordinal)
    {
        "Controller", "ApiController"
    };

    private static readonly HashSet<string> CommandOperations = new(StringComparer.Ordinal)
    {
        "SaveChanges", "SaveChangesAsync",
        "Add", "AddAsync", "AddRange", "AddRangeAsync",
        "Update", "UpdateRange",
        "Remove", "RemoveRange",
        "Delete", "DeleteAsync",
        "Insert", "InsertAsync",
        "Create", "CreateAsync",
        "Modify", "ModifyAsync"
    };

    private static readonly HashSet<string> ActionResultTypes = new(StringComparer.Ordinal)
    {
        "ActionResult", "IActionResult",
        "JsonResult", "ViewResult", "PartialViewResult",
        "RedirectResult", "RedirectToActionResult", "RedirectToRouteResult",
        "FileResult", "ContentResult", "EmptyResult",
        "StatusCodeResult", "ObjectResult"
    };

    /// <inheritdoc />
    public async Task<ControllerInfo?> AnalyzeAsync(
        string filePath,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return null;
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        // Find controller class
        var controllerClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(IsControllerClass);

        if (controllerClass == null)
        {
            return null;
        }

        // Extract namespace
        var namespaceDecl = controllerClass.Ancestors()
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();

        var fileScopedNamespace = controllerClass.Ancestors()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        var namespaceName = namespaceDecl?.Name.ToString()
            ?? fileScopedNamespace?.Name.ToString()
            ?? string.Empty;

        // Extract base class
        var baseClass = ExtractBaseClass(controllerClass);

        // Determine if API controller
        var isApiController = IsApiControllerClass(controllerClass, baseClass);

        // Extract route attributes
        var routeAttributes = ExtractRouteAttributes(controllerClass);

        // Extract action methods
        var actions = ExtractActions(controllerClass);

        // Extract private methods and track which actions call them
        var privateMethods = ExtractPrivateMethods(controllerClass);
        var privateMethodInfos = BuildPrivateMethodInfos(actions, controllerClass, privateMethods);

        // Calculate confidence score
        var confidence = CalculateConfidence(controllerClass, actions);

        return new ControllerInfo
        {
            FilePath = filePath,
            ClassName = controllerClass.Identifier.Text,
            Namespace = namespaceName,
            BaseClass = baseClass,
            IsApiController = isApiController,
            RouteAttributes = routeAttributes,
            Actions = actions,
            PrivateMethods = privateMethodInfos,
            Confidence = confidence
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ControllerInfo>> AnalyzeProjectAsync(
        Core.Models.ProjectInfo projectInfo,
        CancellationToken cancellationToken = default)
    {
        var controllers = new List<ControllerInfo>();

        // Find all controller files in the project
        var controllerFiles = projectInfo.CompileItems
            .Where(item => item.Include.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Include.Contains("Controller", StringComparison.OrdinalIgnoreCase)
                || item.Include.Contains(@"\Controllers\", StringComparison.OrdinalIgnoreCase)
                || item.Include.Contains("/Controllers/", StringComparison.OrdinalIgnoreCase));

        foreach (var item in controllerFiles)
        {
            var fullPath = Path.IsPathRooted(item.Include)
                ? item.Include
                : Path.Combine(Path.GetDirectoryName(projectInfo.FilePath) ?? string.Empty, item.Include);

            if (!File.Exists(fullPath))
            {
                continue;
            }

            try
            {
                var sourceCode = await File.ReadAllTextAsync(fullPath, cancellationToken);
                var controllerInfo = await AnalyzeAsync(fullPath, sourceCode, cancellationToken);

                if (controllerInfo != null)
                {
                    controllers.Add(controllerInfo);
                }
            }
            catch
            {
                // Skip files that cannot be read or parsed
                continue;
            }
        }

        return controllers;
    }

    /// <summary>
    /// Determines if a class is a controller class.
    /// </summary>
    private bool IsControllerClass(ClassDeclarationSyntax classDecl)
    {
        // Check if class name ends with "Controller"
        if (classDecl.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal))
        {
            return true;
        }

        // Check if has [Controller] or [ApiController] attribute
        if (HasControllerAttribute(classDecl))
        {
            return true;
        }

        // Check if inherits from a controller base class
        var baseClass = ExtractBaseClass(classDecl);
        if (baseClass != null && ControllerBaseClasses.Contains(baseClass))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a class is an API controller.
    /// </summary>
    private bool IsApiControllerClass(ClassDeclarationSyntax classDecl, string? baseClass)
    {
        // Check for [ApiController] attribute
        if (HasAttribute(classDecl, "ApiController"))
        {
            return true;
        }

        // Check if inherits from ApiController
        if (baseClass == "ApiController")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a class has a controller-related attribute.
    /// </summary>
    private bool HasControllerAttribute(ClassDeclarationSyntax classDecl)
    {
        return classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr =>
            {
                var name = ExtractAttributeName(attr);
                return ControllerAttributes.Contains(name);
            });
    }

    /// <summary>
    /// Checks if a class has a specific attribute.
    /// </summary>
    private bool HasAttribute(ClassDeclarationSyntax classDecl, string attributeName)
    {
        return classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr =>
            {
                var name = ExtractAttributeName(attr);
                return name.Equals(attributeName, StringComparison.Ordinal);
            });
    }

    /// <summary>
    /// Extracts the base class name from a class declaration.
    /// </summary>
    private string? ExtractBaseClass(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList == null || classDecl.BaseList.Types.Count == 0)
        {
            return null;
        }

        // Get the first base type (which should be the base class, not an interface)
        var firstBaseType = classDecl.BaseList.Types[0].Type;

        return firstBaseType switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.Text,
            GenericNameSyntax genericName => genericName.Identifier.Text,
            _ => null
        };
    }

    /// <summary>
    /// Extracts route attributes from a class declaration.
    /// </summary>
    private IReadOnlyList<string> ExtractRouteAttributes(ClassDeclarationSyntax classDecl)
    {
        var routes = new List<string>();

        foreach (var attributeList in classDecl.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = ExtractAttributeName(attribute);

                if (name.Equals("Route", StringComparison.Ordinal) ||
                    name.Equals("RoutePrefix", StringComparison.Ordinal))
                {
                    var template = ExtractRouteTemplate(attribute);
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        routes.Add(template);
                    }
                }
            }
        }

        return routes;
    }

    /// <summary>
    /// Extracts action methods from a controller class.
    /// </summary>
    private IReadOnlyList<ActionInfo> ExtractActions(ClassDeclarationSyntax classDecl)
    {
        var actions = new List<ActionInfo>();

        var methods = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(IsActionMethod)
            .ToList();

        // Group methods by name to detect overloads
        var methodGroups = methods.GroupBy(m => m.Identifier.Text).ToList();

        foreach (var group in methodGroups)
        {
            var hasOverload = group.Count() > 1;

            foreach (var method in group)
            {
                var actionInfo = ExtractActionInfo(method);
                // Add overload flag to action info
                actionInfo = actionInfo with { HasOverload = hasOverload };
                actions.Add(actionInfo);
            }
        }

        return actions;
    }

    /// <summary>
    /// Extracts private methods from a controller class.
    /// </summary>
    private List<MethodDeclarationSyntax> ExtractPrivateMethods(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => !m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)) &&
                       !m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.ProtectedKeyword)) &&
                       !m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.InternalKeyword)))
            .ToList();
    }

    /// <summary>
    /// Builds PrivateMethodInfo objects by analyzing which actions call which private methods.
    /// </summary>
    private IReadOnlyList<PrivateMethodInfo> BuildPrivateMethodInfos(
        IReadOnlyList<ActionInfo> actions,
        ClassDeclarationSyntax classDecl,
        List<MethodDeclarationSyntax> privateMethods)
    {
        if (privateMethods.Count == 0)
        {
            return [];
        }

        // Find all action method syntax nodes by name
        var actionMethodNodes = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => actions.Any(a => a.Name == m.Identifier.Text))
            .ToList();

        var privateMethodInfos = new List<PrivateMethodInfo>();

        foreach (var privateMethod in privateMethods)
        {
            var methodName = privateMethod.Identifier.Text;
            var callingActions = new List<string>();

            // Find which actions call this private method
            foreach (var actionNode in actionMethodNodes)
            {
                if (CallsMethod(actionNode, methodName))
                {
                    callingActions.Add(actionNode.Identifier.Text);
                }
            }

            // Only include private methods that are actually called by actions
            if (callingActions.Count > 0)
            {
                var privateMethodInfo = new PrivateMethodInfo
                {
                    Name = methodName,
                    Body = privateMethod.ToString(),
                    Parameters = ExtractParameters(privateMethod),
                    ReturnType = privateMethod.ReturnType.ToString(),
                    CallingActions = callingActions,
                    IsAsync = privateMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)),
                    IsStatic = privateMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
                };

                privateMethodInfos.Add(privateMethodInfo);
            }
        }

        return privateMethodInfos;
    }

    /// <summary>
    /// Checks if a method calls another method by name.
    /// </summary>
    private bool CallsMethod(MethodDeclarationSyntax method, string targetMethodName)
    {
        // Check method body
        if (method.Body != null)
        {
            var invocations = method.Body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var methodName = ExtractInvocationMethodName(invocation);
                if (methodName != null && methodName.Equals(targetMethodName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        // Check expression body
        if (method.ExpressionBody != null)
        {
            var invocations = method.ExpressionBody.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var methodName = ExtractInvocationMethodName(invocation);
                if (methodName != null && methodName.Equals(targetMethodName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines if a method is an action method.
    /// </summary>
    private bool IsActionMethod(MethodDeclarationSyntax method)
    {
        // Must be public
        if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            return false;
        }

        // Check if has HTTP method attribute
        if (HasHttpMethodAttribute(method))
        {
            return true;
        }

        // Check if return type is an ActionResult type
        var returnTypeName = ExtractReturnTypeName(method.ReturnType);
        if (returnTypeName != null && IsActionResultType(returnTypeName))
        {
            return true;
        }

        // Check if method name suggests it's an action (e.g., Index, Details, Create, Edit, Delete)
        // and doesn't have [NonAction] attribute
        if (!HasAttribute(method, "NonAction"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts detailed information about an action method.
    /// </summary>
    private ActionInfo ExtractActionInfo(MethodDeclarationSyntax method)
    {
        var name = method.Identifier.Text;
        var httpMethods = ExtractHttpMethods(method);
        var routeTemplate = ExtractActionRouteTemplate(method);
        var parameters = ExtractParameters(method);
        var returnType = method.ReturnType.ToString();
        var isAsync = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
        var filters = ExtractFilters(method);

        // Classify as command or query
        var isCommand = IsCommandOperation(method, httpMethods);
        var isQuery = !isCommand && (httpMethods.Count == 0 ||
            httpMethods.Any(m => m.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
                                  m.Equals("HEAD", StringComparison.OrdinalIgnoreCase)));

        // Calculate confidence
        var confidence = CalculateActionConfidence(method, httpMethods, parameters);

        // Check if action is trivial (only returns View with no logic)
        var isTrivial = IsTrivialAction(method);

        return new ActionInfo
        {
            Name = name,
            HttpMethods = httpMethods,
            RouteTemplate = routeTemplate,
            Parameters = parameters,
            ReturnType = returnType,
            IsAsync = isAsync,
            IsCommand = isCommand,
            IsQuery = isQuery,
            Filters = filters,
            Confidence = confidence,
            IsTrivial = isTrivial
        };
    }

    /// <summary>
    /// Checks if a method has an HTTP method attribute.
    /// </summary>
    private bool HasHttpMethodAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr =>
            {
                var name = ExtractAttributeName(attr);
                return HttpMethodAttributes.Contains(name);
            });
    }

    /// <summary>
    /// Checks if a method has a specific attribute.
    /// </summary>
    private bool HasAttribute(MethodDeclarationSyntax method, string attributeName)
    {
        return method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr =>
            {
                var name = ExtractAttributeName(attr);
                return name.Equals(attributeName, StringComparison.Ordinal);
            });
    }

    /// <summary>
    /// Extracts HTTP methods from a method's attributes.
    /// </summary>
    private IReadOnlyList<string> ExtractHttpMethods(MethodDeclarationSyntax method)
    {
        var methods = new List<string>();

        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = ExtractAttributeName(attribute);

                if (HttpMethodAttributes.Contains(name))
                {
                    // Extract HTTP method from attribute name (e.g., HttpGet -> GET)
                    var httpMethod = name.Replace("Http", string.Empty).ToUpperInvariant();
                    methods.Add(httpMethod);
                }
                else if (name.Equals("AcceptVerbs", StringComparison.Ordinal))
                {
                    // Extract HTTP methods from [AcceptVerbs] attribute
                    var verbs = ExtractAcceptVerbs(attribute);
                    methods.AddRange(verbs);
                }
            }
        }

        // If no HTTP method attributes, default to GET
        if (methods.Count == 0)
        {
            methods.Add("GET");
        }

        return methods;
    }

    /// <summary>
    /// Extracts route template from an action method's attributes.
    /// </summary>
    private string? ExtractActionRouteTemplate(MethodDeclarationSyntax method)
    {
        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = ExtractAttributeName(attribute);

                // Check for [Route] attribute
                if (name.Equals("Route", StringComparison.Ordinal))
                {
                    return ExtractRouteTemplate(attribute);
                }

                // Check for HTTP method attributes with route templates (e.g., [HttpGet("api/users")])
                if (HttpMethodAttributes.Contains(name))
                {
                    var template = ExtractRouteTemplate(attribute);
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        return template;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts parameters from an action method.
    /// </summary>
    private IReadOnlyList<ActionParameter> ExtractParameters(MethodDeclarationSyntax method)
    {
        var parameters = new List<ActionParameter>();

        foreach (var param in method.ParameterList.Parameters)
        {
            var parameterInfo = new ActionParameter
            {
                Name = param.Identifier.Text,
                Type = param.Type?.ToString() ?? "object",
                IsNullable = IsNullableType(param.Type),
                HasDefaultValue = param.Default != null,
                BindingSource = ExtractBindingSource(param)
            };

            parameters.Add(parameterInfo);
        }

        return parameters;
    }

    /// <summary>
    /// Extracts filter attributes from an action method.
    /// </summary>
    private IReadOnlyList<string> ExtractFilters(MethodDeclarationSyntax method)
    {
        var filters = new List<string>();

        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = ExtractAttributeName(attribute);

                // Skip HTTP method and route attributes
                if (HttpMethodAttributes.Contains(name) ||
                    name.Equals("Route", StringComparison.Ordinal))
                {
                    continue;
                }

                // Include authorization, validation, and other filter attributes
                if (name.EndsWith("Attribute", StringComparison.Ordinal))
                {
                    filters.Add(name);
                }
                else
                {
                    filters.Add(name);
                }
            }
        }

        return filters;
    }

    /// <summary>
    /// Determines if a method performs command operations (state modification).
    /// </summary>
    private bool IsCommandOperation(MethodDeclarationSyntax method, IReadOnlyList<string> httpMethods)
    {
        // Check HTTP method - POST, PUT, DELETE, PATCH are commands
        if (httpMethods.Any(m =>
            m.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            m.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            m.Equals("DELETE", StringComparison.OrdinalIgnoreCase) ||
            m.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check method body for command operations
        if (method.Body != null)
        {
            var invocations = method.Body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var methodName = ExtractInvocationMethodName(invocation);
                if (methodName != null && CommandOperations.Contains(methodName))
                {
                    return true;
                }
            }
        }

        // Check expression body for command operations
        if (method.ExpressionBody != null)
        {
            var invocations = method.ExpressionBody.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var methodName = ExtractInvocationMethodName(invocation);
                if (methodName != null && CommandOperations.Contains(methodName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the attribute name from an attribute syntax node.
    /// </summary>
    private string ExtractAttributeName(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();

        // Remove "Attribute" suffix if present
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - "Attribute".Length);
        }

        return name;
    }

    /// <summary>
    /// Extracts route template from a route attribute.
    /// </summary>
    private string? ExtractRouteTemplate(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        var firstArg = attribute.ArgumentList.Arguments[0];

        if (firstArg.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Extracts HTTP verbs from an [AcceptVerbs] attribute.
    /// </summary>
    private IReadOnlyList<string> ExtractAcceptVerbs(AttributeSyntax attribute)
    {
        var verbs = new List<string>();

        if (attribute.ArgumentList == null)
        {
            return verbs;
        }

        foreach (var arg in attribute.ArgumentList.Arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                verbs.Add(literal.Token.ValueText.ToUpperInvariant());
            }
        }

        return verbs;
    }

    /// <summary>
    /// Extracts binding source from a parameter's attributes.
    /// </summary>
    private string? ExtractBindingSource(ParameterSyntax parameter)
    {
        foreach (var attributeList in parameter.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = ExtractAttributeName(attribute);

                if (name.StartsWith("From", StringComparison.Ordinal))
                {
                    return name;
                }

                // Handle [Bind] attribute
                if (name.Equals("Bind", StringComparison.Ordinal))
                {
                    return "Bind";
                }
            }
        }

        return null;
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
    /// Extracts the return type name from a type syntax node.
    /// </summary>
    private string? ExtractReturnTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.Text,
            GenericNameSyntax genericName => genericName.Identifier.Text,
            _ => null
        };
    }

    /// <summary>
    /// Determines if a type name is an ActionResult type.
    /// </summary>
    private bool IsActionResultType(string typeName)
    {
        return ActionResultTypes.Contains(typeName) ||
               typeName.EndsWith("Result", StringComparison.Ordinal);
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
    /// Calculates the confidence score for controller analysis.
    /// </summary>
    private int CalculateConfidence(ClassDeclarationSyntax classDecl, IReadOnlyList<ActionInfo> actions)
    {
        var confidence = 100;

        // Lower confidence if no actions found
        if (actions.Count == 0)
        {
            confidence = Math.Min(confidence, 70);
        }

        // Lower confidence if class doesn't have conventional controller structure
        var hasControllerSuffix = classDecl.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal);
        var hasControllerBase = ExtractBaseClass(classDecl) != null &&
            ControllerBaseClasses.Contains(ExtractBaseClass(classDecl)!);

        if (!hasControllerSuffix && !hasControllerBase)
        {
            confidence = Math.Min(confidence, 80);
        }

        // Average with action confidence scores
        if (actions.Count > 0)
        {
            var avgActionConfidence = (int)actions.Average(a => a.Confidence);
            confidence = (confidence + avgActionConfidence) / 2;
        }

        return confidence;
    }

    /// <summary>
    /// Calculates the confidence score for action method analysis.
    /// </summary>
    private int CalculateActionConfidence(
        MethodDeclarationSyntax method,
        IReadOnlyList<string> httpMethods,
        IReadOnlyList<ActionParameter> parameters)
    {
        var confidence = 100;

        // Lower confidence if no explicit HTTP method attribute and not conventional name
        if (!HasHttpMethodAttribute(method))
        {
            confidence = Math.Min(confidence, 90);
        }

        // Lower confidence if complex parameter binding without attributes
        var complexParamsWithoutBinding = parameters.Count(p =>
            p.BindingSource == null &&
            !IsPrimitiveType(p.Type) &&
            !p.Type.Equals("string", StringComparison.Ordinal));

        if (complexParamsWithoutBinding > 0)
        {
            confidence = Math.Min(confidence, 85);
        }

        // Lower confidence if method has unusual modifiers
        if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            confidence = Math.Min(confidence, 70);
        }

        return confidence;
    }

    /// <summary>
    /// Determines if a type name represents a primitive type.
    /// </summary>
    private bool IsPrimitiveType(string typeName)
    {
        var primitives = new HashSet<string>(StringComparer.Ordinal)
        {
            "int", "long", "short", "byte", "sbyte",
            "uint", "ulong", "ushort",
            "float", "double", "decimal",
            "bool", "char",
            "Int32", "Int64", "Int16", "Byte", "SByte",
            "UInt32", "UInt64", "UInt16",
            "Single", "Double", "Decimal",
            "Boolean", "Char"
        };

        return primitives.Contains(typeName);
    }

    /// <summary>
    /// Determines if an action method is trivial and should not generate CQRS handlers.
    /// Trivial actions are simple methods that only return a view with no business logic.
    /// A truly trivial action:
    /// - Returns View() with no arguments, OR
    /// - Returns View(new EmptyModel()) - just creating a new empty object, no properties set
    /// - Has NO parameters (actions with parameters typically need logic)
    /// - Has NO other statements besides the return
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <returns>True if the action is trivial and should skip CQRS generation.</returns>
    private bool IsTrivialAction(MethodDeclarationSyntax method)
    {
        // Actions with parameters are not trivial (they need to process those parameters)
        if (method.ParameterList.Parameters.Count > 0)
        {
            return false;
        }

        // Check method body
        if (method.Body != null)
        {
            var statements = method.Body.Statements;

            // Must have exactly 1 statement (just the return)
            if (statements.Count != 1)
            {
                return false;
            }

            // Find the return statement
            var returnStatement = statements.OfType<ReturnStatementSyntax>().FirstOrDefault();
            if (returnStatement == null)
            {
                return false;
            }

            // Check if it returns View() or View(something trivial)
            if (!IsViewReturn(returnStatement))
            {
                return false;
            }

            // Check that View argument (if any) is trivial - just new X() with no property initializers
            if (returnStatement.Expression is InvocationExpressionSyntax viewInvocation &&
                viewInvocation.ArgumentList != null &&
                viewInvocation.ArgumentList.Arguments.Count > 0)
            {
                var arg = viewInvocation.ArgumentList.Arguments[0].Expression;

                // Check if it's just 'new SomeType()' without any property/collection initializers
                if (arg is ObjectCreationExpressionSyntax objectCreation)
                {
                    // If it has initializers or arguments, it's not trivial
                    if (objectCreation.ArgumentList?.Arguments.Count > 0 ||
                        objectCreation.Initializer != null)
                    {
                        return false;
                    }
                }
                else
                {
                    // Any other expression passed to View() means it's not trivial
                    return false;
                }
            }

            return true;
        }

        // Check expression body
        if (method.ExpressionBody != null)
        {
            var expression = method.ExpressionBody.Expression;

            // Must be a View() or View(new X()) call with no arguments or simple object creation
            if (expression is InvocationExpressionSyntax invocation)
            {
                var methodName = ExtractInvocationMethodName(invocation);
                if (methodName != null &&
                    (methodName.Equals("View", StringComparison.Ordinal) ||
                     methodName.Equals("PartialView", StringComparison.Ordinal)))
                {
                    // Check View argument (if any) is trivial
                    if (invocation.ArgumentList != null && invocation.ArgumentList.Arguments.Count > 0)
                    {
                        var arg = invocation.ArgumentList.Arguments[0].Expression;

                        if (arg is ObjectCreationExpressionSyntax objectCreation)
                        {
                            // If it has initializers or arguments, it's not trivial
                            if (objectCreation.ArgumentList?.Arguments.Count > 0 ||
                                objectCreation.Initializer != null)
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
        }

        return false;
    }


    /// <summary>
    /// Checks if a return statement returns a View.
    /// </summary>
    private bool IsViewReturn(ReturnStatementSyntax returnStatement)
    {
        if (returnStatement.Expression is InvocationExpressionSyntax invocation)
        {
            var methodName = ExtractInvocationMethodName(invocation);
            return methodName != null &&
                   (methodName.Equals("View", StringComparison.Ordinal) ||
                    methodName.Equals("PartialView", StringComparison.Ordinal));
        }

        return false;
    }
}
