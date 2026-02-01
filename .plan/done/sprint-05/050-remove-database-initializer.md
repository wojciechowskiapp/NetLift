# [TASK-050] Remove Database.SetInitializer and Add Migrations Guidance

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P2 |
| **Estimate** | S |
| **Sprint** | 5 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-044
- **Blocks:** -

---

## Description

Remove EF6 `Database.SetInitializer<T>()` calls and related database initialization patterns. EF Core uses a different migration and database initialization approach. This task handles detection and removal of initializers while adding TODO comments guiding users to EF Core migrations.

---

## Acceptance Criteria

- [ ] Detect `Database.SetInitializer<T>()` calls in constructors and static constructors
- [ ] Comment out or remove initializer calls with explanatory TODO
- [ ] Detect custom `IDatabaseInitializer<T>` implementations and add migration guidance
- [ ] Handle `CreateDatabaseIfNotExists`, `DropCreateDatabaseIfModelChanges`, `DropCreateDatabaseAlways`, and `MigrateDatabaseToLatestVersion` initializers
- [ ] Generate EF Core migration commands as comments
- [ ] Add `context.Database.EnsureCreated()` or `context.Database.Migrate()` guidance
- [ ] Unit tests for various initializer patterns

---

## Technical Notes

### Transformation Patterns

**Pattern 1: Simple Initializer in Constructor**

```csharp
// EF6
public class AppContext : DbContext
{
    public AppContext() : base("DefaultConnection")
    {
        Database.SetInitializer(new CreateDatabaseIfNotExists<AppContext>());
    }
}

// EF Core
public class AppContext : DbContext
{
    public AppContext(DbContextOptions<AppContext> options)
        : base(options)
    {
        // TODO: EF Core database initialization
        // For development: Database.EnsureCreated() - creates DB without migrations
        // For production: Database.Migrate() - applies pending migrations
        // Run in Program.cs/Startup.cs: using var scope = app.Services.CreateScope();
        //                               var db = scope.ServiceProvider.GetRequiredService<AppContext>();
        //                               db.Database.Migrate();
    }
}
```

**Pattern 2: Static Initializer**

```csharp
// EF6
public class AppContext : DbContext
{
    static AppContext()
    {
        Database.SetInitializer(new MigrateDatabaseToLatestVersion<AppContext, Configuration>());
    }
}

// EF Core
public class AppContext : DbContext
{
    // TODO: EF Core migrations - remove static initializer
    // Run migrations via CLI: dotnet ef database update
    // Or programmatically in Program.cs: db.Database.Migrate();

    // static AppContext()
    // {
    //     // REMOVED: Database.SetInitializer(new MigrateDatabaseToLatestVersion<AppContext, Configuration>());
    // }
}
```

**Pattern 3: DropCreateDatabaseIfModelChanges**

```csharp
// EF6
Database.SetInitializer(new DropCreateDatabaseIfModelChanges<AppContext>());

// EF Core
// TODO: EF Core does not support automatic database recreation on model changes
// Use migrations instead:
// 1. dotnet ef migrations add <MigrationName>
// 2. dotnet ef database update
// For development reset: Database.EnsureDeleted(); Database.EnsureCreated();
```

**Pattern 4: Custom Database Initializer**

```csharp
// EF6
public class SeedDataInitializer : CreateDatabaseIfNotExists<AppContext>
{
    protected override void Seed(AppContext context)
    {
        context.Products.Add(new Product { Name = "Sample" });
        context.SaveChanges();
    }
}

// In constructor:
Database.SetInitializer(new SeedDataInitializer());

// EF Core - Seed data in OnModelCreating
public class AppContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TODO: Migrate seed data from IDatabaseInitializer
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Sample" }
        );
    }
}
```

**Pattern 5: MigrateDatabaseToLatestVersion with Configuration**

```csharp
// EF6
internal sealed class Configuration : DbMigrationsConfiguration<AppContext>
{
    public Configuration()
    {
        AutomaticMigrationsEnabled = true;
        AutomaticMigrationDataLossAllowed = false;
    }

    protected override void Seed(AppContext context)
    {
        // Seed data
    }
}

// EF Core - No DbMigrationsConfiguration equivalent
// TODO: Migrate to EF Core migrations:
// 1. Delete old Migrations folder
// 2. dotnet ef migrations add InitialCreate
// 3. Move seed data to OnModelCreating HasData() or IEntityTypeConfiguration
// Note: AutomaticMigrationsEnabled has no equivalent - always use explicit migrations
```

### Roslyn Transformation Implementation

```csharp
public class DatabaseInitializerRemover : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly List<InitializerInfo> _removedInitializers;

    public DatabaseInitializerRemover(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
        _removedInitializers = new List<InitializerInfo>();
    }

    public IReadOnlyList<InitializerInfo> RemovedInitializers => _removedInitializers;

    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (IsSetInitializerCall(node.Expression, out var initializerType, out var contextType))
        {
            _removedInitializers.Add(new InitializerInfo
            {
                InitializerType = initializerType,
                ContextType = contextType,
                OriginalCode = node.ToString()
            });

            // Replace with commented version and TODO
            var comment = GenerateMigrationGuidanceComment(initializerType);
            var commentedCode = $"// REMOVED: {node.ToString().TrimEnd(';')}";

            return SyntaxFactory.ParseStatement(comment + "\r\n" + commentedCode + ";")
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        return base.VisitExpressionStatement(node);
    }

    public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var visitedNode = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node);

        // Check if this is a static constructor that only had initializer
        if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            var body = visitedNode.Body;
            if (body != null && IsEmptyOrOnlyComments(body))
            {
                // Comment out the entire static constructor
                var trivia = SyntaxFactory.Comment(
                    "// TODO: Static constructor removed - EF Core initializers handled differently\r\n" +
                    "// " + node.ToString().Replace("\r\n", "\r\n// "));

                return SyntaxFactory.ParseMemberDeclaration("")
                    .WithLeadingTrivia(trivia);
            }
        }

        return visitedNode;
    }

    private bool IsSetInitializerCall(
        ExpressionSyntax expression,
        out string initializerType,
        out string contextType)
    {
        initializerType = null;
        contextType = null;

        if (expression is InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                // Check for Database.SetInitializer
                if (memberAccess.Name.Identifier.Text == "SetInitializer"
                    && memberAccess.Expression is IdentifierNameSyntax id
                    && id.Identifier.Text == "Database")
                {
                    // Extract initializer type from argument
                    var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
                    if (arg?.Expression is ObjectCreationExpressionSyntax creation)
                    {
                        initializerType = creation.Type.ToString();

                        // Extract context type from generic argument
                        if (creation.Type is GenericNameSyntax generic)
                        {
                            contextType = generic.TypeArgumentList.Arguments
                                .FirstOrDefault()?.ToString();
                        }
                    }
                    else if (arg?.Expression is LiteralExpressionSyntax literal
                             && literal.Kind() == SyntaxKind.NullLiteralExpression)
                    {
                        initializerType = "null";
                    }

                    return true;
                }
            }
        }

        return false;
    }

    private string GenerateMigrationGuidanceComment(string initializerType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// TODO: EF Core migration guidance");

        if (initializerType.Contains("CreateDatabaseIfNotExists"))
        {
            sb.AppendLine("// Replace with: context.Database.EnsureCreated()");
            sb.AppendLine("// Or for migrations: context.Database.Migrate()");
        }
        else if (initializerType.Contains("DropCreateDatabaseIfModelChanges"))
        {
            sb.AppendLine("// EF Core does not support automatic recreation on model changes");
            sb.AppendLine("// Use explicit migrations: dotnet ef migrations add <Name>");
            sb.AppendLine("// For dev reset: context.Database.EnsureDeleted(); context.Database.EnsureCreated();");
        }
        else if (initializerType.Contains("DropCreateDatabaseAlways"))
        {
            sb.AppendLine("// For testing/dev: context.Database.EnsureDeleted(); context.Database.EnsureCreated();");
        }
        else if (initializerType.Contains("MigrateDatabaseToLatestVersion"))
        {
            sb.AppendLine("// Replace with: context.Database.Migrate()");
            sb.AppendLine("// Run in Program.cs: var db = scope.ServiceProvider.GetRequiredService<AppContext>();");
            sb.AppendLine("//                    db.Database.Migrate();");
        }
        else if (initializerType == "null")
        {
            sb.AppendLine("// EF Core has no initializer by default - this null assignment is not needed");
        }

        return sb.ToString().TrimEnd();
    }

    private bool IsEmptyOrOnlyComments(BlockSyntax block)
    {
        return block.Statements.All(s =>
            s is EmptyStatementSyntax ||
            (s.GetLeadingTrivia().All(t =>
                t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                t.IsKind(SyntaxKind.WhitespaceTrivia) ||
                t.IsKind(SyntaxKind.EndOfLineTrivia))));
    }
}

public class InitializerInfo
{
    public string InitializerType { get; set; }
    public string ContextType { get; set; }
    public string OriginalCode { get; set; }
}
```

### DbMigrationsConfiguration Detector

```csharp
public class MigrationsConfigurationDetector
{
    public IEnumerable<MigrationConfigInfo> DetectConfigurations(
        SemanticModel semanticModel,
        SyntaxNode root)
    {
        var configs = new List<MigrationConfigInfo>();

        var classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDeclarations)
        {
            var baseType = classDecl.BaseList?.Types.FirstOrDefault();
            if (baseType == null) continue;

            var baseTypeName = baseType.Type.ToString();
            if (baseTypeName.Contains("DbMigrationsConfiguration"))
            {
                var config = new MigrationConfigInfo
                {
                    ClassName = classDecl.Identifier.Text,
                    HasSeedMethod = HasSeedMethod(classDecl),
                    HasAutomaticMigrations = HasAutomaticMigrationsEnabled(classDecl),
                    Location = classDecl.GetLocation()
                };

                configs.Add(config);
            }
        }

        return configs;
    }

    private bool HasSeedMethod(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.Identifier.Text == "Seed");
    }

    private bool HasAutomaticMigrationsEnabled(ClassDeclarationSyntax classDecl)
    {
        return classDecl.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(a => a.Left.ToString().Contains("AutomaticMigrationsEnabled"));
    }
}

public class MigrationConfigInfo
{
    public string ClassName { get; set; }
    public bool HasSeedMethod { get; set; }
    public bool HasAutomaticMigrations { get; set; }
    public Location Location { get; set; }
}
```

### Seed Data Migration Helper

```csharp
public class SeedDataMigrationHelper
{
    public string GenerateHasDataConfiguration(
        string entityTypeName,
        IEnumerable<PropertyInfo> seedProperties)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// TODO: Migrate seed data to HasData");
        sb.AppendLine($"modelBuilder.Entity<{entityTypeName}>().HasData(");
        sb.AppendLine($"    new {entityTypeName}");
        sb.AppendLine("    {");

        foreach (var prop in seedProperties)
        {
            sb.AppendLine($"        {prop.Name} = /* value */,");
        }

        sb.AppendLine("    }");
        sb.AppendLine(");");

        return sb.ToString();
    }
}
```

### Unit Tests

```csharp
public class DatabaseInitializerRemoverTests
{
    [Fact]
    public async Task Transform_CreateDatabaseIfNotExists_CommentsOutAndAddsTodo()
    {
        // Arrange
        var code = @"
public class AppContext : DbContext
{
    public AppContext()
    {
        Database.SetInitializer(new CreateDatabaseIfNotExists<AppContext>());
    }
}";

        // Act
        var result = await TransformCode(code);

        // Assert
        Assert.Contains("REMOVED:", result);
        Assert.Contains("EnsureCreated", result);
        Assert.DoesNotContain("SetInitializer", result.Replace("REMOVED:", ""));
    }

    [Fact]
    public async Task Transform_MigrateDatabaseToLatestVersion_AddsProperGuidance()
    {
        // Arrange
        var code = @"
public class AppContext : DbContext
{
    static AppContext()
    {
        Database.SetInitializer(new MigrateDatabaseToLatestVersion<AppContext, Configuration>());
    }
}";

        // Act
        var result = await TransformCode(code);

        // Assert
        Assert.Contains("Database.Migrate()", result);
        Assert.Contains("Program.cs", result);
    }

    [Fact]
    public async Task Transform_NullInitializer_RemovesCleanly()
    {
        // Arrange
        var code = @"
public class AppContext : DbContext
{
    static AppContext()
    {
        Database.SetInitializer<AppContext>(null);
    }
}";

        // Act
        var result = await TransformCode(code);

        // Assert
        Assert.Contains("not needed", result);
    }

    [Fact]
    public async Task Transform_DropCreateDatabaseIfModelChanges_AddsWarning()
    {
        // Arrange
        var code = @"
Database.SetInitializer(new DropCreateDatabaseIfModelChanges<AppContext>());";

        // Act
        var result = await TransformCode(code);

        // Assert
        Assert.Contains("does not support automatic recreation", result);
        Assert.Contains("explicit migrations", result);
    }

    [Fact]
    public async Task Detect_DbMigrationsConfiguration_FindsConfigClass()
    {
        // Arrange
        var code = @"
internal sealed class Configuration : DbMigrationsConfiguration<AppContext>
{
    public Configuration()
    {
        AutomaticMigrationsEnabled = true;
    }

    protected override void Seed(AppContext context)
    {
        context.Products.Add(new Product { Name = ""Sample"" });
    }
}";

        // Act
        var detector = new MigrationsConfigurationDetector();
        var configs = await DetectConfigs(code, detector);

        // Assert
        var config = Assert.Single(configs);
        Assert.Equal("Configuration", config.ClassName);
        Assert.True(config.HasSeedMethod);
        Assert.True(config.HasAutomaticMigrations);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
