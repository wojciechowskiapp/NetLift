using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Mvc.Configuration;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Rewrites HttpContext.Current usages from System.Web to ASP.NET Core patterns.
/// In controller classes, uses base properties (User, Request, Response).
/// In non-controller classes, adds IHttpContextAccessor injection.
/// </summary>
public sealed class HttpContextCurrentRewriter : CSharpSyntaxRewriter, IHttpContextRewriter
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private readonly HashSet<string> _classesNeedingAccessor = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClassContext> _classContexts = new(StringComparer.Ordinal);
    private int _lowestConfidence = 100;
    private string? _currentClassName;
    private bool _currentClassIsController;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public bool RequiresHttpContextAccessor => _classesNeedingAccessor.Count > 0;

    /// <inheritdoc />
    public IReadOnlyCollection<string> ClassesNeedingAccessor => _classesNeedingAccessor;

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
        _classesNeedingAccessor.Clear();
        _classContexts.Clear();
        _lowestConfidence = 100;
        _currentClassName = null;
        _currentClassIsController = false;

        // Parse the source code
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // First pass: detect HttpContext.Current usages and determine class contexts
        var rewritten = Visit(root);

        if (rewritten == null)
        {
            return sourceCode;
        }

        // Second pass: add IHttpContextAccessor injection to classes that need it
        rewritten = AddHttpContextAccessorInjection(rewritten);

        // Add new using directives
        rewritten = AddRequiredUsings(rewritten);

        return rewritten.ToFullString();
    }

    /// <summary>
    /// Visits class declarations to determine if they are controllers.
    /// </summary>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var previousClassName = _currentClassName;
        var previousIsController = _currentClassIsController;

        _currentClassName = node.Identifier.Text;
        _currentClassIsController = IsControllerClass(node);

        // Store class context
        if (!_classContexts.ContainsKey(_currentClassName))
        {
            _classContexts[_currentClassName] = new ClassContext(
                node,
                _currentClassIsController,
                HasHttpContextAccessorField(node));
        }

        // Visit children
        var visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);

        // Restore previous context
        _currentClassName = previousClassName;
        _currentClassIsController = previousIsController;

        return visited;
    }

    /// <summary>
    /// Visits member access expressions to detect and transform HttpContext.Current usages.
    /// </summary>
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Check if the expression (left side) of this node is HttpContext.Current
        // This means the current node represents HttpContext.Current.PropertyName
        if (node.Expression is MemberAccessExpressionSyntax expressionMember &&
            IsHttpContextCurrent(expressionMember))
        {
            // This node is HttpContext.Current.SomeProperty (e.g., HttpContext.Current.User)
            // Replace this entire node with the mapped property
            var propertyName = node.Name.Identifier.Text;
            return ReplaceHttpContextProperty(node, propertyName);
        }

        // Check if this node itself IS HttpContext.Current (direct access without property)
        if (IsHttpContextCurrent(node))
        {
            // Direct access to HttpContext.Current
            return ReplaceHttpContextProperty(node, "");
        }

        // Not an HttpContext.Current usage, visit children normally
        return base.VisitMemberAccessExpression(node);
    }

    /// <summary>
    /// Replaces an HttpContext.Current property access with the appropriate mapping.
    /// </summary>
    private SyntaxNode ReplaceHttpContextProperty(MemberAccessExpressionSyntax node, string propertyName)
    {
        // Get the mapping for this property
        var mapping = HttpContextMappings.GetMapping(propertyName);
        if (mapping == null)
        {
            _diagnostics.Add(new RewriterDiagnostic(
                $"Unknown HttpContext.Current.{propertyName} usage - manual review required",
                RewriterDiagnosticSeverity.Warning));
            _lowestConfidence = Math.Min(_lowestConfidence, 40);
            // Visit children and return as-is
            return (SyntaxNode?)base.VisitMemberAccessExpression(node) ?? node;
        }

        // Update confidence score
        _lowestConfidence = Math.Min(_lowestConfidence, mapping.ConfidenceScore);

        // Determine replacement based on whether we're in a controller and if property is available on controller
        // Session and Items always require accessor, even in controllers
        bool useControllerProperty = _currentClassIsController &&
                                      !string.IsNullOrEmpty(propertyName) &&
                                      HttpContextMappings.IsControllerBaseProperty(propertyName);

        // Special case: Direct HttpContext.Current (empty propertyName) uses controller property in controllers
        if (_currentClassIsController && string.IsNullOrEmpty(propertyName))
        {
            useControllerProperty = true;
        }

        string replacement;
        if (useControllerProperty)
        {
            replacement = mapping.ControllerProperty;
            var displayName = string.IsNullOrEmpty(propertyName) ? "HttpContext.Current" : $"HttpContext.Current.{propertyName}";
            _diagnostics.Add(new RewriterDiagnostic(
                $"Replaced {displayName} with {replacement} in controller {_currentClassName}",
                RewriterDiagnosticSeverity.Info));
        }
        else
        {
            replacement = mapping.AccessorProperty;
            if (_currentClassName != null)
            {
                _classesNeedingAccessor.Add(_currentClassName);
                _requiredUsings.Add("Microsoft.AspNetCore.Http");
            }
            var displayName = string.IsNullOrEmpty(propertyName) ? "HttpContext.Current" : $"HttpContext.Current.{propertyName}";
            _diagnostics.Add(new RewriterDiagnostic(
                $"Replaced {displayName} with {replacement} in {_currentClassName ?? "unknown class"}",
                RewriterDiagnosticSeverity.Info));
        }

        // Parse the replacement expression and preserve trivia
        var replacementExpr = SyntaxFactory.ParseExpression(replacement)
            .WithTriviaFrom(node);

        return replacementExpr;
    }

    /// <summary>
    /// Checks if a member access expression is HttpContext.Current.
    /// </summary>
    private static bool IsHttpContextCurrent(MemberAccessExpressionSyntax node)
    {
        // Check for .Current
        if (node.Name.Identifier.Text != "Current")
        {
            return false;
        }

        // Check if the left side is HttpContext or System.Web.HttpContext
        var leftSide = node.Expression.ToString();
        return leftSide == "HttpContext" ||
               leftSide == "System.Web.HttpContext";
    }

    /// <summary>
    /// Checks if a class is a controller by examining its base class.
    /// </summary>
    private static bool IsControllerClass(ClassDeclarationSyntax node)
    {
        if (node.BaseList == null || node.BaseList.Types.Count == 0)
        {
            return false;
        }

        var baseName = node.BaseList.Types[0].Type.ToString();
        return baseName.EndsWith("Controller", StringComparison.Ordinal) ||
               baseName.EndsWith("ControllerBase", StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if a class already has an IHttpContextAccessor field.
    /// </summary>
    private static bool HasHttpContextAccessorField(ClassDeclarationSyntax node)
    {
        return node.Members
            .OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Type.ToString().Contains("IHttpContextAccessor", StringComparison.Ordinal));
    }

    /// <summary>
    /// Adds IHttpContextAccessor injection to classes that need it.
    /// </summary>
    private SyntaxNode AddHttpContextAccessorInjection(SyntaxNode root)
    {
        if (_classesNeedingAccessor.Count == 0)
        {
            return root;
        }

        var rewritten = root;

        foreach (var className in _classesNeedingAccessor)
        {
            if (!_classContexts.TryGetValue(className, out var context))
            {
                continue;
            }

            // Skip if already has IHttpContextAccessor
            if (context.HasHttpContextAccessor)
            {
                _diagnostics.Add(new RewriterDiagnostic(
                    $"Class {className} already has IHttpContextAccessor field, skipping injection",
                    RewriterDiagnosticSeverity.Info));
                continue;
            }

            // Find the class in the new tree
            var classNode = rewritten.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (classNode == null)
            {
                continue;
            }

            var transformed = AddHttpContextAccessorToClass(classNode);
            rewritten = rewritten.ReplaceNode(classNode, transformed);

            _diagnostics.Add(new RewriterDiagnostic(
                $"Added IHttpContextAccessor injection to {className}",
                RewriterDiagnosticSeverity.Info));
        }

        return rewritten;
    }

    /// <summary>
    /// Adds IHttpContextAccessor field and constructor parameter to a class.
    /// </summary>
    private ClassDeclarationSyntax AddHttpContextAccessorToClass(ClassDeclarationSyntax node)
    {
        const string accessorType = "IHttpContextAccessor";
        const string accessorParam = "httpContextAccessor";
        const string accessorField = "_httpContextAccessor";

        // Parse field from template
        var fieldCode = $"private readonly {accessorType} {accessorField};";
        var fieldTree = CSharpSyntaxTree.ParseText($"class Temp {{ {fieldCode} }}");
        var fieldRoot = fieldTree.GetRoot();
        var accessorFieldDecl = fieldRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .First()
            .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        // Get existing constructors
        var constructors = node.Members.OfType<ConstructorDeclarationSyntax>().ToList();

        ClassDeclarationSyntax result;

        if (constructors.Count == 0)
        {
            // No constructor - add new one with IHttpContextAccessor
            result = AddNewConstructorWithAccessor(node, accessorType, accessorParam, accessorField, accessorFieldDecl);
        }
        else if (constructors.Count == 1)
        {
            // Single constructor - add IHttpContextAccessor to it
            result = AddAccessorToExistingConstructor(node, constructors[0], accessorType, accessorParam, accessorField, accessorFieldDecl);
        }
        else
        {
            // Multiple constructors - lower confidence, add to first constructor
            _lowestConfidence = Math.Min(_lowestConfidence, 70);
            _diagnostics.Add(new RewriterDiagnostic(
                $"Class {node.Identifier.Text} has multiple constructors - adding IHttpContextAccessor to first constructor only",
                RewriterDiagnosticSeverity.Warning));

            result = AddAccessorToExistingConstructor(node, constructors[0], accessorType, accessorParam, accessorField, accessorFieldDecl);
        }

        return result;
    }

    /// <summary>
    /// Adds a new constructor with IHttpContextAccessor injection.
    /// </summary>
    private ClassDeclarationSyntax AddNewConstructorWithAccessor(
        ClassDeclarationSyntax node,
        string accessorType,
        string accessorParam,
        string accessorField,
        FieldDeclarationSyntax fieldDecl)
    {
        var className = node.Identifier.Text;

        // Parse constructor from template
        var constructorCode = $@"public {className}({accessorType} {accessorParam})
    {{
        {accessorField} = {accessorParam};
    }}";
        var constructorTree = CSharpSyntaxTree.ParseText($"class Temp {{ {constructorCode} }}");
        var constructorRoot = constructorTree.GetRoot();
        var constructor = constructorRoot.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .First()
            .WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace("    "))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        // Add members to class
        var newMembers = node.Members.Insert(0, fieldDecl).Insert(1, constructor);
        return node.WithMembers(newMembers);
    }

    /// <summary>
    /// Adds IHttpContextAccessor parameter to an existing constructor.
    /// </summary>
    private ClassDeclarationSyntax AddAccessorToExistingConstructor(
        ClassDeclarationSyntax node,
        ConstructorDeclarationSyntax existingConstructor,
        string accessorType,
        string accessorParam,
        string accessorField,
        FieldDeclarationSyntax fieldDecl)
    {
        // Build new parameter list
        var existingParams = string.Join(", ", existingConstructor.ParameterList.Parameters.Select(p => p.ToString()));
        var newParamListCode = string.IsNullOrEmpty(existingParams)
            ? $"({accessorType} {accessorParam})"
            : $"({existingParams}, {accessorType} {accessorParam})";

        var paramListTree = CSharpSyntaxTree.ParseText($"class Temp {{ void M{newParamListCode} {{}} }}");
        var paramListRoot = paramListTree.GetRoot();
        var newParameterList = paramListRoot.DescendantNodes()
            .OfType<ParameterListSyntax>()
            .First();

        // Parse assignment
        var assignmentCode = $"{accessorField} = {accessorParam};";
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

        // Add accessor field at the beginning
        newMembers = newMembers.Insert(0, fieldDecl);

        return node.WithMembers(newMembers);
    }

    /// <summary>
    /// Adds required using directives that were identified during rewriting.
    /// </summary>
    private SyntaxNode AddRequiredUsings(SyntaxNode root)
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

    /// <summary>
    /// Holds context information about a class being processed.
    /// </summary>
    private sealed record ClassContext(
        ClassDeclarationSyntax Node,
        bool IsController,
        bool HasHttpContextAccessor);
}
