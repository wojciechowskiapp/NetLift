using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;

namespace NetLift.Transforms.Modernization.Transformers;

/// <summary>
/// Transforms controller classes to use MediatR instead of direct service calls.
/// Replaces action method bodies with MediatR.Send() calls and injects IMediator.
/// </summary>
public sealed class ControllerSlimmer : IControllerTransformer
{
    /// <inheritdoc />
    public Task<ControllerTransformResult> TransformAsync(
        string controllerSource,
        IReadOnlyList<ActionLogicContext> actionContexts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(controllerSource);
        ArgumentNullException.ThrowIfNull(actionContexts);

        if (string.IsNullOrWhiteSpace(controllerSource))
        {
            return Task.FromResult(new ControllerTransformResult
            {
                TransformedSource = controllerSource,
                Confidence = 100
            });
        }

        // Parse the source code
        var tree = CSharpSyntaxTree.ParseText(controllerSource);
        var root = tree.GetRoot();

        // Create rewriter instance
        var rewriter = new ControllerSlimmerRewriter(actionContexts);

        // Rewrite the tree
        var rewritten = rewriter.Visit(root);

        if (rewritten == null)
        {
            return Task.FromResult(new ControllerTransformResult
            {
                TransformedSource = controllerSource,
                Confidence = 0,
                Warnings = [new TransformWarning
                {
                    ActionName = "Unknown",
                    Message = "Failed to parse controller source",
                    Severity = "Error"
                }]
            });
        }

        // Add required usings
        rewritten = rewriter.AddRequiredUsings(rewritten);

        var result = new ControllerTransformResult
        {
            TransformedSource = rewritten.ToFullString(),
            RequiredUsings = rewriter.RequiredUsings.ToList(),
            TransformedActions = rewriter.TransformedActions.ToList(),
            Warnings = rewriter.Warnings.ToList(),
            Confidence = rewriter.Confidence
        };

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<string> TransformActionAsync(
        string actionSource,
        ActionLogicContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actionSource);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(actionSource))
        {
            return Task.FromResult(actionSource);
        }

        // Parse the action method
        var tree = CSharpSyntaxTree.ParseText($"class Temp {{ {actionSource} }}");
        var root = tree.GetRoot();
        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (method == null)
        {
            return Task.FromResult(actionSource);
        }

        // Transform the method
        var rewriter = new ControllerSlimmerRewriter([context]);
        var transformed = rewriter.TransformAction(method, context);

        return Task.FromResult(transformed?.ToFullString() ?? actionSource);
    }

    /// <summary>
    /// Roslyn rewriter that transforms controller actions to use MediatR.
    /// </summary>
    private sealed class ControllerSlimmerRewriter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, ActionLogicContext> _actionContextMap;
        private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
        private readonly List<string> _transformedActions = new();
        private readonly List<TransformWarning> _warnings = new();
        private int _lowestConfidence = 100;
        private bool _hasMediatorField;
        private bool _hasConstructor;
        private string? _rootNamespace;

        public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;
        public IReadOnlyCollection<string> TransformedActions => _transformedActions;
        public IReadOnlyCollection<TransformWarning> Warnings => _warnings;
        public int Confidence => _lowestConfidence;

        public ControllerSlimmerRewriter(IReadOnlyList<ActionLogicContext> actionContexts)
        {
            // Use composite key (name + parameter types) to handle method overloads with same param count
            _actionContextMap = actionContexts.ToDictionary(
                ctx => GetActionKey(ctx.Action.Name, ctx.Action.Parameters),
                ctx => ctx,
                StringComparer.Ordinal);

            // Extract root namespace from the first action context
            if (actionContexts.Count > 0)
            {
                _rootNamespace = ExtractRootNamespace(actionContexts[0].TargetNamespace);
            }
        }

        /// <summary>
        /// Creates a composite key for action lookup that handles overloads.
        /// Uses parameter types to distinguish Edit(int? id) from Edit(Course course).
        /// </summary>
        private static string GetActionKey(string actionName, IReadOnlyList<ActionParameter> parameters)
        {
            if (parameters.Count == 0)
            {
                return $"{actionName}_0";
            }

            // Include simplified type names to handle same-count different-type overloads
            var typeSignature = string.Join("_", parameters.Select(p => SimplifyTypeName(p.Type)));
            return $"{actionName}_{parameters.Count}_{typeSignature}";
        }

        /// <summary>
        /// Simplifies a type name for key generation.
        /// </summary>
        private static string SimplifyTypeName(string typeName)
        {
            // Remove nullable marker and get just the type name
            var simplified = typeName.TrimEnd('?').Replace("[]", "Array");

            // Extract just the type name without namespace
            var lastDot = simplified.LastIndexOf('.');
            if (lastDot >= 0)
            {
                simplified = simplified.Substring(lastDot + 1);
            }

            // Remove generic parameters for simplicity
            var genericStart = simplified.IndexOf('<');
            if (genericStart >= 0)
            {
                simplified = simplified.Substring(0, genericStart);
            }

            return simplified;
        }

        /// <summary>
        /// Visits class declarations to add IMediator field and constructor injection.
        /// </summary>
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            // Check if this is a controller class
            if (!IsControllerClass(node))
            {
                return base.VisitClassDeclaration(node);
            }

            // First, check if IMediator is already present
            _hasMediatorField = HasMediatorField(node);
            _hasConstructor = node.Members.OfType<ConstructorDeclarationSyntax>().Any();

            // Visit children to transform methods
            var visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
            if (visited == null)
            {
                return null;
            }

            // If no transformations were made, return original
            if (_transformedActions.Count == 0)
            {
                return visited;
            }

            // Add IMediator field and constructor injection if not present
            if (!_hasMediatorField)
            {
                visited = AddMediatorInjection(visited);
            }

            return visited;
        }

        /// <summary>
        /// Visits method declarations to transform action methods.
        /// </summary>
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var methodName = node.Identifier.Text;

            // Build composite key matching the one used in dictionary
            var actionKey = GetActionKeyFromSyntax(methodName, node.ParameterList.Parameters);
            if (!_actionContextMap.TryGetValue(actionKey, out var context))
            {
                return node;
            }

            // Transform the action
            var transformed = TransformAction(node, context);
            if (transformed != null && transformed != node)
            {
                _transformedActions.Add(methodName);
                _lowestConfidence = Math.Min(_lowestConfidence, context.Confidence);
            }

            return transformed;
        }

        /// <summary>
        /// Creates a composite key from syntax node parameters.
        /// </summary>
        private static string GetActionKeyFromSyntax(string actionName, SeparatedSyntaxList<ParameterSyntax> parameters)
        {
            if (parameters.Count == 0)
            {
                return $"{actionName}_0";
            }

            // Include simplified type names to match the key from ActionParameter
            var typeSignature = string.Join("_", parameters.Select(p => SimplifyTypeName(p.Type?.ToString() ?? "object")));
            return $"{actionName}_{parameters.Count}_{typeSignature}";
        }

        /// <summary>
        /// Transforms a single action method to use MediatR.
        /// </summary>
        public MethodDeclarationSyntax? TransformAction(
            MethodDeclarationSyntax method,
            ActionLogicContext context)
        {
            var actionInfo = context.Action;
            var requestTypeName = GenerateRequestTypeName(context);

            // Build new return type (async Task<IActionResult>)
            var newReturnType = BuildReturnType(actionInfo);

            // Build new parameters (handle nullable → required)
            var newParameters = BuildParameters(actionInfo);

            // Build new body (MediatR.Send call)
            var newBody = BuildMediatorSendBody(context, requestTypeName);

            // Add async modifier if needed
            var modifiers = method.Modifiers;
            if (!actionInfo.IsAsync && !modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            {
                modifiers = modifiers.Add(
                    SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
                        .WithTrailingTrivia(SyntaxFactory.Space));
            }

            // Build transformed method
            var transformed = method
                .WithModifiers(modifiers)
                .WithReturnType(newReturnType)
                .WithParameterList(newParameters)
                .WithBody(newBody)
                .WithExpressionBody(null) // Remove expression body if present
                .NormalizeWhitespace(); // Ensure proper formatting

            // Add required usings
            if (!string.IsNullOrEmpty(_rootNamespace))
            {
                _requiredUsings.Add($"{_rootNamespace}.Application.Common.Interfaces");
            }
            _requiredUsings.Add("Microsoft.AspNetCore.Mvc");

            return transformed;
        }

        /// <summary>
        /// Checks if a class is a controller class.
        /// </summary>
        private static bool IsControllerClass(ClassDeclarationSyntax node)
        {
            if (node.BaseList == null)
            {
                return false;
            }

            foreach (var baseType in node.BaseList.Types)
            {
                var typeName = baseType.Type.ToString();
                if (typeName.Contains("Controller", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a class has an IMediator field.
        /// </summary>
        private static bool HasMediatorField(ClassDeclarationSyntax node)
        {
            return node.Members
                .OfType<FieldDeclarationSyntax>()
                .Any(f => f.Declaration.Type.ToString().Contains("IMediator", StringComparison.Ordinal));
        }

        /// <summary>
        /// Adds IMediator field and constructor injection to a controller.
        /// </summary>
        private ClassDeclarationSyntax AddMediatorInjection(ClassDeclarationSyntax node)
        {
            const string mediatorType = "IMediator";
            const string mediatorParam = "mediator";
            const string mediatorField = "_mediator";

            // Get existing constructors
            var constructors = node.Members.OfType<ConstructorDeclarationSyntax>().ToList();

            ClassDeclarationSyntax result;

            if (constructors.Count == 0)
            {
                // No constructor - add new one with IMediator
                result = AddNewConstructorWithMediator(node, node.Identifier.Text, mediatorType, mediatorParam, mediatorField);
            }
            else
            {
                // Add IMediator to first constructor
                result = AddMediatorToExistingConstructor(node, constructors[0], mediatorType, mediatorParam, mediatorField);
            }

            if (!string.IsNullOrEmpty(_rootNamespace))
            {
                _requiredUsings.Add($"{_rootNamespace}.Application.Common.Interfaces");
            }

            return result;
        }

        /// <summary>
        /// Adds a new constructor with IMediator injection.
        /// </summary>
        private static ClassDeclarationSyntax AddNewConstructorWithMediator(
            ClassDeclarationSyntax node,
            string className,
            string mediatorType,
            string mediatorParam,
            string mediatorField)
        {
            // Create field
            var fieldCode = $"private readonly {mediatorType} {mediatorField};";
            var fieldTree = CSharpSyntaxTree.ParseText($"class Temp {{ {fieldCode} }}");
            var fieldRoot = fieldTree.GetRoot();
            var mediatorFieldDecl = fieldRoot.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .First()
                .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

            // Create constructor
            var constructorCode = $@"public {className}({mediatorType} {mediatorParam})
    {{
        {mediatorField} = {mediatorParam};
    }}";
            var constructorTree = CSharpSyntaxTree.ParseText($"class Temp {{ {constructorCode} }}");
            var constructorRoot = constructorTree.GetRoot();
            var constructor = constructorRoot.DescendantNodes()
                .OfType<ConstructorDeclarationSyntax>()
                .First()
                .WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace("    "))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

            // Add members to class
            var newMembers = node.Members.Insert(0, mediatorFieldDecl).Insert(1, constructor);
            return node.WithMembers(newMembers);
        }

        /// <summary>
        /// Adds IMediator parameter to an existing constructor.
        /// </summary>
        private static ClassDeclarationSyntax AddMediatorToExistingConstructor(
            ClassDeclarationSyntax node,
            ConstructorDeclarationSyntax existingConstructor,
            string mediatorType,
            string mediatorParam,
            string mediatorField)
        {
            // Create field
            var fieldCode = $"private readonly {mediatorType} {mediatorField};";
            var fieldTree = CSharpSyntaxTree.ParseText($"class Temp {{ {fieldCode} }}");
            var fieldRoot = fieldTree.GetRoot();
            var mediatorFieldDecl = fieldRoot.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .First()
                .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

            // Build new parameter list
            var existingParams = string.Join(", ", existingConstructor.ParameterList.Parameters.Select(p => p.ToString()));
            var newParamListCode = string.IsNullOrEmpty(existingParams)
                ? $"({mediatorType} {mediatorParam})"
                : $"({existingParams}, {mediatorType} {mediatorParam})";

            var paramListTree = CSharpSyntaxTree.ParseText($"class Temp {{ void M{newParamListCode} {{}} }}");
            var paramListRoot = paramListTree.GetRoot();
            var newParameterList = paramListRoot.DescendantNodes()
                .OfType<ParameterListSyntax>()
                .First();

            // Build assignment
            var assignmentCode = $"{mediatorField} = {mediatorParam};";
            var assignmentTree = CSharpSyntaxTree.ParseText($"class Temp {{ void M() {{ {assignmentCode} }} }}");
            var assignmentRoot = assignmentTree.GetRoot();
            var assignment = assignmentRoot.DescendantNodes()
                .OfType<ExpressionStatementSyntax>()
                .First();

            var newBody = existingConstructor.Body != null
                ? existingConstructor.Body.WithStatements(
                    existingConstructor.Body.Statements.Add(assignment))
                : SyntaxFactory.Block(assignment);

            var newConstructor = existingConstructor
                .WithParameterList(newParameterList)
                .WithBody(newBody);

            // Replace constructor and add field
            var newMembers = node.Members.Replace(existingConstructor, newConstructor);
            newMembers = newMembers.Insert(0, mediatorFieldDecl);

            return node.WithMembers(newMembers);
        }

        /// <summary>
        /// Builds the return type for the transformed action.
        /// </summary>
        private static TypeSyntax BuildReturnType(ActionInfo actionInfo)
        {
            // Always return Task<IActionResult>
            return SyntaxFactory.ParseTypeName("Task<IActionResult>")
                .WithTrailingTrivia(SyntaxFactory.Space);
        }

        /// <summary>
        /// Builds the parameter list for the transformed action.
        /// Converts nullable params to required params and adds [FromRoute]/[FromBody] attributes.
        /// </summary>
        private ParameterListSyntax BuildParameters(ActionInfo actionInfo)
        {
            var parameters = new List<ParameterSyntax>();

            foreach (var param in actionInfo.Parameters)
            {
                // Determine type (remove nullable for simple types)
                var paramType = param.Type;
                var isNullableValueType = param.IsNullable && IsValueType(param.Type);

                if (isNullableValueType)
                {
                    // int? → int (make required)
                    paramType = param.Type.TrimEnd('?');
                }

                var typeSyntax = SyntaxFactory.ParseTypeName(paramType)
                    .WithTrailingTrivia(SyntaxFactory.Space);

                // Build parameter
                var paramSyntax = SyntaxFactory.Parameter(
                    SyntaxFactory.Identifier(param.Name))
                    .WithType(typeSyntax);

                // Add binding source attribute
                var bindingAttribute = DetermineBindingAttribute(param, actionInfo);
                if (!string.IsNullOrEmpty(bindingAttribute))
                {
                    var attribute = SyntaxFactory.Attribute(
                        SyntaxFactory.IdentifierName(bindingAttribute));
                    var attributeList = SyntaxFactory.AttributeList(
                        SyntaxFactory.SingletonSeparatedList(attribute))
                        .WithTrailingTrivia(SyntaxFactory.Space);
                    paramSyntax = paramSyntax.WithAttributeLists(
                        SyntaxFactory.SingletonList(attributeList));

                    _requiredUsings.Add("Microsoft.AspNetCore.Mvc");
                }

                parameters.Add(paramSyntax);
            }

            return SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(parameters));
        }

        /// <summary>
        /// Determines the binding attribute for a parameter.
        /// </summary>
        private static string? DetermineBindingAttribute(ActionParameter param, ActionInfo actionInfo)
        {
            // If already has binding source, keep it
            if (!string.IsNullOrEmpty(param.BindingSource))
            {
                return param.BindingSource;
            }

            // For POST/PUT/DELETE with complex types, use FromBody
            if (actionInfo.IsCommand && IsComplexType(param.Type))
            {
                return "FromBody";
            }

            // For simple types in route (like id), use FromRoute
            if (param.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                return "FromRoute";
            }

            // Default: no attribute (FromQuery is default)
            return null;
        }

        /// <summary>
        /// Builds the method body with MediatR.Send call.
        /// </summary>
        private BlockSyntax BuildMediatorSendBody(ActionLogicContext context, string requestTypeName)
        {
            var actionInfo = context.Action;

            // Build request object initialization
            var requestInit = BuildRequestInitializer(context, requestTypeName);

            // Build Send call: await _mediator.Send(new XxxQuery { ... })
            var sendCallCode = $"var result = await _mediator.Send({requestInit});";
            var sendCallTree = CSharpSyntaxTree.ParseText($"class Temp {{ async void M() {{ {sendCallCode} }} }}");
            var sendStatement = sendCallTree.GetRoot()
                .DescendantNodes()
                .OfType<LocalDeclarationStatementSyntax>()
                .First();

            // Build return statement based on action type
            var returnStatement = BuildReturnStatement(context);

            // Build block with proper indentation
            var statements = SyntaxFactory.List(new StatementSyntax[]
            {
                sendStatement.WithLeadingTrivia(SyntaxFactory.Whitespace("        ")),
                returnStatement.WithLeadingTrivia(SyntaxFactory.Whitespace("        "))
            });

            return SyntaxFactory.Block(statements)
                .WithOpenBraceToken(
                    SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                        .WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"))
                        .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")))
                .WithCloseBraceToken(
                    SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                        .WithLeadingTrivia(SyntaxFactory.Whitespace("    ")));
        }

        /// <summary>
        /// Builds the request object initializer.
        /// </summary>
        private string BuildRequestInitializer(ActionLogicContext context, string requestTypeName)
        {
            var actionInfo = context.Action;

            if (actionInfo.Parameters.Count == 0)
            {
                return $"new {requestTypeName}()";
            }

            // Build property initializers
            var properties = actionInfo.Parameters
                .Select(p => $"{p.Name.Substring(0, 1).ToUpperInvariant()}{p.Name.Substring(1)} = {p.Name}")
                .ToList();

            if (properties.Count == 1)
            {
                return $"new {requestTypeName} {{ {properties[0]} }}";
            }

            var propsJoined = string.Join(", ", properties);
            return $"new {requestTypeName} {{ {propsJoined} }}";
        }

        /// <summary>
        /// Builds the return statement based on the action context.
        /// </summary>
        private ReturnStatementSyntax BuildReturnStatement(ActionLogicContext context)
        {
            var actionInfo = context.Action;
            var logic = context.ActionLogic;

            // Determine return based on action type and logic
            string returnExpression;

            if (logic?.ReturnStatement != null)
            {
                if (logic.ReturnStatement.IsViewReturn)
                {
                    // return result.IsSuccess ? View(result.Value) : NotFound();
                    returnExpression = "result.IsSuccess ? View(result.Value) : NotFound()";
                }
                else if (logic.ReturnStatement.IsRedirect)
                {
                    // Preserve redirect
                    returnExpression = "result.IsSuccess ? RedirectToAction(\"Index\") : BadRequest()";
                }
                else if (logic.ReturnStatement.IsErrorReturn)
                {
                    // return result.IsSuccess ? Ok() : BadRequest();
                    returnExpression = "result.IsSuccess ? Ok() : BadRequest()";
                }
                else
                {
                    // Default: return Ok or NotFound based on result
                    returnExpression = "result.IsSuccess ? Ok(result.Value) : NotFound()";
                }
            }
            else if (actionInfo.IsQuery)
            {
                // Query: return View or Ok based on controller type and return type
                var isMvcController = !context.Controller.IsApiController;
                var isViewResult = actionInfo.ReturnType.Contains("ViewResult", StringComparison.Ordinal) ||
                                   actionInfo.ReturnType.Contains("PartialViewResult", StringComparison.Ordinal) ||
                                   (actionInfo.ReturnType.Equals("ActionResult", StringComparison.Ordinal) && isMvcController);

                if (isViewResult)
                {
                    returnExpression = "result.IsSuccess ? View(result.Value) : NotFound()";
                }
                else
                {
                    returnExpression = "result.IsSuccess ? Ok(result.Value) : NotFound()";
                }
            }
            else
            {
                // Command: return Ok, Created, or redirect
                if (context.Action.Name.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
                {
                    returnExpression = "result.IsSuccess ? CreatedAtAction(nameof(Details), new { id = result.Value }, result.Value) : BadRequest()";
                }
                else if (context.Action.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase))
                {
                    returnExpression = "result.IsSuccess ? NoContent() : NotFound()";
                }
                else
                {
                    returnExpression = "result.IsSuccess ? Ok() : BadRequest()";
                }
            }

            // Add confidence warning if low
            if (context.Confidence < 80)
            {
                // Build descriptive action signature (e.g., "StudentsController.Create(Student student)")
                var paramSignature = actionInfo.Parameters.Count > 0
                    ? string.Join(", ", actionInfo.Parameters.Select(p => $"{p.Type} {p.Name}"))
                    : "";
                var actionSignature = $"{context.Controller.ClassName}.{actionInfo.Name}({paramSignature})";

                _warnings.Add(new TransformWarning
                {
                    ActionName = actionSignature,
                    Message = $"Generated with {context.Confidence}% confidence - review return logic",
                    Severity = "Info"
                });
            }

            var returnTree = CSharpSyntaxTree.ParseText($"class Temp {{ void M() {{ return {returnExpression}; }} }}");
            return returnTree.GetRoot()
                .DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .First();
        }

        /// <summary>
        /// Generates the request type name (Query or Command) with controller prefix.
        /// </summary>
        private static string GenerateRequestTypeName(ActionLogicContext context)
        {
            var controllerBaseName = context.Controller.ClassName.Replace("Controller", string.Empty);
            var actionName = context.Action.Name;
            var suffix = context.GenerateQuery ? "Query" : "Command";

            // Avoid duplicate suffix
            if (actionName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return actionName;
            }

            // Convert common action names for queries
            if (context.GenerateQuery)
            {
                if (actionName.Equals("Index", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{controllerBaseName}GetListQuery";
                }

                if (actionName.Equals("Details", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{controllerBaseName}GetByIdQuery";
                }

                // If this is a GET that has a POST overload (like GET Create with POST Create), add "Form" suffix
                if (context.Action.HasOverload)
                {
                    return $"{controllerBaseName}{actionName}FormQuery";
                }
            }

            return $"{controllerBaseName}{actionName}{suffix}";
        }

        /// <summary>
        /// Checks if a type is a value type.
        /// </summary>
        private static bool IsValueType(string typeName)
        {
            var baseType = typeName.TrimEnd('?');
            return baseType switch
            {
                "int" or "long" or "short" or "byte" or "sbyte" or
                "uint" or "ulong" or "ushort" or
                "float" or "double" or "decimal" or
                "bool" or "char" or
                "DateTime" or "DateTimeOffset" or "TimeSpan" or
                "Guid" => true,
                _ => false
            };
        }

        /// <summary>
        /// Checks if a type is a complex type.
        /// </summary>
        private static bool IsComplexType(string typeName)
        {
            var baseType = typeName.TrimEnd('?');

            // Simple types are not complex
            if (IsValueType(baseType) || baseType == "string")
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Extracts the root namespace from a full namespace.
        /// For example: "MyApp.Application.Students.Commands" -> "MyApp"
        /// </summary>
        private static string ExtractRootNamespace(string fullNamespace)
        {
            if (string.IsNullOrWhiteSpace(fullNamespace))
                return "Application";

            var parts = fullNamespace.Split('.');
            // Find where "Application" starts and return everything before it
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("Application", StringComparison.OrdinalIgnoreCase))
                {
                    return i > 0 ? string.Join(".", parts.Take(i)) : parts[0];
                }
            }

            // If no Application found, return first part
            return parts[0];
        }

        /// <summary>
        /// Adds required using directives.
        /// </summary>
        public SyntaxNode AddRequiredUsings(SyntaxNode root)
        {
            if (_requiredUsings.Count == 0)
            {
                return root;
            }

            if (root is CompilationUnitSyntax compilationUnit)
            {
                var existingUsings = compilationUnit.Usings
                    .Select(u => u.Name?.ToString())
                    .Where(n => n != null)
                    .ToHashSet(StringComparer.Ordinal);

                var newUsings = _requiredUsings
                    .Where(ns => !existingUsings.Contains(ns) && !string.IsNullOrWhiteSpace(ns))
                    .Select(ns =>
                    {
                        var usingCode = $"using {ns};";
                        var usingTree = CSharpSyntaxTree.ParseText(usingCode);
                        var parsedUsing = usingTree.GetRoot()
                            .DescendantNodes()
                            .OfType<UsingDirectiveSyntax>()
                            .First();
                        return parsedUsing.WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
                    })
                    .ToList();

                if (newUsings.Count > 0)
                {
                    return compilationUnit.AddUsings(newUsings.ToArray());
                }
            }

            return root;
        }
    }
}
