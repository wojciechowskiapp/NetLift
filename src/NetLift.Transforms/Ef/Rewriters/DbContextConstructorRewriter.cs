using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;

namespace NetLift.Transforms.Ef.Rewriters;

/// <summary>
/// Rewrites EF6 DbContext constructor patterns to EF Core constructor patterns.
/// Transforms parameterless constructors, connection string constructors, and connection string name constructors
/// to the standard EF Core pattern: DbContext(DbContextOptions&lt;T&gt; options).
/// </summary>
public sealed class DbContextConstructorRewriter : CSharpSyntaxRewriter, IDbContextConstructorRewriter
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private readonly List<DbContextConnectionStringInfo> _removedConnectionStrings = new();
    private int _lowestConfidence = 100;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public IReadOnlyCollection<DbContextConnectionStringInfo> RemovedConnectionStrings => _removedConnectionStrings;

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
        _removedConnectionStrings.Clear();
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
        rewritten = AddRequiredUsings(rewritten);

        return rewritten.ToFullString();
    }

    /// <summary>
    /// Visits class declarations to detect and transform DbContext classes.
    /// </summary>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // First visit children to handle nested classes
        var visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
        if (visited == null)
        {
            return null;
        }

        // Check if this class inherits from DbContext
        if (!InheritsFromDbContext(visited))
        {
            return visited;
        }

        // Transform the DbContext constructors
        return TransformDbContextConstructors(visited);
    }

    /// <summary>
    /// Checks if a class inherits from DbContext.
    /// </summary>
    private static bool InheritsFromDbContext(ClassDeclarationSyntax node)
    {
        if (node.BaseList == null || node.BaseList.Types.Count == 0)
        {
            return false;
        }

        // Check if any base type is DbContext
        return node.BaseList.Types.Any(baseType =>
        {
            var baseName = ExtractBaseName(baseType.Type);
            return baseName == "DbContext";
        });
    }

    /// <summary>
    /// Transforms DbContext constructors to EF Core pattern.
    /// </summary>
    private ClassDeclarationSyntax TransformDbContextConstructors(ClassDeclarationSyntax node)
    {
        var className = node.Identifier.Text;
        var constructors = node.Members.OfType<ConstructorDeclarationSyntax>().ToList();

        // If no constructors, no transformation needed (will use default constructor)
        if (constructors.Count == 0)
        {
            _diagnostics.Add(new RewriterDiagnostic(
                $"DbContext {className} has no explicit constructors - no transformation needed",
                RewriterDiagnosticSeverity.Info));
            return node;
        }

        // Handle multiple constructors
        if (constructors.Count > 1)
        {
            return TransformMultipleConstructors(node, constructors, className);
        }

        // Single constructor - transform it
        var constructor = constructors[0];
        return TransformSingleConstructor(node, constructor, className);
    }

    /// <summary>
    /// Transforms a DbContext with a single constructor.
    /// </summary>
    private ClassDeclarationSyntax TransformSingleConstructor(
        ClassDeclarationSyntax node,
        ConstructorDeclarationSyntax constructor,
        string className)
    {
        // Check if it's already the correct EF Core pattern
        if (IsEfCoreConstructor(constructor, className))
        {
            _diagnostics.Add(new RewriterDiagnostic(
                $"DbContext {className} already has EF Core constructor pattern - no transformation needed",
                RewriterDiagnosticSeverity.Info));
            return node;
        }

        // Check for connection string patterns
        var connectionStringName = ExtractConnectionStringName(constructor);
        if (connectionStringName != null)
        {
            _removedConnectionStrings.Add(new DbContextConnectionStringInfo(className, connectionStringName));
            _diagnostics.Add(new RewriterDiagnostic(
                $"Removed connection string '{connectionStringName}' from {className} - migrate to appsettings.json",
                RewriterDiagnosticSeverity.Warning));
        }

        // Check for custom logic in constructor body
        var hasCustomLogic = HasCustomLogic(constructor);
        if (hasCustomLogic)
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 75);
            _diagnostics.Add(new RewriterDiagnostic(
                $"DbContext {className} constructor has custom logic - preserved in new constructor",
                RewriterDiagnosticSeverity.Warning));
        }
        else
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 95);
        }

        // Create the new EF Core constructor
        var newConstructor = CreateEfCoreConstructor(className, constructor.Body, hasCustomLogic);

        // Replace the old constructor
        var newMembers = node.Members.Replace(constructor, newConstructor);
        var result = node.WithMembers(newMembers);

        _requiredUsings.Add("Microsoft.EntityFrameworkCore");

        _diagnostics.Add(new RewriterDiagnostic(
            $"Transformed {className} constructor to EF Core pattern",
            RewriterDiagnosticSeverity.Info));

        return result;
    }

    /// <summary>
    /// Transforms a DbContext with multiple constructors.
    /// </summary>
    private ClassDeclarationSyntax TransformMultipleConstructors(
        ClassDeclarationSyntax node,
        List<ConstructorDeclarationSyntax> constructors,
        string className)
    {
        _lowestConfidence = Math.Min(_lowestConfidence, 80);
        _diagnostics.Add(new RewriterDiagnostic(
            $"DbContext {className} has {constructors.Count} constructors - keeping design-time constructor if present",
            RewriterDiagnosticSeverity.Warning));

        // Check if one is already the EF Core pattern
        var efCoreConstructor = constructors.FirstOrDefault(c => IsEfCoreConstructor(c, className));
        if (efCoreConstructor != null)
        {
            _diagnostics.Add(new RewriterDiagnostic(
                $"DbContext {className} already has EF Core constructor - preserving all constructors",
                RewriterDiagnosticSeverity.Info));
            return node;
        }

        // Transform runtime constructor (the one with connection string or parameterless)
        // Keep design-time constructor if it looks like one
        var result = node;
        var transformedCount = 0;

        foreach (var constructor in constructors)
        {
            if (IsDesignTimeConstructor(constructor))
            {
                // Keep design-time constructor as-is
                _diagnostics.Add(new RewriterDiagnostic(
                    $"Preserved design-time constructor in {className}",
                    RewriterDiagnosticSeverity.Info));
                continue;
            }

            // Transform this constructor
            var connectionStringName = ExtractConnectionStringName(constructor);
            if (connectionStringName != null)
            {
                _removedConnectionStrings.Add(new DbContextConnectionStringInfo(className, connectionStringName));
            }

            var hasCustomLogic = HasCustomLogic(constructor);
            var newConstructor = CreateEfCoreConstructor(className, constructor.Body, hasCustomLogic);

            // Replace the constructor
            var newMembers = result.Members.Replace(constructor, newConstructor);
            result = result.WithMembers(newMembers);
            transformedCount++;
        }

        if (transformedCount > 0)
        {
            _requiredUsings.Add("Microsoft.EntityFrameworkCore");
            _diagnostics.Add(new RewriterDiagnostic(
                $"Transformed {transformedCount} constructor(s) to EF Core pattern in {className}",
                RewriterDiagnosticSeverity.Info));
        }

        return result;
    }

    /// <summary>
    /// Creates an EF Core constructor with DbContextOptions parameter.
    /// </summary>
    private ConstructorDeclarationSyntax CreateEfCoreConstructor(
        string className,
        BlockSyntax? originalBody,
        bool hasCustomLogic)
    {
        var optionsType = $"DbContextOptions<{className}>";
        var paramName = "options";

        // Parse constructor from template to get proper formatting
        var constructorCode = hasCustomLogic && originalBody != null
            ? $@"public {className}({optionsType} {paramName}) : base({paramName})
    {originalBody.ToFullString()}"
            : $@"public {className}({optionsType} {paramName}) : base({paramName})
    {{
    }}";

        var constructorTree = CSharpSyntaxTree.ParseText($"class Temp {{ {constructorCode} }}");
        var constructorRoot = constructorTree.GetRoot();
        var constructor = constructorRoot.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .First()
            .WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace("    "))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        return constructor;
    }

    /// <summary>
    /// Checks if a constructor is already the EF Core pattern.
    /// </summary>
    private static bool IsEfCoreConstructor(ConstructorDeclarationSyntax constructor, string className)
    {
        // Check parameter list
        if (constructor.ParameterList.Parameters.Count != 1)
        {
            return false;
        }

        var param = constructor.ParameterList.Parameters[0];
        var paramType = param.Type?.ToString() ?? string.Empty;

        // Check if parameter type is DbContextOptions<ClassName> or DbContextOptions
        return paramType.Contains("DbContextOptions", StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if a constructor looks like a design-time constructor (parameterless for migrations).
    /// </summary>
    private static bool IsDesignTimeConstructor(ConstructorDeclarationSyntax constructor)
    {
        // Design-time constructors are typically parameterless and call base with a connection string
        if (constructor.ParameterList.Parameters.Count != 0)
        {
            return false;
        }

        // Check if it has a base initializer with connection string
        if (constructor.Initializer == null)
        {
            return false;
        }

        // If it calls base with arguments, it's likely a design-time constructor
        return constructor.Initializer.ArgumentList.Arguments.Count > 0;
    }

    /// <summary>
    /// Extracts connection string name from base() initializer if present.
    /// </summary>
    private static string? ExtractConnectionStringName(ConstructorDeclarationSyntax constructor)
    {
        if (constructor.Initializer == null || constructor.Initializer.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        var firstArg = constructor.Initializer.ArgumentList.Arguments[0];
        var expression = firstArg.Expression;

        // Check for string literal like "name=DefaultConnection"
        if (expression is LiteralExpressionSyntax literal &&
            literal.Token.ValueText is string value &&
            value.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(5); // Remove "name=" prefix
        }

        return null;
    }

    /// <summary>
    /// Checks if a constructor has custom logic beyond just calling base.
    /// </summary>
    private static bool HasCustomLogic(ConstructorDeclarationSyntax constructor)
    {
        if (constructor.Body == null)
        {
            return false;
        }

        // If there are any statements in the body, it has custom logic
        return constructor.Body.Statements.Count > 0;
    }

    /// <summary>
    /// Extracts the base class name from a type syntax node.
    /// Handles both simple names (DbContext) and qualified names (System.Data.Entity.DbContext).
    /// </summary>
    private static string? ExtractBaseName(TypeSyntax type)
    {
        return type switch
        {
            // Simple name: DbContext
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,

            // Qualified name: System.Data.Entity.DbContext -> DbContext
            QualifiedNameSyntax qualifiedName => GetRightmostIdentifier(qualifiedName),

            _ => null
        };
    }

    /// <summary>
    /// Gets the rightmost identifier from a qualified name.
    /// Example: System.Data.Entity.DbContext -> DbContext
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
    /// Visits identifier names to rename EF6-specific types to EF Core equivalents.
    /// Example: DbModelBuilder → ModelBuilder
    /// </summary>
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var visited = (IdentifierNameSyntax?)base.VisitIdentifierName(node);
        if (visited == null)
        {
            return null;
        }

        // Rename DbModelBuilder → ModelBuilder
        if (visited.Identifier.Text == "DbModelBuilder")
        {
            _diagnostics.Add(new RewriterDiagnostic(
                "Renamed DbModelBuilder to ModelBuilder for EF Core compatibility",
                RewriterDiagnosticSeverity.Info));

            return SyntaxFactory.IdentifierName("ModelBuilder")
                .WithTriviaFrom(visited);
        }

        // Rename PluralizingTableNameConvention → (removed - EF Core doesn't have this)
        // Note: This is handled by removing the convention code, not renaming

        return visited;
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
                    // Parse complete using directive to ensure proper spacing
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
