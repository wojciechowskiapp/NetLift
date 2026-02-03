using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Common;
using NetLift.Transforms.Mvc.Configuration;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Rewrites controller base classes from System.Web.Mvc/Http to ASP.NET Core equivalents.
/// Handles Controller, ApiController, custom base controllers, and adds ILogger injection.
/// </summary>
public sealed class ControllerBaseClassRewriter : CSharpSyntaxRewriter, IControllerBaseRewriter
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private readonly List<DependencyInfo> _addedDependencies = new();
    private int _lowestConfidence = 100;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public IReadOnlyCollection<DependencyInfo> AddedDependencies => _addedDependencies;

    /// <inheritdoc />
    public string Rewrite(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return sourceCode;
        }

        // Reset state
        _requiredUsings.Clear();
        _diagnostics.Clear();
        _addedDependencies.Clear();
        _lowestConfidence = 100;

        // Parse the source code
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // Rewrite the tree
        var rewritten = Visit(root);

        if (rewritten == null)
        {
            return sourceCode;
        }

        // Add new using directives
        if (rewritten is CompilationUnitSyntax compilationUnit)
        {
            rewritten = compilationUnit.AddRequiredUsings(_requiredUsings);
        }

        return rewritten.ToFullString();
    }

    /// <summary>
    /// Visits class declarations to detect and transform controller classes.
    /// </summary>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // First visit children to handle nested classes
        var visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
        if (visited == null)
        {
            return null;
        }

        // Check if this class has a base class
        if (visited.BaseList == null || visited.BaseList.Types.Count == 0)
        {
            return visited;
        }

        // Get the first base type (base class, not interfaces)
        var firstBaseType = visited.BaseList.Types[0];
        var baseName = ExtractBaseName(firstBaseType.Type);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            return visited;
        }

        // Check if this is a known controller base class or custom controller
        var mapping = ControllerBaseMappings.GetMapping(baseName);
        var isCustomController = ControllerBaseMappings.IsCustomBaseController(baseName);

        if (mapping == null && !isCustomController)
        {
            return visited;
        }

        // Transform the controller
        var transformed = visited;

        if (mapping != null)
        {
            // Known base class mapping (Controller or ApiController)
            transformed = TransformKnownController(transformed, baseName, mapping);
        }
        else if (isCustomController)
        {
            // Custom base controller - just update the base class to Controller
            transformed = TransformCustomController(transformed, baseName);
        }

        // Add ILogger constructor injection
        transformed = AddLoggerInjection(transformed);

        // Detect async methods and add System.Threading.Tasks using
        if (HasAsyncMethods(transformed))
        {
            _requiredUsings.Add("System.Threading.Tasks");
        }

        // Detect [Authorize] attribute and add Authorization using
        if (HasAuthorizeAttribute(transformed))
        {
            _requiredUsings.Add("Microsoft.AspNetCore.Authorization");
        }

        // Transform [ChildActionOnly] to TODO comment (doesn't exist in Core)
        transformed = TransformChildActionOnlyAttribute(transformed);

        return transformed;
    }

    /// <summary>
    /// Checks if the class has any async methods (return Task or Task&lt;T&gt;).
    /// </summary>
    private static bool HasAsyncMethods(ClassDeclarationSyntax node)
    {
        return node.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.ReturnType.ToString().StartsWith("Task", StringComparison.Ordinal) ||
                      m.Modifiers.Any(SyntaxKind.AsyncKeyword));
    }

    /// <summary>
    /// Checks if the class or any of its methods has [Authorize] attribute.
    /// </summary>
    private static bool HasAuthorizeAttribute(ClassDeclarationSyntax node)
    {
        // Check class-level attributes
        if (HasAttribute(node, "Authorize"))
        {
            return true;
        }

        // Check method-level attributes
        return node.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().Equals("Authorize", StringComparison.Ordinal) ||
                          a.Name.ToString().Equals("AuthorizeAttribute", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Transforms [ChildActionOnly] attributes to TODO comments since this doesn't exist in Core.
    /// In ASP.NET Core, child actions should be converted to ViewComponents.
    /// </summary>
    private ClassDeclarationSyntax TransformChildActionOnlyAttribute(ClassDeclarationSyntax node)
    {
        var hasChildAction = false;
        var newMembers = new List<MemberDeclarationSyntax>();

        foreach (var member in node.Members)
        {
            if (member is MethodDeclarationSyntax method)
            {
                var childActionAttr = method.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(a => a.Name.ToString().Equals("ChildActionOnly", StringComparison.Ordinal) ||
                                         a.Name.ToString().Equals("ChildActionOnlyAttribute", StringComparison.Ordinal));

                if (childActionAttr != null)
                {
                    hasChildAction = true;
                    // Remove the [ChildActionOnly] attribute and add a TODO comment
                    var newMethod = RemoveAttribute(method, "ChildActionOnly");

                    // Add TODO comment as leading trivia
                    var todoComment = SyntaxFactory.Comment("// TODO: [ChildActionOnly] removed - convert to ViewComponent for ASP.NET Core\n    ");
                    var existingTrivia = newMethod.GetLeadingTrivia();
                    newMethod = newMethod.WithLeadingTrivia(existingTrivia.Add(todoComment));

                    newMembers.Add(newMethod);
                    continue;
                }
            }
            newMembers.Add(member);
        }

        if (hasChildAction)
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 70);
            _diagnostics.Add(new RewriterDiagnostic(
                "[ChildActionOnly] attribute removed - convert these actions to ViewComponents",
                RewriterDiagnosticSeverity.Warning));
        }

        return node.WithMembers(SyntaxFactory.List(newMembers));
    }

    /// <summary>
    /// Removes a specific attribute from a method.
    /// </summary>
    private static MethodDeclarationSyntax RemoveAttribute(MethodDeclarationSyntax method, string attributeName)
    {
        var newAttributeLists = new List<AttributeListSyntax>();

        foreach (var attrList in method.AttributeLists)
        {
            var remainingAttrs = attrList.Attributes
                .Where(a => !a.Name.ToString().Equals(attributeName, StringComparison.Ordinal) &&
                            !a.Name.ToString().Equals(attributeName + "Attribute", StringComparison.Ordinal))
                .ToList();

            if (remainingAttrs.Count > 0)
            {
                var newList = attrList.WithAttributes(SyntaxFactory.SeparatedList(remainingAttrs));
                newAttributeLists.Add(newList);
            }
        }

        return method.WithAttributeLists(SyntaxFactory.List(newAttributeLists));
    }

    /// <summary>
    /// Transforms a controller with a known base class mapping.
    /// </summary>
    private ClassDeclarationSyntax TransformKnownController(
        ClassDeclarationSyntax node,
        string originalBaseName,
        ControllerBaseMapping mapping)
    {
        var result = node;

        // Update confidence score
        _lowestConfidence = Math.Min(_lowestConfidence, mapping.ConfidenceScore);

        // Always update base class to ensure qualified names are replaced with simple names
        // and ApiController is replaced with ControllerBase
        var originalBaseType = result.BaseList?.Types[0].Type.ToString() ?? string.Empty;
        var needsUpdate = originalBaseName != mapping.NewBaseName ||
                          originalBaseType.Contains(".", StringComparison.Ordinal);

        if (needsUpdate)
        {
            result = UpdateBaseClass(result, mapping.NewBaseName);
            _requiredUsings.Add("Microsoft.AspNetCore.Mvc");

            _diagnostics.Add(new RewriterDiagnostic(
                $"Updated base class: '{originalBaseName}' → '{mapping.NewBaseName}'",
                RewriterDiagnosticSeverity.Info));
        }

        // Add [ApiController] attribute if needed
        if (mapping.RequiresApiControllerAttribute && !HasAttribute(result, "ApiController"))
        {
            result = AddAttribute(result, "ApiController");
            _requiredUsings.Add("Microsoft.AspNetCore.Mvc");

            _diagnostics.Add(new RewriterDiagnostic(
                $"Added [ApiController] attribute to {node.Identifier.Text}",
                RewriterDiagnosticSeverity.Info));
        }

        // Add [Route] attribute if needed
        if (mapping.RequiresRouteAttribute && !HasAttribute(result, "Route"))
        {
            result = AddRouteAttribute(result);
            _requiredUsings.Add("Microsoft.AspNetCore.Mvc");

            _diagnostics.Add(new RewriterDiagnostic(
                $"Added [Route(\"api/[controller]\")] attribute to {node.Identifier.Text}",
                RewriterDiagnosticSeverity.Info));
        }

        return result;
    }

    /// <summary>
    /// Transforms a custom base controller by updating its base class to Controller.
    /// </summary>
    private ClassDeclarationSyntax TransformCustomController(ClassDeclarationSyntax node, string baseName)
    {
        // For custom controllers, we keep the name but update the base to Controller
        _lowestConfidence = Math.Min(_lowestConfidence, ControllerBaseMappings.GetCustomBaseControllerConfidence());

        _diagnostics.Add(new RewriterDiagnostic(
            $"Detected custom base controller: '{baseName}' (will inherit from Controller)",
            RewriterDiagnosticSeverity.Info));

        // Note: We don't change the custom controller's base here - that would require
        // analyzing the custom controller's definition, which might be in another file
        return node;
    }

    /// <summary>
    /// Adds ILogger constructor injection to a controller.
    /// </summary>
    private ClassDeclarationSyntax AddLoggerInjection(ClassDeclarationSyntax node)
    {
        var className = node.Identifier.Text;
        var loggerType = $"ILogger<{className}>";
        var loggerParam = "logger";
        var loggerField = "_logger";

        // Check if ILogger is already injected
        if (HasLoggerField(node))
        {
            _diagnostics.Add(new RewriterDiagnostic(
                $"Controller {className} already has ILogger field, skipping injection",
                RewriterDiagnosticSeverity.Info));
            return node;
        }

        // Get existing constructors
        var constructors = node.Members.OfType<ConstructorDeclarationSyntax>().ToList();

        if (constructors.Count == 0)
        {
            // No constructor - add new one with ILogger
            return AddNewConstructorWithLogger(node, className, loggerType, loggerParam, loggerField);
        }
        else if (constructors.Count == 1)
        {
            // Single constructor - add ILogger to it
            return AddLoggerToExistingConstructor(node, constructors[0], loggerType, loggerParam, loggerField);
        }
        else
        {
            // Multiple constructors - lower confidence, add logger to primary constructor
            _lowestConfidence = Math.Min(_lowestConfidence, 60);
            _diagnostics.Add(new RewriterDiagnostic(
                $"Controller {className} has multiple constructors - adding ILogger to first constructor only",
                RewriterDiagnosticSeverity.Warning));

            return AddLoggerToExistingConstructor(node, constructors[0], loggerType, loggerParam, loggerField);
        }
    }

    /// <summary>
    /// Adds a new constructor with ILogger injection.
    /// </summary>
    private ClassDeclarationSyntax AddNewConstructorWithLogger(
        ClassDeclarationSyntax node,
        string className,
        string loggerType,
        string loggerParam,
        string loggerField)
    {
        // Parse field and constructor from template strings to get proper formatting
        var fieldCode = $"private readonly {loggerType} {loggerField};";
        var fieldTree = CSharpSyntaxTree.ParseText($"class Temp {{ {fieldCode} }}");
        var fieldRoot = fieldTree.GetRoot();
        var loggerFieldDecl = fieldRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .First()
            .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        // Parse constructor from template
        var constructorCode = $@"public {className}({loggerType} {loggerParam})
    {{
        {loggerField} = {loggerParam};
    }}";
        var constructorTree = CSharpSyntaxTree.ParseText($"class Temp {{ {constructorCode} }}");
        var constructorRoot = constructorTree.GetRoot();
        var constructor = constructorRoot.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .First()
            .WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace("    "))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        // Add members to class
        var newMembers = node.Members.Insert(0, loggerFieldDecl).Insert(1, constructor);
        var result = node.WithMembers(newMembers);

        // Track the added dependency
        _addedDependencies.Add(new DependencyInfo(loggerType, loggerParam, loggerField));
        _requiredUsings.Add("Microsoft.Extensions.Logging");

        _diagnostics.Add(new RewriterDiagnostic(
            $"Added ILogger constructor injection to {className}",
            RewriterDiagnosticSeverity.Info));

        return result;
    }

    /// <summary>
    /// Adds ILogger parameter to an existing constructor.
    /// </summary>
    private ClassDeclarationSyntax AddLoggerToExistingConstructor(
        ClassDeclarationSyntax node,
        ConstructorDeclarationSyntax existingConstructor,
        string loggerType,
        string loggerParam,
        string loggerField)
    {
        var className = node.Identifier.Text;

        // Parse field from template to get proper formatting
        var fieldCode = $"private readonly {loggerType} {loggerField};";
        var fieldTree = CSharpSyntaxTree.ParseText($"class Temp {{ {fieldCode} }}");
        var fieldRoot = fieldTree.GetRoot();
        var loggerFieldDecl = fieldRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .First()
            .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        // Build new parameter list with proper spacing by parsing from template
        var existingParams = string.Join(", ", existingConstructor.ParameterList.Parameters.Select(p => p.ToString()));
        var newParamListCode = string.IsNullOrEmpty(existingParams)
            ? $"({loggerType} {loggerParam})"
            : $"({existingParams}, {loggerType} {loggerParam})";

        var paramListTree = CSharpSyntaxTree.ParseText($"class Temp {{ void M{newParamListCode} {{}} }}");
        var paramListRoot = paramListTree.GetRoot();
        var newParameterList = paramListRoot.DescendantNodes()
            .OfType<ParameterListSyntax>()
            .First();

        // Parse assignment from template to get proper formatting
        var assignmentCode = $"{loggerField} = {loggerParam};";
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

        // Replace constructor in class members
        var newMembers = node.Members.Replace(existingConstructor, newConstructor);

        // Add logger field at the beginning
        newMembers = newMembers.Insert(0, loggerFieldDecl);

        var result = node.WithMembers(newMembers);

        // Track the added dependency
        _addedDependencies.Add(new DependencyInfo(loggerType, loggerParam, loggerField));
        _requiredUsings.Add("Microsoft.Extensions.Logging");

        _diagnostics.Add(new RewriterDiagnostic(
            $"Added ILogger parameter to existing constructor in {className}",
            RewriterDiagnosticSeverity.Info));

        return result;
    }

    /// <summary>
    /// Updates the base class of a class declaration.
    /// </summary>
    private static ClassDeclarationSyntax UpdateBaseClass(ClassDeclarationSyntax node, string newBaseName)
    {
        if (node.BaseList == null || node.BaseList.Types.Count == 0)
        {
            return node;
        }

        // Get the old base type to preserve trivia
        var oldBaseType = node.BaseList.Types[0];

        var newBaseType = SyntaxFactory.SimpleBaseType(
            SyntaxFactory.IdentifierName(newBaseName))
            .WithTriviaFrom(oldBaseType);

        var newTypes = node.BaseList.Types.Replace(
            oldBaseType,
            newBaseType);

        return node.WithBaseList(
            node.BaseList.WithTypes(newTypes));
    }

    /// <summary>
    /// Adds an attribute to a class declaration.
    /// </summary>
    private static ClassDeclarationSyntax AddAttribute(ClassDeclarationSyntax node, string attributeName)
    {
        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName(attributeName));

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        return node.WithAttributeLists(
            node.AttributeLists.Add(attributeList));
    }

    /// <summary>
    /// Adds a [Route("api/[controller]")] attribute to a class declaration.
    /// </summary>
    private static ClassDeclarationSyntax AddRouteAttribute(ClassDeclarationSyntax node)
    {
        var routeArgument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal("api/[controller]")));

        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("Route"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(routeArgument)));

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        return node.WithAttributeLists(
            node.AttributeLists.Add(attributeList));
    }

    /// <summary>
    /// Checks if a class has a specific attribute.
    /// </summary>
    private static bool HasAttribute(ClassDeclarationSyntax node, string attributeName)
    {
        return node.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Equals(attributeName, StringComparison.Ordinal) ||
                      a.Name.ToString().Equals(attributeName + "Attribute", StringComparison.Ordinal));
    }

    /// <summary>
    /// Checks if a class has an ILogger field.
    /// </summary>
    private static bool HasLoggerField(ClassDeclarationSyntax node)
    {
        return node.Members
            .OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Type.ToString().StartsWith("ILogger", StringComparison.Ordinal));
    }

    /// <summary>
    /// Extracts the base class name from a type syntax node.
    /// Handles both simple names (Controller) and qualified names (System.Web.Mvc.Controller).
    /// </summary>
    private static string? ExtractBaseName(TypeSyntax type)
    {
        return type switch
        {
            // Simple name: Controller
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,

            // Qualified name: System.Web.Mvc.Controller -> Controller
            // or Namespace.ApiController -> ApiController
            QualifiedNameSyntax qualifiedName => GetRightmostIdentifier(qualifiedName),

            _ => null
        };
    }

    /// <summary>
    /// Gets the rightmost identifier from a qualified name.
    /// Example: System.Web.Mvc.Controller -> Controller
    /// </summary>
    private static string GetRightmostIdentifier(QualifiedNameSyntax qualifiedName)
    {
        // The right side of a QualifiedNameSyntax is always a SimpleNameSyntax
        // which contains the rightmost identifier
        // For A.B.C, the structure is: QualifiedName(QualifiedName(A, B), C)
        // So qualifiedName.Right gives us C directly
        return qualifiedName.Right.Identifier.Text;
    }

    /// <summary>
    /// Visits attribute lists to remove empty attribute lists that result from removing attributes.
    /// </summary>
    public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
    {
        var visited = (AttributeListSyntax?)base.VisitAttributeList(node);
        if (visited == null)
        {
            return null;
        }

        // If all attributes were removed, remove the entire attribute list
        if (visited.Attributes.Count == 0)
        {
            return null;
        }

        return visited;
    }

    /// <summary>
    /// Visits attribute syntax to transform [Bind(Include = "...")] to [Bind("...")].
    /// Also handles [Bind(Exclude = "...")] by removing the attribute (no Core equivalent).
    /// In ASP.NET Core, Bind attribute uses positional argument instead of named Include parameter.
    /// </summary>
    public override SyntaxNode? VisitAttribute(AttributeSyntax node)
    {
        var visited = (AttributeSyntax?)base.VisitAttribute(node);
        if (visited == null)
        {
            return null;
        }

        // Check if this is a Bind attribute
        var name = visited.Name.ToString();
        if (!name.Equals("Bind", StringComparison.Ordinal) &&
            !name.Equals("BindAttribute", StringComparison.Ordinal))
        {
            return visited;
        }

        // Check if it has named arguments
        var argumentList = visited.ArgumentList;
        if (argumentList == null || argumentList.Arguments.Count == 0)
        {
            return visited;
        }

        // Find the Exclude argument - if present, remove the entire attribute
        var excludeArg = argumentList.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == "Exclude");

        if (excludeArg != null)
        {
            // [Bind(Exclude = "...")] has no direct equivalent in ASP.NET Core
            // Remove the attribute and log a warning
            _lowestConfidence = Math.Min(_lowestConfidence, 60);
            _diagnostics.Add(new RewriterDiagnostic(
                "[Bind(Exclude = \"...\")] removed - use [BindNever] attribute on excluded properties instead",
                RewriterDiagnosticSeverity.Warning));

            // Return null to remove this attribute entirely
            return null;
        }

        // Find the Include argument
        var includeArg = argumentList.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == "Include");

        if (includeArg == null)
        {
            return visited;
        }

        // Transform [Bind(Include = "...")] to [Bind("...")]
        var newArg = SyntaxFactory.AttributeArgument(includeArg.Expression);
        var newArgumentList = SyntaxFactory.AttributeArgumentList(
            SyntaxFactory.SingletonSeparatedList(newArg));

        var result = visited.WithArgumentList(newArgumentList);

        _diagnostics.Add(new RewriterDiagnostic(
            "Transformed [Bind(Include = \"...\")] to [Bind(\"...\")] for ASP.NET Core compatibility",
            RewriterDiagnosticSeverity.Info));

        return result;
    }

}
