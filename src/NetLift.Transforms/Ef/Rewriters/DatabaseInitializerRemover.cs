using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;

namespace NetLift.Transforms.Ef.Rewriters;

/// <summary>
/// Removes EF6 Database.SetInitializer calls and adds migration guidance for EF Core.
/// Detects and removes all EF6 initializer patterns:
/// - CreateDatabaseIfNotExists
/// - MigrateDatabaseToLatestVersion
/// - DropCreateDatabaseIfModelChanges
/// - DropCreateDatabaseAlways
/// - Null initializers
/// Comments out the original statement and adds appropriate migration guidance based on the initializer type.
/// </summary>
public sealed class DatabaseInitializerRemover : CSharpSyntaxRewriter, IDatabaseInitializerRemover
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private readonly List<RemovedInitializerInfo> _removedInitializers = new();
    private int _lowestConfidence = 100;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public IReadOnlyCollection<RemovedInitializerInfo> RemovedInitializers => _removedInitializers;

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
        _removedInitializers.Clear();
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

        return rewritten.ToFullString();
    }

    /// <summary>
    /// Visits expression statements to detect and remove Database.SetInitializer calls.
    /// </summary>
    public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        // Check if this is a Database.SetInitializer call
        if (node.Expression is InvocationExpressionSyntax invocation &&
            IsSetInitializerCall(invocation))
        {
            return TransformSetInitializerStatement(node, invocation);
        }

        return base.VisitExpressionStatement(node);
    }

    /// <summary>
    /// Checks if an invocation is a Database.SetInitializer call.
    /// </summary>
    private static bool IsSetInitializerCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // Check method name
        if (memberAccess.Name.Identifier.Text != "SetInitializer")
        {
            return false;
        }

        // Check if it's called on Database
        var expressionText = memberAccess.Expression.ToString();
        return expressionText == "Database" || expressionText.EndsWith(".Database", StringComparison.Ordinal);
    }

    /// <summary>
    /// Transforms a Database.SetInitializer statement by commenting it out and adding guidance.
    /// </summary>
    private SyntaxNode TransformSetInitializerStatement(
        ExpressionStatementSyntax node,
        InvocationExpressionSyntax invocation)
    {
        // Extract initializer information
        var initializerInfo = ExtractInitializerInfo(invocation);

        // Generate guidance comment
        var guidanceComment = GenerateGuidanceComment(
            initializerInfo.InitializerType,
            initializerInfo.ContextType);

        // Get the original statement as string
        var originalStatement = node.ToFullString().TrimEnd();

        // Track the removal
        _removedInitializers.Add(new RemovedInitializerInfo(
            initializerInfo.InitializerType,
            initializerInfo.ContextType,
            originalStatement));

        _lowestConfidence = Math.Min(_lowestConfidence, 95);
        _diagnostics.Add(new RewriterDiagnostic(
            $"Removed Database.SetInitializer<{initializerInfo.ContextType}>({initializerInfo.InitializerType}) - added migration guidance",
            RewriterDiagnosticSeverity.Info));

        // Get the leading trivia (indentation) from the original statement
        var leadingTrivia = node.GetLeadingTrivia();

        // Create the commented-out version with guidance
        var commentedCode = CreateCommentedStatement(
            guidanceComment,
            originalStatement,
            leadingTrivia);

        // Return an empty statement with the comment trivia
        // This effectively removes the code but preserves it as a comment
        var emptyStatement = SyntaxFactory.EmptyStatement()
            .WithLeadingTrivia(commentedCode)
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        return emptyStatement;
    }

    /// <summary>
    /// Extracts initializer type and context type from the SetInitializer invocation.
    /// </summary>
    private static (string InitializerType, string ContextType) ExtractInitializerInfo(InvocationExpressionSyntax invocation)
    {
        string initializerType = "Unknown";
        string contextType = "DbContext";

        // Get the initializer type and context type from the argument
        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            var argument = invocation.ArgumentList.Arguments[0].Expression;

            if (argument is ObjectCreationExpressionSyntax objectCreation)
            {
                // Extract the initializer type name and context type from the generic argument
                if (objectCreation.Type is GenericNameSyntax genericType)
                {
                    initializerType = genericType.Identifier.Text;

                    // Extract context type from the first generic parameter
                    var typeArgs = genericType.TypeArgumentList.Arguments;
                    if (typeArgs.Count > 0)
                    {
                        contextType = typeArgs[0].ToString();
                    }
                }
                else if (objectCreation.Type is IdentifierNameSyntax identifier)
                {
                    initializerType = identifier.Identifier.Text;
                }
            }
            else if (argument.IsKind(SyntaxKind.NullLiteralExpression))
            {
                initializerType = "null";

                // For null initializer, try to get context type from SetInitializer<TContext>
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name is GenericNameSyntax genericName)
                {
                    var typeArgs = genericName.TypeArgumentList.Arguments;
                    if (typeArgs.Count > 0)
                    {
                        contextType = typeArgs[0].ToString();
                    }
                }
            }
        }

        return (initializerType, contextType);
    }

    /// <summary>
    /// Generates appropriate migration guidance based on the initializer type.
    /// </summary>
    private static string GenerateGuidanceComment(string initializerType, string contextType)
    {
        return initializerType switch
        {
            "CreateDatabaseIfNotExists" =>
                $@"// TODO: EF Core migration guidance for CreateDatabaseIfNotExists
// For development/testing: Use context.Database.EnsureCreated() in Program.cs
// For production with migrations: Use context.Database.Migrate() in Program.cs
//
// Add to Program.cs after app.Build():
//   using (var scope = app.Services.CreateScope())
//   {{
//       var db = scope.ServiceProvider.GetRequiredService<{contextType}>();
//       db.Database.Migrate(); // or db.Database.EnsureCreated() for simple scenarios
//   }}
//
// To create migrations:
//   dotnet ef migrations add InitialCreate
//   dotnet ef database update",

            "MigrateDatabaseToLatestVersion" =>
                $@"// TODO: EF Core migration guidance for MigrateDatabaseToLatestVersion
// Use context.Database.Migrate() in Program.cs to apply pending migrations
//
// Add to Program.cs after app.Build():
//   using (var scope = app.Services.CreateScope())
//   {{
//       var db = scope.ServiceProvider.GetRequiredService<{contextType}>();
//       db.Database.Migrate();
//   }}
//
// To manage migrations:
//   dotnet ef migrations add MigrationName
//   dotnet ef migrations list
//   dotnet ef database update",

            "DropCreateDatabaseIfModelChanges" =>
                $@"// TODO: EF Core migration guidance for DropCreateDatabaseIfModelChanges
// WARNING: No direct equivalent in EF Core
// For development: Use db.Database.EnsureDeleted() + db.Database.EnsureCreated()
// For production: Use migrations to handle schema changes
//
// Development approach (add to Program.cs):
//   using (var scope = app.Services.CreateScope())
//   {{
//       var db = scope.ServiceProvider.GetRequiredService<{contextType}>();
//       db.Database.EnsureDeleted();
//       db.Database.EnsureCreated();
//   }}
//
// Production approach: Use migrations instead
//   dotnet ef migrations add MigrationName
//   dotnet ef database update",

            "DropCreateDatabaseAlways" =>
                $@"// TODO: EF Core migration guidance for DropCreateDatabaseAlways
// WARNING: Only suitable for development/testing
// Use db.Database.EnsureDeleted() + db.Database.EnsureCreated()
//
// Add to Program.cs (DEVELOPMENT ONLY):
//   using (var scope = app.Services.CreateScope())
//   {{
//       var db = scope.ServiceProvider.GetRequiredService<{contextType}>();
//       db.Database.EnsureDeleted();
//       db.Database.EnsureCreated();
//   }}
//
// For production: Use migrations
//   dotnet ef migrations add InitialCreate
//   dotnet ef database update",

            "null" =>
                $@"// TODO: EF Core migration guidance
// Database.SetInitializer<{contextType}>(null) has been removed
// EF Core does not auto-initialize databases by default
// Use explicit migration control:
//   - context.Database.Migrate() for migrations
//   - context.Database.EnsureCreated() for simple scenarios
//
// To manage database schema:
//   dotnet ef migrations add InitialCreate
//   dotnet ef database update",

            _ =>
                $@"// TODO: EF Core migration guidance
// Custom initializer '{initializerType}' has been removed
// EF Core uses migrations for schema management
// Review and migrate initialization logic to:
//   - OnModelCreating() for schema configuration
//   - Database.Migrate() or Database.EnsureCreated() for initialization
//
// Migration commands:
//   dotnet ef migrations add InitialCreate
//   dotnet ef database update"
        };
    }

    /// <summary>
    /// Creates a commented statement with guidance and the original code.
    /// </summary>
    private static SyntaxTriviaList CreateCommentedStatement(
        string guidanceComment,
        string originalStatement,
        SyntaxTriviaList leadingTrivia)
    {
        var triviaList = new List<SyntaxTrivia>();

        // Add original indentation
        triviaList.AddRange(leadingTrivia);

        // Add guidance comment lines
        var guidanceLines = guidanceComment.Split('\n');
        foreach (var line in guidanceLines)
        {
            triviaList.Add(SyntaxFactory.Comment(line.TrimEnd()));
            triviaList.Add(SyntaxFactory.EndOfLine("\n"));

            // Add indentation for multi-line comments
            if (line != guidanceLines[^1])
            {
                triviaList.AddRange(leadingTrivia);
            }
        }

        // Add original indentation before the commented-out code
        triviaList.AddRange(leadingTrivia);

        // Add the commented-out original statement (trim all whitespace from original)
        var cleanStatement = originalStatement.Trim();
        triviaList.Add(SyntaxFactory.Comment($"// REMOVED: {cleanStatement}"));
        triviaList.Add(SyntaxFactory.EndOfLine("\n"));

        return SyntaxFactory.TriviaList(triviaList);
    }
}
