using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;

namespace NetLift.Transforms.Ef.Rewriters;

/// <summary>
/// Rewrites EF6 Database.SqlQuery and ExecuteSqlCommand patterns to EF Core equivalents.
/// Transforms Database.SqlQuery&lt;T&gt;() to DbSet&lt;T&gt;.FromSqlRaw() or FromSqlInterpolated().
/// Transforms ExecuteSqlCommand to ExecuteSqlRaw.
/// </summary>
public sealed class SqlQueryRewriter : CSharpSyntaxRewriter, ISqlQueryRewriter
{
    private static readonly Regex PlaceholderRegex = new(@"@p(\d+)", RegexOptions.Compiled);

    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private readonly HashSet<string> _keylessTypes = new(StringComparer.Ordinal);
    private int _lowestConfidence = 100;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public IReadOnlyCollection<string> KeylessTypesDetected => _keylessTypes;

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
        _keylessTypes.Clear();
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
    /// Visits invocation expressions to detect and transform EF6 SQL patterns.
    /// </summary>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Check pattern BEFORE visiting children to preserve original structure
        // Check if this is Database.SqlQuery<T>()
        if (IsSqlQueryInvocation(node, out var typeArgument))
        {
            return TransformSqlQuery(node, typeArgument!);
        }

        // Check if this is Database.ExecuteSqlCommand()
        if (IsExecuteSqlCommandInvocation(node))
        {
            return TransformExecuteSqlCommand(node);
        }

        // Not a pattern we're transforming, visit children normally
        return base.VisitInvocationExpression(node);
    }

    /// <summary>
    /// Checks if an invocation is context.Database.SqlQuery&lt;T&gt;().
    /// </summary>
    private static bool IsSqlQueryInvocation(InvocationExpressionSyntax node, out TypeSyntax? typeArgument)
    {
        typeArgument = null;

        // Must be a member access: something.SqlQuery
        if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // Member name must be SqlQuery with generic type argument
        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        if (genericName.Identifier.Text != "SqlQuery")
        {
            return false;
        }

        // Extract type argument
        if (genericName.TypeArgumentList.Arguments.Count == 1)
        {
            typeArgument = genericName.TypeArgumentList.Arguments[0];
        }

        // Check if accessed on Database property
        // Can be either: Database.SqlQuery (implicit this) or context.Database.SqlQuery
        if (memberAccess.Expression is IdentifierNameSyntax identifierName &&
            identifierName.Identifier.Text == "Database")
        {
            return true;
        }

        if (memberAccess.Expression is MemberAccessExpressionSyntax databaseAccess &&
            databaseAccess.Name.Identifier.Text == "Database")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if an invocation is context.Database.ExecuteSqlCommand().
    /// </summary>
    private static bool IsExecuteSqlCommandInvocation(InvocationExpressionSyntax node)
    {
        // Must be a member access: something.ExecuteSqlCommand
        if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // Member name must be ExecuteSqlCommand
        if (memberAccess.Name.Identifier.Text != "ExecuteSqlCommand")
        {
            return false;
        }

        // Check if accessed on Database property
        // Can be either: Database.ExecuteSqlCommand (implicit this) or context.Database.ExecuteSqlCommand
        if (memberAccess.Expression is IdentifierNameSyntax identifierName &&
            identifierName.Identifier.Text == "Database")
        {
            return true;
        }

        if (memberAccess.Expression is MemberAccessExpressionSyntax databaseAccess &&
            databaseAccess.Name.Identifier.Text == "Database")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Transforms Database.SqlQuery&lt;T&gt;() to DbSet&lt;T&gt;.FromSqlRaw() or FromSqlInterpolated().
    /// </summary>
    private InvocationExpressionSyntax TransformSqlQuery(InvocationExpressionSyntax node, TypeSyntax typeArgument)
    {
        var typeName = typeArgument.ToString();

        // Check if first argument is an interpolated string
        var isInterpolated = node.ArgumentList.Arguments.Count > 0 &&
                            node.ArgumentList.Arguments[0].Expression is InterpolatedStringExpressionSyntax;

        if (isInterpolated)
        {
            return TransformToFromSqlInterpolated(node, typeName);
        }
        else
        {
            return TransformToFromSqlRaw(node, typeName);
        }
    }

    /// <summary>
    /// Transforms to FromSqlRaw with placeholder conversion.
    /// </summary>
    private InvocationExpressionSyntax TransformToFromSqlRaw(InvocationExpressionSyntax node, string typeName)
    {
        var dbSetName = GetDbSetName(typeName);
        var useSetMethod = dbSetName == null;

        if (useSetMethod)
        {
            _keylessTypes.Add(typeName);
            _lowestConfidence = Math.Min(_lowestConfidence, 80);
            _diagnostics.Add(new RewriterDiagnostic(
                $"Type '{typeName}' might be a keyless entity - using Set<{typeName}>() and may require OnModelCreating configuration",
                RewriterDiagnosticSeverity.Warning));
        }
        else
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 95);
        }

        // Convert @p0, @p1 placeholders to {0}, {1}
        var arguments = ConvertPlaceholders(node.ArgumentList.Arguments);

        // Build new invocation
        var contextExpression = GetContextExpression(node);
        var dbSetAccess = useSetMethod
            ? BuildSetMethodAccess(contextExpression, typeName)
            : BuildDbSetAccess(contextExpression, dbSetName!);

        var fromSqlRawAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            dbSetAccess,
            SyntaxFactory.IdentifierName("FromSqlRaw"));

        var result = SyntaxFactory.InvocationExpression(
            fromSqlRawAccess,
            SyntaxFactory.ArgumentList(arguments))
            .WithTriviaFrom(node);

        _requiredUsings.Add("Microsoft.EntityFrameworkCore");

        _diagnostics.Add(new RewriterDiagnostic(
            $"Transformed Database.SqlQuery<{typeName}>() to {(useSetMethod ? $"Set<{typeName}>()" : dbSetName)}.FromSqlRaw()",
            RewriterDiagnosticSeverity.Info));

        return result;
    }

    /// <summary>
    /// Transforms to FromSqlInterpolated for interpolated strings.
    /// </summary>
    private InvocationExpressionSyntax TransformToFromSqlInterpolated(InvocationExpressionSyntax node, string typeName)
    {
        var dbSetName = GetDbSetName(typeName);
        var useSetMethod = dbSetName == null;

        if (useSetMethod)
        {
            _keylessTypes.Add(typeName);
            _lowestConfidence = Math.Min(_lowestConfidence, 80);
            _diagnostics.Add(new RewriterDiagnostic(
                $"Type '{typeName}' might be a keyless entity - using Set<{typeName}>() and may require OnModelCreating configuration",
                RewriterDiagnosticSeverity.Warning));
        }
        else
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 90);
        }

        // Build new invocation
        var contextExpression = GetContextExpression(node);
        var dbSetAccess = useSetMethod
            ? BuildSetMethodAccess(contextExpression, typeName)
            : BuildDbSetAccess(contextExpression, dbSetName!);

        var fromSqlInterpolatedAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            dbSetAccess,
            SyntaxFactory.IdentifierName("FromSqlInterpolated"));

        var result = SyntaxFactory.InvocationExpression(
            fromSqlInterpolatedAccess,
            node.ArgumentList)
            .WithTriviaFrom(node);

        _requiredUsings.Add("Microsoft.EntityFrameworkCore");

        _diagnostics.Add(new RewriterDiagnostic(
            $"Transformed Database.SqlQuery<{typeName}>() to {(useSetMethod ? $"Set<{typeName}>()" : dbSetName)}.FromSqlInterpolated()",
            RewriterDiagnosticSeverity.Info));

        return result;
    }

    /// <summary>
    /// Transforms Database.ExecuteSqlCommand() to Database.ExecuteSqlRaw().
    /// </summary>
    private InvocationExpressionSyntax TransformExecuteSqlCommand(InvocationExpressionSyntax node)
    {
        _lowestConfidence = Math.Min(_lowestConfidence, 95);

        // Convert @p0, @p1 placeholders to {0}, {1}
        var arguments = ConvertPlaceholders(node.ArgumentList.Arguments);

        // Get the Database member access
        var memberAccess = (MemberAccessExpressionSyntax)node.Expression;
        var databaseExpression = memberAccess.Expression;

        // Build new invocation with ExecuteSqlRaw
        var executeSqlRawAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            databaseExpression,
            SyntaxFactory.IdentifierName("ExecuteSqlRaw"));

        var result = SyntaxFactory.InvocationExpression(
            executeSqlRawAccess,
            SyntaxFactory.ArgumentList(arguments))
            .WithTriviaFrom(node);

        _requiredUsings.Add("Microsoft.EntityFrameworkCore");

        _diagnostics.Add(new RewriterDiagnostic(
            "Transformed Database.ExecuteSqlCommand() to Database.ExecuteSqlRaw()",
            RewriterDiagnosticSeverity.Info));

        return result;
    }

    /// <summary>
    /// Converts @p0, @p1 style placeholders to {0}, {1} style placeholders in SQL string.
    /// </summary>
    private static SeparatedSyntaxList<ArgumentSyntax> ConvertPlaceholders(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        if (arguments.Count == 0)
        {
            return arguments;
        }

        var firstArg = arguments[0];

        // Only convert placeholders in string literals, not interpolated strings
        if (firstArg.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var originalText = literal.Token.ValueText;
            var convertedText = PlaceholderRegex.Replace(originalText, "{$1}");

            if (originalText != convertedText)
            {
                var newLiteral = SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(convertedText));

                var newFirstArg = firstArg.WithExpression(newLiteral);
                return arguments.Replace(firstArg, newFirstArg);
            }
        }

        return arguments;
    }

    /// <summary>
    /// Gets the context expression from Database.SqlQuery invocation.
    /// </summary>
    private static ExpressionSyntax? GetContextExpression(InvocationExpressionSyntax node)
    {
        // node.Expression is something.SqlQuery
        // memberAccess.Expression can be:
        // - Database (implicit this - IdentifierNameSyntax)
        // - context.Database (MemberAccessExpressionSyntax)
        var memberAccess = (MemberAccessExpressionSyntax)node.Expression;

        if (memberAccess.Expression is IdentifierNameSyntax)
        {
            // Database.SqlQuery - implicit this, we'll use "this" in generated code
            return null;
        }

        if (memberAccess.Expression is MemberAccessExpressionSyntax databaseAccess)
        {
            // context.Database.SqlQuery - extract "context"
            return databaseAccess.Expression;
        }

        return null;
    }

    /// <summary>
    /// Gets the DbSet property name for a type.
    /// Returns null if the type name doesn't look like a standard entity.
    /// Uses simple pluralization heuristic.
    /// </summary>
    private static string? GetDbSetName(string typeName)
    {
        // Simple pluralization heuristic
        // This is not perfect but works for most common cases
        // For production, we'd need semantic analysis to check actual DbSet properties

        // Common patterns that suggest this is an entity type
        if (typeName.EndsWith("Entity", StringComparison.Ordinal) ||
            typeName.EndsWith("Model", StringComparison.Ordinal) ||
            typeName.EndsWith("ViewModel", StringComparison.Ordinal) ||
            typeName.EndsWith("DTO", StringComparison.Ordinal) ||
            typeName.EndsWith("Summary", StringComparison.Ordinal) ||
            typeName.EndsWith("Info", StringComparison.Ordinal))
        {
            // These look like DTOs or view models, not entities
            return null;
        }

        // Simple pluralization
        return Pluralize(typeName);
    }

    /// <summary>
    /// Simple pluralization for common entity names.
    /// </summary>
    private static string Pluralize(string name)
    {
        // Simple rules - for production, use a proper pluralization library
        if (name.EndsWith("s", StringComparison.Ordinal) ||
            name.EndsWith("x", StringComparison.Ordinal) ||
            name.EndsWith("ch", StringComparison.Ordinal) ||
            name.EndsWith("sh", StringComparison.Ordinal))
        {
            return name + "es";
        }

        if (name.EndsWith("y", StringComparison.Ordinal) &&
            name.Length > 1 &&
            !IsVowel(name[name.Length - 2]))
        {
            return name.Substring(0, name.Length - 1) + "ies";
        }

        return name + "s";
    }

    /// <summary>
    /// Checks if a character is a vowel.
    /// </summary>
    private static bool IsVowel(char c)
    {
        return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' ||
               c == 'A' || c == 'E' || c == 'I' || c == 'O' || c == 'U';
    }

    /// <summary>
    /// Builds context.Set&lt;T&gt;() expression or Set&lt;T&gt;() if context is null (implicit this).
    /// </summary>
    private static ExpressionSyntax BuildSetMethodAccess(ExpressionSyntax? contextExpression, string typeName)
    {
        var setMethod = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("Set"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.ParseTypeName(typeName))));

        ExpressionSyntax setAccess;
        if (contextExpression != null)
        {
            setAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                contextExpression,
                setMethod);
        }
        else
        {
            // Implicit this - just Set<T>
            setAccess = setMethod;
        }

        return SyntaxFactory.InvocationExpression(setAccess);
    }

    /// <summary>
    /// Builds context.DbSetName expression or DbSetName if context is null (implicit this).
    /// </summary>
    private static ExpressionSyntax BuildDbSetAccess(ExpressionSyntax? contextExpression, string dbSetName)
    {
        if (contextExpression != null)
        {
            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                contextExpression,
                SyntaxFactory.IdentifierName(dbSetName));
        }
        else
        {
            // Implicit this - just DbSetName
            return SyntaxFactory.IdentifierName(dbSetName);
        }
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
