# [TASK-049] Configure EF Core Lazy Loading

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

Configure EF Core lazy loading for migrated DbContext classes. EF6 had lazy loading enabled by default, while EF Core requires explicit configuration. This task handles detection of EF6 lazy loading settings and configures EF Core accordingly using either proxy-based lazy loading or generates warnings about explicit loading.

---

## Acceptance Criteria

- [ ] Detect `Configuration.LazyLoadingEnabled` settings in EF6 DbContext
- [ ] Add `.UseLazyLoadingProxies()` to DbContextOptions when lazy loading was enabled
- [ ] Detect `Configuration.ProxyCreationEnabled` settings
- [ ] Add Microsoft.EntityFrameworkCore.Proxies package reference when needed
- [ ] Generate TODO comments for navigation properties that need `virtual` keyword
- [ ] Provide alternative explicit loading pattern as comment when proxies not desired
- [ ] Unit tests for lazy loading configuration detection and transformation

---

## Technical Notes

### Transformation Patterns

**Pattern 1: Lazy Loading Enabled (Default in EF6)**

```csharp
// EF6 - lazy loading is ON by default
public class OrderContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Customer> Customers { get; set; }
}

// EF Core with proxy-based lazy loading
public class OrderContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Customer> Customers { get; set; }

    public OrderContext(DbContextOptions<OrderContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseLazyLoadingProxies();
        }
    }
}

// Entity classes must have virtual navigation properties
public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public virtual Customer Customer { get; set; } // Must be virtual
    public virtual ICollection<OrderItem> Items { get; set; } // Must be virtual
}
```

**Pattern 2: Lazy Loading Explicitly Disabled**

```csharp
// EF6
public class PerformanceContext : DbContext
{
    public PerformanceContext()
    {
        Configuration.LazyLoadingEnabled = false;
        Configuration.ProxyCreationEnabled = false;
    }
}

// EF Core - no lazy loading (default behavior)
public class PerformanceContext : DbContext
{
    public PerformanceContext(DbContextOptions<PerformanceContext> options)
        : base(options)
    {
        // EF Core: Lazy loading is disabled by default
        // Use explicit loading: context.Entry(order).Reference(o => o.Customer).Load();
        // Or eager loading: context.Orders.Include(o => o.Customer)
    }
}
```

**Pattern 3: Mixed Configuration**

```csharp
// EF6
public class MixedContext : DbContext
{
    public MixedContext()
    {
        Configuration.LazyLoadingEnabled = true;
        Configuration.ProxyCreationEnabled = false; // No proxies but lazy loading?
    }
}

// EF Core - ILazyLoader injection approach
public class MixedContext : DbContext
{
    // Note: For lazy loading without proxies, use ILazyLoader injection
    // See: https://docs.microsoft.com/ef/core/querying/related-data/lazy
}

public class Order
{
    private ILazyLoader _lazyLoader;
    private Customer _customer;

    public Order() { }

    public Order(ILazyLoader lazyLoader)
    {
        _lazyLoader = lazyLoader;
    }

    public int Id { get; set; }
    public int CustomerId { get; set; }

    public Customer Customer
    {
        get => _lazyLoader?.Load(this, ref _customer);
        set => _customer = value;
    }
}
```

### Roslyn Transformation Implementation

```csharp
public class LazyLoadingConfigurationTransformer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private bool _lazyLoadingEnabled = true; // EF6 default
    private bool _proxyCreationEnabled = true; // EF6 default
    private bool _hasExplicitLazyLoadingSetting = false;

    public LazyLoadingConfigurationTransformer(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public bool RequiresLazyLoadingProxies => _lazyLoadingEnabled && _proxyCreationEnabled;
    public bool RequiresILazyLoader => _lazyLoadingEnabled && !_proxyCreationEnabled;

    public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        // Detect Configuration.LazyLoadingEnabled = value
        if (IsLazyLoadingAssignment(node, out var value))
        {
            _lazyLoadingEnabled = value;
            _hasExplicitLazyLoadingSetting = true;

            // Remove this assignment (will be handled in OnConfiguring)
            return null;
        }

        // Detect Configuration.ProxyCreationEnabled = value
        if (IsProxyCreationAssignment(node, out var proxyValue))
        {
            _proxyCreationEnabled = proxyValue;

            // Remove this assignment
            return null;
        }

        return base.VisitAssignmentExpression(node);
    }

    private bool IsLazyLoadingAssignment(AssignmentExpressionSyntax node, out bool value)
    {
        value = false;

        if (node.Left is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Name.Identifier.Text == "LazyLoadingEnabled"
                && memberAccess.Expression is MemberAccessExpressionSyntax configAccess
                && configAccess.Name.Identifier.Text == "Configuration")
            {
                if (node.Right is LiteralExpressionSyntax literal)
                {
                    value = literal.Kind() == SyntaxKind.TrueLiteralExpression;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsProxyCreationAssignment(AssignmentExpressionSyntax node, out bool value)
    {
        value = false;

        if (node.Left is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Name.Identifier.Text == "ProxyCreationEnabled"
                && memberAccess.Expression is MemberAccessExpressionSyntax configAccess
                && configAccess.Name.Identifier.Text == "Configuration")
            {
                if (node.Right is LiteralExpressionSyntax literal)
                {
                    value = literal.Kind() == SyntaxKind.TrueLiteralExpression;
                    return true;
                }
            }
        }

        return false;
    }

    public MethodDeclarationSyntax GenerateOnConfiguringMethod(bool hasExisting)
    {
        var statements = new List<StatementSyntax>();

        // Add base call if needed
        if (hasExisting)
        {
            statements.Add(SyntaxFactory.ParseStatement(
                "base.OnConfiguring(optionsBuilder);"));
        }

        // Add lazy loading configuration
        if (_lazyLoadingEnabled && _proxyCreationEnabled)
        {
            statements.Add(SyntaxFactory.IfStatement(
                SyntaxFactory.PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("optionsBuilder"),
                        SyntaxFactory.IdentifierName("IsConfigured"))),
                SyntaxFactory.Block(
                    SyntaxFactory.ParseStatement(
                        "optionsBuilder.UseLazyLoadingProxies();"))));
        }
        else if (!_lazyLoadingEnabled)
        {
            // Add comment about explicit/eager loading
            statements.Add(SyntaxFactory.ParseStatement("// Lazy loading disabled - use Include() for eager loading or Entry().Load() for explicit loading")
                .WithLeadingTrivia(SyntaxFactory.Comment(
                    "// EF Core: Lazy loading is disabled by default. Use Include() for eager loading.")));
        }

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "OnConfiguring")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("optionsBuilder"))
                    .WithType(SyntaxFactory.ParseTypeName("DbContextOptionsBuilder")))
            .WithBody(SyntaxFactory.Block(statements));
    }
}
```

### Package Reference Generator

```csharp
public class LazyLoadingPackageReferenceGenerator
{
    public string GeneratePackageReference()
    {
        return @"<PackageReference Include=""Microsoft.EntityFrameworkCore.Proxies"" Version=""8.0.0"" />";
    }

    public void AddToProjectFile(string projectFilePath)
    {
        var doc = XDocument.Load(projectFilePath);
        var itemGroup = doc.Descendants("ItemGroup")
            .FirstOrDefault(ig => ig.Elements("PackageReference").Any());

        if (itemGroup == null)
        {
            itemGroup = new XElement("ItemGroup");
            doc.Root.Add(itemGroup);
        }

        // Check if already exists
        var existingRef = itemGroup.Elements("PackageReference")
            .FirstOrDefault(pr => pr.Attribute("Include")?.Value == "Microsoft.EntityFrameworkCore.Proxies");

        if (existingRef == null)
        {
            itemGroup.Add(XElement.Parse(GeneratePackageReference()));
            doc.Save(projectFilePath);
        }
    }
}
```

### Navigation Property Virtual Keyword Analyzer

```csharp
public class NavigationPropertyVirtualAnalyzer
{
    public IEnumerable<DiagnosticInfo> AnalyzeForMissingVirtual(
        SemanticModel semanticModel,
        ClassDeclarationSyntax entityClass)
    {
        var diagnostics = new List<DiagnosticInfo>();

        foreach (var property in entityClass.Members.OfType<PropertyDeclarationSyntax>())
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(property);
            if (propertySymbol == null) continue;

            // Check if this is a navigation property
            if (IsNavigationProperty(propertySymbol))
            {
                // Check if virtual
                var hasVirtual = property.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword));

                if (!hasVirtual)
                {
                    diagnostics.Add(new DiagnosticInfo
                    {
                        Message = $"Navigation property '{property.Identifier.Text}' should be virtual for lazy loading proxies",
                        Location = property.GetLocation(),
                        PropertyName = property.Identifier.Text,
                        EntityName = entityClass.Identifier.Text
                    });
                }
            }
        }

        return diagnostics;
    }

    private bool IsNavigationProperty(IPropertySymbol property)
    {
        var type = property.Type;

        // Check for collection types (ICollection<T>, IList<T>, etc.)
        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.IsGenericType)
            {
                var genericDef = namedType.ConstructedFrom.ToString();
                if (genericDef.Contains("ICollection") ||
                    genericDef.Contains("IList") ||
                    genericDef.Contains("IEnumerable") ||
                    genericDef.Contains("List") ||
                    genericDef.Contains("HashSet"))
                {
                    return true;
                }
            }

            // Check for reference navigation (class type with Id property pattern)
            if (namedType.TypeKind == TypeKind.Class && !namedType.IsValueType)
            {
                // Exclude common non-entity types
                var typeName = namedType.Name;
                if (typeName != "String" && typeName != "Object" &&
                    !typeName.StartsWith("Nullable"))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public class DiagnosticInfo
{
    public string Message { get; set; }
    public Location Location { get; set; }
    public string PropertyName { get; set; }
    public string EntityName { get; set; }
}
```

### Unit Tests

```csharp
public class LazyLoadingConfigurationTransformerTests
{
    [Fact]
    public async Task Transform_DefaultLazyLoading_AddsUseLazyLoadingProxies()
    {
        // Arrange
        var code = @"
public class OrderContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
}";

        // Act
        var result = await TransformCode(code);

        // Assert
        Assert.Contains("UseLazyLoadingProxies", result);
    }

    [Fact]
    public async Task Transform_LazyLoadingDisabled_DoesNotAddProxies()
    {
        // Arrange
        var code = @"
public class OrderContext : DbContext
{
    public OrderContext()
    {
        Configuration.LazyLoadingEnabled = false;
    }
}";

        // Act
        var result = await TransformCode(code);

        // Assert
        Assert.DoesNotContain("UseLazyLoadingProxies", result);
        Assert.Contains("Lazy loading disabled", result);
    }

    [Fact]
    public async Task Transform_ProxyCreationDisabled_AddsILazyLoaderComment()
    {
        // Arrange
        var code = @"
public class OrderContext : DbContext
{
    public OrderContext()
    {
        Configuration.LazyLoadingEnabled = true;
        Configuration.ProxyCreationEnabled = false;
    }
}";

        // Act
        var result = await TransformCode(code);

        // Assert
        Assert.DoesNotContain("UseLazyLoadingProxies", result);
        Assert.Contains("ILazyLoader", result);
    }

    [Fact]
    public async Task Analyze_NonVirtualNavigationProperty_ReportsDiagnostic()
    {
        // Arrange
        var code = @"
public class Order
{
    public int Id { get; set; }
    public Customer Customer { get; set; } // Not virtual!
}";

        // Act
        var analyzer = new NavigationPropertyVirtualAnalyzer();
        var diagnostics = await AnalyzeCode(code, analyzer);

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("should be virtual", diagnostics[0].Message);
    }

    [Fact]
    public async Task Analyze_VirtualNavigationProperty_NoDiagnostic()
    {
        // Arrange
        var code = @"
public class Order
{
    public int Id { get; set; }
    public virtual Customer Customer { get; set; }
}";

        // Act
        var analyzer = new NavigationPropertyVirtualAnalyzer();
        var diagnostics = await AnalyzeCode(code, analyzer);

        // Assert
        Assert.Empty(diagnostics);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
