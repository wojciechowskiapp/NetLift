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

    // EF6 database initializer base class names
    private static readonly HashSet<string> InitializerBaseClasses = new(StringComparer.Ordinal)
    {
        "CreateDatabaseIfNotExists",
        "DropCreateDatabaseIfModelChanges",
        "DropCreateDatabaseAlways",
        "MigrateDatabaseToLatestVersion"
    };

    /// <summary>
    /// Visits class declarations to detect and transform classes inheriting from EF6 initializers.
    /// </summary>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
        if (visited == null || visited.BaseList == null)
        {
            return visited;
        }

        // Check if the base class is an EF6 initializer
        var firstBaseType = visited.BaseList.Types.FirstOrDefault();
        if (firstBaseType == null)
        {
            return visited;
        }

        var baseTypeName = ExtractBaseTypeName(firstBaseType.Type);
        if (!InitializerBaseClasses.Contains(baseTypeName))
        {
            return visited;
        }

        // Extract context type from the generic argument
        var contextType = ExtractContextType(firstBaseType.Type);

        // Remove the base class
        var remainingBaseTypes = visited.BaseList.Types.Skip(1).ToList();
        ClassDeclarationSyntax result;

        if (remainingBaseTypes.Count == 0)
        {
            // No other base types, remove the entire base list
            result = visited.WithBaseList(null);
        }
        else
        {
            // Keep other interfaces
            var newBaseList = SyntaxFactory.BaseList(
                SyntaxFactory.SeparatedList(remainingBaseTypes));
            result = visited.WithBaseList(newBaseList);
        }

        // Remove 'override' modifier from all methods (they were overriding the removed base class)
        result = RemoveOverrideModifiers(result);

        // Add TODO comment with migration guidance
        var guidanceComment = GenerateClassGuidanceComment(baseTypeName, contextType, visited.Identifier.Text);
        var leadingTrivia = result.GetLeadingTrivia();
        var commentTrivia = SyntaxFactory.Comment(guidanceComment);
        result = result.WithLeadingTrivia(
            leadingTrivia.Add(commentTrivia).Add(SyntaxFactory.EndOfLine("\n")));

        _lowestConfidence = Math.Min(_lowestConfidence, 70);
        _diagnostics.Add(new RewriterDiagnostic(
            $"Removed {baseTypeName}<{contextType}> base class from {visited.Identifier.Text} - class needs manual conversion to use EF Core migrations or HasData() seeding",
            RewriterDiagnosticSeverity.Warning));

        _removedInitializers.Add(new RemovedInitializerInfo(
            baseTypeName,
            contextType,
            $"class {visited.Identifier.Text} : {firstBaseType.Type}"));

        return result;
    }

    /// <summary>
    /// Extracts the base type name without generic arguments.
    /// </summary>
    private static string ExtractBaseTypeName(TypeSyntax type)
    {
        return type switch
        {
            GenericNameSyntax generic => generic.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            QualifiedNameSyntax qualified => ExtractBaseTypeName(qualified.Right),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Extracts the context type from a generic type.
    /// </summary>
    private static string ExtractContextType(TypeSyntax type)
    {
        if (type is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count > 0)
        {
            return generic.TypeArgumentList.Arguments[0].ToString();
        }
        return "DbContext";
    }

    /// <summary>
    /// Removes 'override' modifier from all methods in a class.
    /// Used when the base class is removed and override methods become regular methods.
    /// </summary>
    private static ClassDeclarationSyntax RemoveOverrideModifiers(ClassDeclarationSyntax classDecl)
    {
        var newMembers = new SyntaxList<MemberDeclarationSyntax>();

        foreach (var member in classDecl.Members)
        {
            if (member is MethodDeclarationSyntax method)
            {
                var overrideToken = method.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.OverrideKeyword));
                if (overrideToken != default)
                {
                    // Remove the override modifier
                    var newModifiers = method.Modifiers.Remove(overrideToken);

                    // If the method was protected override, make it public (common pattern for Seed)
                    var protectedToken = newModifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.ProtectedKeyword));
                    if (protectedToken != default)
                    {
                        newModifiers = newModifiers.Remove(protectedToken);
                        newModifiers = newModifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space));
                    }

                    var newMethod = method.WithModifiers(newModifiers);
                    newMembers = newMembers.Add(newMethod);
                    continue;
                }
            }
            newMembers = newMembers.Add(member);
        }

        return classDecl.WithMembers(newMembers);
    }

    /// <summary>
    /// Generates guidance comment for classes inheriting from EF6 initializers.
    /// </summary>
    private static string GenerateClassGuidanceComment(string initializerType, string contextType, string className)
    {
        return $@"// TODO: EF Core migration guidance for {className}
// This class inherited from {initializerType}<{contextType}> which doesn't exist in EF Core.
//
// Options for migrating the Seed() method:
// 1. Use HasData() in OnModelCreating for static seed data:
//    modelBuilder.Entity<Genre>().HasData(new Genre {{ GenreId = 1, Name = ""Rock"" }});
//
// 2. Create a separate seed service called from Program.cs:
//    public class DataSeeder {{ public void Seed({contextType} context) {{ ... }} }}
//
// 3. Use EF Core migrations with data seeding in migrations.
//
// For database initialization, use in Program.cs:
//    var scope = app.Services.CreateScope();
//    var db = scope.ServiceProvider.GetRequiredService<{contextType}>();
//    db.Database.Migrate(); // or db.Database.EnsureCreated()
";
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
