# [TASK-043] Detect EF6 DbContext Classes

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

- **Depends on:** TASK-002, TASK-004
- **Blocks:** TASK-044, TASK-045, TASK-046

---

## Description

Implement Roslyn analyzer to detect Entity Framework 6 DbContext classes in the codebase. This is the foundation for all EF6 to EF Core migrations, identifying classes that inherit from System.Data.Entity.DbContext and their DbSet properties.

---

## Acceptance Criteria

- [ ] Detect classes inheriting from System.Data.Entity.DbContext
- [ ] Extract all DbSet<T> properties with their entity types
- [ ] Identify OnModelCreating method and fluent API configurations
- [ ] Detect custom constructors (parameterless, connection string-based)
- [ ] Generate DbContextInfo model with all metadata
- [ ] Add to migration report with EF6 context details
- [ ] Unit tests with various DbContext patterns

---

## Technical Notes

### EF6 DbContext Patterns to Detect

**Basic DbContext:**
```csharp
// EF6
using System.Data.Entity;

public class NorthwindContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Product> Products { get; set; }

    public NorthwindContext() : base("name=NorthwindConnection")
    {
    }
}
```

**DbContext with OnModelCreating:**
```csharp
// EF6
public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasRequired(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId);

        modelBuilder.Entity<Order>()
            .HasKey(o => new { o.OrderId, o.CustomerId });
    }
}
```

**DbContext with Multiple Constructors:**
```csharp
// EF6
public class InventoryContext : DbContext
{
    public DbSet<Item> Items { get; set; }

    public InventoryContext() : base()
    {
    }

    public InventoryContext(string connectionString) : base(connectionString)
    {
    }

    public InventoryContext(DbConnection connection) : base(connection, true)
    {
    }
}
```

### Roslyn Detection Implementation

```csharp
public class EF6DbContextDetector
{
    private readonly SemanticModel _semanticModel;

    public async Task<List<DbContextInfo>> DetectDbContextsAsync(
        SyntaxNode root,
        SemanticModel semanticModel)
    {
        var dbContexts = new List<DbContextInfo>();

        var classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDeclarations)
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
            if (classSymbol == null) continue;

            // Check if inherits from System.Data.Entity.DbContext
            if (InheritsFromEF6DbContext(classSymbol))
            {
                var contextInfo = new DbContextInfo
                {
                    ClassName = classSymbol.Name,
                    Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                    FilePath = classDecl.SyntaxTree.FilePath,
                    DbSets = ExtractDbSets(classDecl, semanticModel),
                    Constructors = ExtractConstructors(classDecl, semanticModel),
                    HasOnModelCreating = HasOnModelCreatingMethod(classDecl),
                    FluentApiCalls = ExtractFluentApiCalls(classDecl, semanticModel)
                };

                dbContexts.Add(contextInfo);
            }
        }

        return dbContexts;
    }

    private bool InheritsFromEF6DbContext(INamedTypeSymbol classSymbol)
    {
        var baseType = classSymbol.BaseType;

        while (baseType != null)
        {
            var fullName = baseType.ToDisplayString();
            if (fullName == "System.Data.Entity.DbContext")
            {
                return true;
            }
            baseType = baseType.BaseType;
        }

        return false;
    }

    private List<DbSetInfo> ExtractDbSets(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel)
    {
        var dbSets = new List<DbSetInfo>();

        var properties = classDecl.Members
            .OfType<PropertyDeclarationSyntax>();

        foreach (var property in properties)
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(property);
            if (propertySymbol == null) continue;

            var propertyType = propertySymbol.Type;

            // Check if type is DbSet<T>
            if (propertyType is INamedTypeSymbol namedType &&
                namedType.IsGenericType &&
                namedType.ConstructedFrom.ToDisplayString() == "System.Data.Entity.DbSet<T>")
            {
                var entityType = namedType.TypeArguments[0];

                dbSets.Add(new DbSetInfo
                {
                    PropertyName = propertySymbol.Name,
                    EntityType = entityType.ToDisplayString(),
                    EntityTypeSymbol = entityType
                });
            }
        }

        return dbSets;
    }

    private List<ConstructorInfo> ExtractConstructors(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel)
    {
        var constructors = new List<ConstructorInfo>();

        var ctorDeclarations = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>();

        foreach (var ctor in ctorDeclarations)
        {
            var ctorSymbol = semanticModel.GetDeclaredSymbol(ctor);
            if (ctorSymbol == null) continue;

            var baseInitializer = ctor.Initializer;
            string baseCall = null;

            if (baseInitializer != null && baseInitializer.IsKind(SyntaxKind.BaseConstructorInitializer))
            {
                baseCall = baseInitializer.ToString();
            }

            constructors.Add(new ConstructorInfo
            {
                Parameters = ctorSymbol.Parameters.Select(p => new ParameterInfo
                {
                    Name = p.Name,
                    Type = p.Type.ToDisplayString()
                }).ToList(),
                BaseConstructorCall = baseCall,
                IsParameterless = ctorSymbol.Parameters.Length == 0
            });
        }

        return constructors;
    }

    private bool HasOnModelCreatingMethod(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.Identifier.Text == "OnModelCreating");
    }

    private List<FluentApiCall> ExtractFluentApiCalls(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel)
    {
        var fluentCalls = new List<FluentApiCall>();

        var onModelCreating = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "OnModelCreating");

        if (onModelCreating == null) return fluentCalls;

        // Extract modelBuilder.Entity<T>() chains
        var invocations = onModelCreating.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            var method = symbolInfo.Symbol as IMethodSymbol;

            if (method != null && IsFluentApiMethod(method))
            {
                fluentCalls.Add(new FluentApiCall
                {
                    MethodName = method.Name,
                    FullExpression = invocation.ToString(),
                    EntityType = ExtractEntityTypeFromChain(invocation, semanticModel)
                });
            }
        }

        return fluentCalls;
    }

    private bool IsFluentApiMethod(IMethodSymbol method)
    {
        var fluentApiMethods = new[]
        {
            "HasRequired", "HasOptional", "HasMany", "WithMany",
            "WithRequired", "WithOptional", "HasForeignKey",
            "HasKey", "ToTable", "Property", "HasMaxLength",
            "IsRequired", "HasColumnName", "HasDatabaseGeneratedOption"
        };

        return fluentApiMethods.Contains(method.Name);
    }
}
```

### Model Classes

```csharp
public class DbContextInfo
{
    public string ClassName { get; set; }
    public string Namespace { get; set; }
    public string FilePath { get; set; }
    public List<DbSetInfo> DbSets { get; set; }
    public List<ConstructorInfo> Constructors { get; set; }
    public bool HasOnModelCreating { get; set; }
    public List<FluentApiCall> FluentApiCalls { get; set; }
}

public class DbSetInfo
{
    public string PropertyName { get; set; }
    public string EntityType { get; set; }
    public ITypeSymbol EntityTypeSymbol { get; set; }
}

public class ConstructorInfo
{
    public List<ParameterInfo> Parameters { get; set; }
    public string BaseConstructorCall { get; set; }
    public bool IsParameterless { get; set; }
}

public class ParameterInfo
{
    public string Name { get; set; }
    public string Type { get; set; }
}

public class FluentApiCall
{
    public string MethodName { get; set; }
    public string FullExpression { get; set; }
    public string EntityType { get; set; }
}
```

### Unit Tests

```csharp
public class EF6DbContextDetectorTests
{
    [Fact]
    public async Task DetectDbContextsAsync_BasicDbContext_ReturnsContextInfo()
    {
        // Arrange
        var code = @"
using System.Data.Entity;

namespace TestApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Order> Orders { get; set; }

        public ApplicationDbContext() : base(""DefaultConnection"")
        {
        }
    }

    public class User { public int Id { get; set; } }
    public class Order { public int Id { get; set; } }
}";

        var (tree, semanticModel) = await CreateSemanticModel(code);
        var detector = new EF6DbContextDetector();

        // Act
        var contexts = await detector.DetectDbContextsAsync(tree.GetRoot(), semanticModel);

        // Assert
        Assert.Single(contexts);
        var context = contexts[0];
        Assert.Equal("ApplicationDbContext", context.ClassName);
        Assert.Equal("TestApp.Data", context.Namespace);
        Assert.Equal(2, context.DbSets.Count);
        Assert.Contains(context.DbSets, d => d.PropertyName == "Users" && d.EntityType == "TestApp.Data.User");
        Assert.Contains(context.DbSets, d => d.PropertyName == "Orders" && d.EntityType == "TestApp.Data.Order");
    }

    [Fact]
    public async Task DetectDbContextsAsync_WithOnModelCreating_DetectsFluentApi()
    {
        // Arrange
        var code = @"
using System.Data.Entity;

public class SalesContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>()
            .HasRequired(c => c.Address)
            .WithMany()
            .HasForeignKey(c => c.AddressId);
    }
}

public class Customer
{
    public int Id { get; set; }
    public int AddressId { get; set; }
}";

        var (tree, semanticModel) = await CreateSemanticModel(code);
        var detector = new EF6DbContextDetector();

        // Act
        var contexts = await detector.DetectDbContextsAsync(tree.GetRoot(), semanticModel);

        // Assert
        var context = contexts[0];
        Assert.True(context.HasOnModelCreating);
        Assert.NotEmpty(context.FluentApiCalls);
        Assert.Contains(context.FluentApiCalls, f => f.MethodName == "HasRequired");
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
