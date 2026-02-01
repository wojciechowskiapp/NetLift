# [TASK-044] Transform DbContext Constructor Pattern

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P0 |
| **Estimate** | M |
| **Sprint** | 5 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-043
- **Blocks:** TASK-045, TASK-050

---

## Description

Transform EF6 DbContext constructors to EF Core pattern using DbContextOptions<T>. This includes removing connection string-based constructors and adding the modern dependency injection pattern.

---

## Acceptance Criteria

- [ ] Replace base("connectionString") with DbContextOptions<T> constructor
- [ ] Remove DbConnection-based constructors
- [ ] Add protected parameterless constructor for tooling support
- [ ] Generate using directive for Microsoft.EntityFrameworkCore
- [ ] Remove using System.Data.Entity
- [ ] Update all instantiation sites (if detected)
- [ ] Unit tests with various constructor patterns

---

## Technical Notes

### Transformation Patterns

**Pattern 1: Simple Connection String Constructor**

```csharp
// EF6
using System.Data.Entity;

public class NorthwindContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }

    public NorthwindContext() : base("name=NorthwindConnection")
    {
    }
}

// EF Core
using Microsoft.EntityFrameworkCore;

public class NorthwindContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }

    public NorthwindContext(DbContextOptions<NorthwindContext> options)
        : base(options)
    {
    }

    // For design-time tools (migrations, scaffolding)
    protected NorthwindContext()
    {
    }
}
```

**Pattern 2: Multiple Constructors**

```csharp
// EF6
public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public ApplicationDbContext() : base()
    {
    }

    public ApplicationDbContext(string connectionString) : base(connectionString)
    {
    }

    public ApplicationDbContext(DbConnection connection, bool contextOwnsConnection)
        : base(connection, contextOwnsConnection)
    {
    }
}

// EF Core
public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected ApplicationDbContext()
    {
    }
}
```

**Pattern 3: DbContext with Initialization Logic**

```csharp
// EF6
public class InventoryContext : DbContext
{
    public DbSet<Product> Products { get; set; }

    public InventoryContext() : base("InventoryDb")
    {
        Database.SetInitializer(new MigrateDatabaseToLatestVersion<InventoryContext, Configuration>());
        Configuration.LazyLoadingEnabled = false;
        Configuration.ProxyCreationEnabled = false;
    }
}

// EF Core
public class InventoryContext : DbContext
{
    public DbSet<Product> Products { get; set; }

    public InventoryContext(DbContextOptions<InventoryContext> options)
        : base(options)
    {
        // EF Core: Database initialization handled in Program.cs/Startup
        // Configuration moved to OnConfiguring or DbContextOptions
    }

    protected InventoryContext()
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback for design-time tools
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=InventoryDb");
        }

        optionsBuilder.UseLazyLoadingProxies(false);
    }
}
```

### Roslyn Transformation Implementation

```csharp
public class DbContextConstructorTransformer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly string _contextClassName;

    public DbContextConstructorTransformer(
        SemanticModel semanticModel,
        string contextClassName)
    {
        _semanticModel = semanticModel;
        _contextClassName = contextClassName;
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var classSymbol = _semanticModel.GetDeclaredSymbol(node);

        // Only transform if this is the DbContext class
        if (classSymbol?.Name != _contextClassName)
        {
            return base.VisitClassDeclaration(node);
        }

        // Remove all existing constructors
        var membersWithoutCtors = node.Members
            .Where(m => m is not ConstructorDeclarationSyntax)
            .ToList();

        // Create new EF Core constructors
        var newConstructors = CreateEFCoreConstructors(node);

        var newMembers = new List<MemberDeclarationSyntax>();
        newMembers.AddRange(newConstructors);
        newMembers.AddRange(membersWithoutCtors);

        // Add OnConfiguring if needed for design-time support
        var onConfiguring = CreateOnConfiguringMethod(node);
        if (onConfiguring != null)
        {
            newMembers.Add(onConfiguring);
        }

        var updatedClass = node.WithMembers(SyntaxFactory.List(newMembers));

        return base.VisitClassDeclaration(updatedClass);
    }

    private List<ConstructorDeclarationSyntax> CreateEFCoreConstructors(
        ClassDeclarationSyntax classDecl)
    {
        var constructors = new List<ConstructorDeclarationSyntax>();

        // Primary constructor with DbContextOptions<T>
        var primaryCtor = SyntaxFactory.ConstructorDeclaration(_contextClassName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("options"))
                    .WithType(SyntaxFactory.ParseTypeName(
                        $"DbContextOptions<{_contextClassName}>")))
            .WithInitializer(SyntaxFactory.ConstructorInitializer(
                SyntaxKind.BaseConstructorInitializer,
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.IdentifierName("options"))))))
            .WithBody(SyntaxFactory.Block());

        constructors.Add(primaryCtor);

        // Protected parameterless constructor for design-time tools
        var parameterlessCtor = SyntaxFactory.ConstructorDeclaration(_contextClassName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword))
            .WithBody(SyntaxFactory.Block());

        constructors.Add(parameterlessCtor);

        return constructors;
    }

    private MethodDeclarationSyntax CreateOnConfiguringMethod(
        ClassDeclarationSyntax classDecl)
    {
        // Check if OnConfiguring already exists
        var existingOnConfiguring = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "OnConfiguring");

        if (existingOnConfiguring != null)
        {
            return null; // Already has OnConfiguring
        }

        // Extract connection string from old constructor
        var connectionString = ExtractConnectionStringFromConstructor(classDecl);

        if (string.IsNullOrEmpty(connectionString))
        {
            return null; // No fallback connection string available
        }

        var onConfiguring = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "OnConfiguring")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("optionsBuilder"))
                    .WithType(SyntaxFactory.ParseTypeName("DbContextOptionsBuilder")))
            .WithBody(SyntaxFactory.Block(
                // if (!optionsBuilder.IsConfigured)
                SyntaxFactory.IfStatement(
                    SyntaxFactory.PrefixUnaryExpression(
                        SyntaxKind.LogicalNotExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("optionsBuilder"),
                            SyntaxFactory.IdentifierName("IsConfigured"))),
                    SyntaxFactory.Block(
                        SyntaxFactory.ParseStatement(
                            $"optionsBuilder.UseSqlServer(\"{connectionString}\");"))),
                // Add comment
                SyntaxFactory.ParseStatement("// Fallback for design-time tools")
                    .WithLeadingTrivia(SyntaxFactory.Comment("// Fallback for design-time tools"))
            ));

        return onConfiguring;
    }

    private string ExtractConnectionStringFromConstructor(ClassDeclarationSyntax classDecl)
    {
        var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();

        foreach (var ctor in constructors)
        {
            var baseInitializer = ctor.Initializer;
            if (baseInitializer?.ArgumentList.Arguments.Count > 0)
            {
                var firstArg = baseInitializer.ArgumentList.Arguments[0];

                // Extract literal string
                if (firstArg.Expression is LiteralExpressionSyntax literal)
                {
                    var value = literal.Token.ValueText;

                    // Convert "name=ConnectionName" to actual connection string
                    if (value.StartsWith("name="))
                    {
                        var connName = value.Substring(5);
                        return $"Server=(localdb)\\mssqllocaldb;Database={connName}";
                    }

                    return value;
                }
            }
        }

        return null;
    }

    public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node)
    {
        var namespaceName = node.Name.ToString();

        // Remove EF6 using
        if (namespaceName == "System.Data.Entity")
        {
            return null;
        }

        return base.VisitUsingDirective(node);
    }
}
```

### Adding EF Core Using Directive

```csharp
public class DbContextUsingDirectiveTransformer
{
    public CompilationUnitSyntax AddEFCoreUsings(CompilationUnitSyntax root)
    {
        var efCoreUsing = SyntaxFactory.UsingDirective(
            SyntaxFactory.ParseName("Microsoft.EntityFrameworkCore"));

        // Check if already exists
        var hasEFCoreUsing = root.Usings.Any(u =>
            u.Name.ToString() == "Microsoft.EntityFrameworkCore");

        if (hasEFCoreUsing)
        {
            return root;
        }

        // Add after System usings
        var systemUsings = root.Usings
            .Where(u => u.Name.ToString().StartsWith("System"))
            .ToList();

        var otherUsings = root.Usings
            .Where(u => !u.Name.ToString().StartsWith("System"))
            .ToList();

        var newUsings = systemUsings
            .Concat(new[] { efCoreUsing })
            .Concat(otherUsings);

        return root.WithUsings(SyntaxFactory.List(newUsings));
    }
}
```

### Unit Tests

```csharp
public class DbContextConstructorTransformerTests
{
    [Fact]
    public async Task Transform_SimpleConnectionStringConstructor_TransformsToDbContextOptions()
    {
        // Arrange
        var code = @"
using System.Data.Entity;

public class AppContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public AppContext() : base(""DefaultConnection"")
    {
    }
}";

        var expected = @"
using Microsoft.EntityFrameworkCore;

public class AppContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public AppContext(DbContextOptions<AppContext> options) : base(options)
    {
    }

    protected AppContext()
    {
    }
}";

        // Act
        var result = await TransformDbContext(code, "AppContext");

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_MultipleConstructors_ReplacesWithSinglePattern()
    {
        // Arrange
        var code = @"
using System.Data.Entity;

public class SalesContext : DbContext
{
    public SalesContext() : base()
    {
    }

    public SalesContext(string conn) : base(conn)
    {
    }
}";

        // Act
        var result = await TransformDbContext(code, "SalesContext");

        // Assert
        Assert.Contains("DbContextOptions<SalesContext>", result);
        Assert.DoesNotContain("string conn", result);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
