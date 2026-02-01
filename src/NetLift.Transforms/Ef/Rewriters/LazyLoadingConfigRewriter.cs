using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;

namespace NetLift.Transforms.Ef.Rewriters;

/// <summary>
/// Rewrites EF6 lazy loading configuration to EF Core patterns.
/// EF6 has lazy loading ON by default, EF Core has it OFF by default.
/// Removes Configuration.LazyLoadingEnabled and Configuration.ProxyCreationEnabled assignments,
/// and adds appropriate TODO comments with guidance.
/// </summary>
public sealed class LazyLoadingConfigRewriter : CSharpSyntaxRewriter, ILazyLoadingConfigRewriter
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private int _lowestConfidence = 100;

    private bool? _lazyLoadingExplicitSetting;
    private bool? _proxyCreationExplicitSetting;
    private string? _currentDbContextName;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public bool LazyLoadingWasEnabled => _lazyLoadingExplicitSetting ?? true; // EF6 default

    /// <inheritdoc />
    public bool ProxyCreationWasEnabled => _proxyCreationExplicitSetting ?? true; // EF6 default

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
        _lowestConfidence = 100;
        _lazyLoadingExplicitSetting = null;
        _proxyCreationExplicitSetting = null;
        _currentDbContextName = null;

        // Parse the source code
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // Rewrite the tree
        var rewritten = Visit(root);

        if (rewritten == null)
        {
            return sourceCode;
        }

        return rewritten.ToFullString();
    }

    /// <summary>
    /// Visits class declarations to detect DbContext classes.
    /// </summary>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var previousContextName = _currentDbContextName;
        bool? localLazySetting = null;
        bool? localProxySetting = null;

        // Check if this class inherits from DbContext
        if (InheritsFromDbContext(node))
        {
            _currentDbContextName = node.Identifier.Text;

            // Temporarily reset to detect settings in THIS DbContext
            _lazyLoadingExplicitSetting = null;
            _proxyCreationExplicitSetting = null;
        }

        // Visit children
        var visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);

        // If this was a DbContext, save the detected settings and add TODO comment
        if (_currentDbContextName == node.Identifier.Text && visited != null)
        {
            // Save the settings we found for this DbContext
            localLazySetting = _lazyLoadingExplicitSetting;
            localProxySetting = _proxyCreationExplicitSetting;

            visited = AddTodoCommentIfNeeded(visited);
        }

        // Restore previous context name
        _currentDbContextName = previousContextName;

        // Keep the settings from the most recent DbContext we processed
        // (Don't restore previous settings - we want to track the last DbContext)
        if (localLazySetting.HasValue || localProxySetting.HasValue)
        {
            // Only update if we found actual settings in this DbContext
            if (localLazySetting.HasValue)
                _lazyLoadingExplicitSetting = localLazySetting;
            if (localProxySetting.HasValue)
                _proxyCreationExplicitSetting = localProxySetting;
        }

        return visited;
    }

    /// <summary>
    /// Visits constructor declarations to find and remove lazy loading configuration.
    /// </summary>
    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        // Only process if we're inside a DbContext
        if (_currentDbContextName == null)
        {
            return base.VisitConstructorDeclaration(node);
        }

        // Visit the constructor body to detect and remove Configuration.* assignments
        var visited = (ConstructorDeclarationSyntax?)base.VisitConstructorDeclaration(node);

        return visited;
    }

    /// <summary>
    /// Visits expression statements to detect and remove Configuration.LazyLoadingEnabled/ProxyCreationEnabled.
    /// </summary>
    public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        // Only process if we're inside a DbContext
        if (_currentDbContextName == null)
        {
            return base.VisitExpressionStatement(node);
        }

        // Check if this is an assignment to Configuration.LazyLoadingEnabled or ProxyCreationEnabled
        if (node.Expression is AssignmentExpressionSyntax assignment)
        {
            var leftSide = assignment.Left.ToString();

            if (leftSide.Contains("Configuration.LazyLoadingEnabled", StringComparison.Ordinal))
            {
                // Extract the boolean value
                var value = ExtractBooleanValue(assignment.Right);
                if (value.HasValue)
                {
                    _lazyLoadingExplicitSetting = value.Value;

                    _diagnostics.Add(new RewriterDiagnostic(
                        $"Detected Configuration.LazyLoadingEnabled = {value.Value} in {_currentDbContextName}",
                        RewriterDiagnosticSeverity.Info));
                }

                // Remove this statement
                return null;
            }

            if (leftSide.Contains("Configuration.ProxyCreationEnabled", StringComparison.Ordinal))
            {
                // Extract the boolean value
                var value = ExtractBooleanValue(assignment.Right);
                if (value.HasValue)
                {
                    _proxyCreationExplicitSetting = value.Value;

                    _diagnostics.Add(new RewriterDiagnostic(
                        $"Detected Configuration.ProxyCreationEnabled = {value.Value} in {_currentDbContextName}",
                        RewriterDiagnosticSeverity.Info));
                }

                // Remove this statement
                return null;
            }
        }

        return base.VisitExpressionStatement(node);
    }

    /// <summary>
    /// Adds TODO comment to constructor if lazy loading configuration was detected.
    /// </summary>
    private ClassDeclarationSyntax AddTodoCommentIfNeeded(ClassDeclarationSyntax node)
    {
        var lazyEnabled = _lazyLoadingExplicitSetting ?? true; // EF6 default
        var proxyEnabled = _proxyCreationExplicitSetting ?? true; // EF6 default
        var hadExplicitSettings = _lazyLoadingExplicitSetting.HasValue || _proxyCreationExplicitSetting.HasValue;

        // Find the constructor to add comment to
        var constructor = node.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        if (constructor == null)
        {
            // No constructor, can't add comment
            if (hadExplicitSettings)
            {
                _diagnostics.Add(new RewriterDiagnostic(
                    $"Removed lazy loading configuration from {_currentDbContextName} but no constructor found to add TODO comment",
                    RewriterDiagnosticSeverity.Warning));
            }
            return node;
        }

        // Determine confidence score
        if (_lazyLoadingExplicitSetting == false || _proxyCreationExplicitSetting == false)
        {
            // Explicit disable - high confidence
            _lowestConfidence = Math.Min(_lowestConfidence, 95);
        }
        else if (_lazyLoadingExplicitSetting == true || _proxyCreationExplicitSetting == true)
        {
            // Explicit enable - medium confidence (needs package + virtual props)
            _lowestConfidence = Math.Min(_lowestConfidence, 85);
        }
        else if (hadExplicitSettings)
        {
            // Had some explicit settings but not lazy loading
            _lowestConfidence = Math.Min(_lowestConfidence, 80);
        }
        else
        {
            // No explicit settings - assume EF6 defaults
            _lowestConfidence = Math.Min(_lowestConfidence, 75);
        }

        // Create appropriate TODO comment
        string todoComment;
        if (lazyEnabled && proxyEnabled)
        {
            todoComment = @"// TODO: EF6 had lazy loading enabled by default
        // To enable in EF Core, add to Program.cs:
        // services.AddDbContext<" + _currentDbContextName + @">(options =>
        //     options.UseLazyLoadingProxies()
        //            .UseSqlServer(connectionString));
        // Requires: Microsoft.EntityFrameworkCore.Proxies package
        // Note: Navigation properties must be virtual";

            _diagnostics.Add(new RewriterDiagnostic(
                $"Lazy loading was enabled in {_currentDbContextName} - added TODO comment with migration instructions",
                RewriterDiagnosticSeverity.Warning));
        }
        else
        {
            todoComment = @"// Lazy loading is disabled (EF Core default)
        // Use Include() for eager loading or Entry().Load() for explicit loading";

            _diagnostics.Add(new RewriterDiagnostic(
                $"Lazy loading was disabled in {_currentDbContextName} - added informational comment",
                RewriterDiagnosticSeverity.Info));
        }

        // Add the comment to the constructor body
        var newConstructor = AddCommentToConstructor(constructor, todoComment);
        var newMembers = node.Members.Replace(constructor, newConstructor);

        return node.WithMembers(newMembers);
    }

    /// <summary>
    /// Adds a comment to the beginning of a constructor body.
    /// </summary>
    private ConstructorDeclarationSyntax AddCommentToConstructor(ConstructorDeclarationSyntax constructor, string comment)
    {
        if (constructor.Body == null)
        {
            // Constructor has no body (expression-bodied or something else)
            // Create a body with just the comment
            var commentTriviaNoBody = SyntaxFactory.ParseLeadingTrivia(comment + "\n");
            var body = SyntaxFactory.Block()
                .WithOpenBraceToken(
                    SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                        .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")))
                .WithCloseBraceToken(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(commentTriviaNoBody),
                        SyntaxKind.CloseBraceToken,
                        SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine("\n"))));

            return constructor.WithBody(body);
        }

        // Add comment as leading trivia to the first statement, or to the close brace if empty
        var existingStatements = constructor.Body.Statements;
        var commentTrivia = SyntaxFactory.ParseLeadingTrivia(comment + "\n        ");

        if (existingStatements.Count > 0)
        {
            var firstStatement = existingStatements[0];
            var newFirstStatement = firstStatement.WithLeadingTrivia(
                commentTrivia.AddRange(firstStatement.GetLeadingTrivia()));
            var newStatements = existingStatements.Replace(firstStatement, newFirstStatement);

            return constructor.WithBody(constructor.Body.WithStatements(newStatements));
        }
        else
        {
            // Empty body - add comment before the closing brace
            var closeBrace = constructor.Body.CloseBraceToken;
            var newCloseBrace = closeBrace.WithLeadingTrivia(
                commentTrivia.AddRange(closeBrace.LeadingTrivia));

            return constructor.WithBody(constructor.Body.WithCloseBraceToken(newCloseBrace));
        }
    }

    /// <summary>
    /// Extracts a boolean value from an expression.
    /// </summary>
    private static bool? ExtractBooleanValue(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.TrueLiteralExpression => true,
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.FalseLiteralExpression => false,
            _ => null
        };
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
    /// Extracts the base class name from a type syntax node.
    /// </summary>
    private static string? ExtractBaseName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.Text,
            _ => null
        };
    }
}
