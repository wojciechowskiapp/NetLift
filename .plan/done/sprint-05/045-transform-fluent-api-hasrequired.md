# [TASK-045] Transform Fluent API HasRequired/HasOptional

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P0 |
| **Estimate** | L |
| **Sprint** | 5 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-043, TASK-044
- **Blocks:** TASK-046, TASK-050

---

## Description

Transform EF6 relationship fluent API from HasRequired/HasOptional pattern to EF Core's HasOne/HasMany pattern. This is one of the most significant breaking changes between EF6 and EF Core.

---

## Acceptance Criteria

- [ ] Convert HasRequired().WithMany() to HasOne().WithMany()
- [ ] Convert HasRequired().WithOptional() to HasOne().WithOne()
- [ ] Convert HasOptional().WithMany() to HasOne().WithMany()
- [ ] Convert HasOptional().WithOptional() to HasOne().WithOne()
- [ ] Preserve HasForeignKey() configuration
- [ ] Preserve OnDelete() cascade behavior
- [ ] Handle WillCascadeOnDelete() conversion
- [ ] Unit tests with all relationship patterns

---

## Technical Notes

### Transformation Patterns

**Pattern 1: Required One-to-Many**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>()
        .HasRequired(o => o.Customer)
        .WithMany(c => c.Orders)
        .HasForeignKey(o => o.CustomerId);
}

// EF Core
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>()
        .HasOne(o => o.Customer)
        .WithMany(c => c.Orders)
        .HasForeignKey(o => o.CustomerId);
}
```

**Pattern 2: Optional One-to-Many**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Employee>()
        .HasOptional(e => e.Manager)
        .WithMany(m => m.Subordinates)
        .HasForeignKey(e => e.ManagerId);
}

// EF Core
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Employee>()
        .HasOne(e => e.Manager)
        .WithMany(m => m.Subordinates)
        .HasForeignKey(e => e.ManagerId);
}
```

**Pattern 3: Required One-to-One**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>()
        .HasRequired(u => u.Profile)
        .WithRequiredPrincipal(p => p.User);
}

// EF Core
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>()
        .HasOne(u => u.Profile)
        .WithOne(p => p.User)
        .HasForeignKey<Profile>(p => p.UserId)
        .IsRequired();
}
```

**Pattern 4: Optional One-to-One**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Person>()
        .HasOptional(p => p.License)
        .WithRequired(l => l.Person);
}

// EF Core
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Person>()
        .HasOne(p => p.License)
        .WithOne(l => l.Person)
        .HasForeignKey<License>(l => l.PersonId);
}
```

**Pattern 5: Cascade Delete Behavior**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>()
        .HasRequired(o => o.Customer)
        .WithMany(c => c.Orders)
        .HasForeignKey(o => o.CustomerId)
        .WillCascadeOnDelete(false);
}

// EF Core
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>()
        .HasOne(o => o.Customer)
        .WithMany(c => c.Orders)
        .HasForeignKey(o => o.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);
}
```

**Pattern 6: Multiple Foreign Keys (Composite)**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<OrderDetail>()
        .HasRequired(od => od.Order)
        .WithMany(o => o.OrderDetails)
        .HasForeignKey(od => new { od.OrderId, od.ProductId });
}

// EF Core
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<OrderDetail>()
        .HasOne(od => od.Order)
        .WithMany(o => o.OrderDetails)
        .HasForeignKey(od => new { od.OrderId, od.ProductId });
}
```

**Pattern 7: WithRequiredPrincipal/WithRequiredDependent**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>()
        .HasRequired(u => u.Profile)
        .WithRequiredPrincipal(p => p.User);

    modelBuilder.Entity<BlogPost>()
        .HasRequired(b => b.Author)
        .WithRequiredDependent(a => a.Blog);
}

// EF Core
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>()
        .HasOne(u => u.Profile)
        .WithOne(p => p.User)
        .HasForeignKey<Profile>(p => p.UserId)
        .IsRequired();

    modelBuilder.Entity<BlogPost>()
        .HasOne(b => b.Author)
        .WithOne(a => a.Blog)
        .HasForeignKey<BlogPost>(b => b.AuthorId)
        .IsRequired();
}
```

### Roslyn Transformation Implementation

```csharp
public class FluentApiHasRequiredTransformer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<string, RelationshipInfo> _relationships;

    public FluentApiHasRequiredTransformer(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
        _relationships = new Dictionary<string, RelationshipInfo>();
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        var method = symbolInfo.Symbol as IMethodSymbol;

        if (method == null)
        {
            return base.VisitInvocationExpression(node);
        }

        // Detect relationship chain start
        if (IsEntityConfigurationMethod(method))
        {
            var chain = ExtractFluentChain(node);
            var transformed = TransformRelationshipChain(chain);
            return transformed ?? base.VisitInvocationExpression(node);
        }

        return base.VisitInvocationExpression(node);
    }

    private bool IsEntityConfigurationMethod(IMethodSymbol method)
    {
        return method.Name == "HasRequired" ||
               method.Name == "HasOptional" ||
               method.Name == "HasMany";
    }

    private FluentChain ExtractFluentChain(InvocationExpressionSyntax node)
    {
        var chain = new FluentChain();
        var current = node;

        while (current != null)
        {
            var method = GetMethodName(current);
            chain.Methods.Add(new ChainMethod
            {
                Name = method,
                Arguments = current.ArgumentList.Arguments.ToList(),
                FullExpression = current
            });

            // Walk up the chain
            if (current.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                current = memberAccess.Expression as InvocationExpressionSyntax;
            }
            else
            {
                break;
            }
        }

        chain.Methods.Reverse();
        return chain;
    }

    private InvocationExpressionSyntax TransformRelationshipChain(FluentChain chain)
    {
        var methodNames = chain.Methods.Select(m => m.Name).ToList();

        // Pattern: HasRequired().WithMany()
        if (methodNames.Contains("HasRequired") && methodNames.Contains("WithMany"))
        {
            return TransformHasRequiredWithMany(chain);
        }

        // Pattern: HasRequired().WithOptional()
        if (methodNames.Contains("HasRequired") &&
            (methodNames.Contains("WithOptional") || methodNames.Contains("WithRequiredPrincipal")))
        {
            return TransformHasRequiredWithOne(chain);
        }

        // Pattern: HasOptional().WithMany()
        if (methodNames.Contains("HasOptional") && methodNames.Contains("WithMany"))
        {
            return TransformHasOptionalWithMany(chain);
        }

        // Pattern: HasOptional().WithOptional()
        if (methodNames.Contains("HasOptional") && methodNames.Contains("WithOptional"))
        {
            return TransformHasOptionalWithOne(chain);
        }

        return null;
    }

    private InvocationExpressionSyntax TransformHasRequiredWithMany(FluentChain chain)
    {
        var hasRequired = chain.Methods.First(m => m.Name == "HasRequired");
        var withMany = chain.Methods.First(m => m.Name == "WithMany");

        // Build: Entity<T>().HasOne(...).WithMany(...)
        var newChain = SyntaxFactory.ParseExpression(
            chain.EntityExpression.ToString());

        // HasOne
        newChain = CreateMethodCall(newChain, "HasOne", hasRequired.Arguments);

        // WithMany
        newChain = CreateMethodCall(newChain, "WithMany", withMany.Arguments);

        // Add other methods (HasForeignKey, OnDelete, etc.)
        foreach (var method in chain.Methods)
        {
            if (method.Name == "HasForeignKey")
            {
                newChain = CreateMethodCall(newChain, "HasForeignKey", method.Arguments);
            }
            else if (method.Name == "WillCascadeOnDelete")
            {
                newChain = TransformWillCascadeOnDelete(newChain, method);
            }
        }

        return newChain as InvocationExpressionSyntax;
    }

    private InvocationExpressionSyntax TransformHasRequiredWithOne(FluentChain chain)
    {
        var hasRequired = chain.Methods.First(m => m.Name == "HasRequired");
        var withMethod = chain.Methods.First(m =>
            m.Name == "WithOptional" ||
            m.Name == "WithRequiredPrincipal" ||
            m.Name == "WithRequiredDependent");

        var newChain = SyntaxFactory.ParseExpression(
            chain.EntityExpression.ToString());

        // HasOne
        newChain = CreateMethodCall(newChain, "HasOne", hasRequired.Arguments);

        // WithOne
        newChain = CreateMethodCall(newChain, "WithOne", withMethod.Arguments);

        // Add HasForeignKey with type parameter for one-to-one
        var foreignKeyMethod = chain.Methods.FirstOrDefault(m => m.Name == "HasForeignKey");
        if (foreignKeyMethod != null)
        {
            // Determine dependent type
            var dependentType = InferDependentType(chain);
            newChain = CreateMethodCall(
                newChain,
                $"HasForeignKey<{dependentType}>",
                foreignKeyMethod.Arguments);
        }

        // IsRequired() for required relationships
        newChain = CreateMethodCall(newChain, "IsRequired", new List<ArgumentSyntax>());

        return newChain as InvocationExpressionSyntax;
    }

    private InvocationExpressionSyntax TransformHasOptionalWithMany(FluentChain chain)
    {
        var hasOptional = chain.Methods.First(m => m.Name == "HasOptional");
        var withMany = chain.Methods.First(m => m.Name == "WithMany");

        var newChain = SyntaxFactory.ParseExpression(
            chain.EntityExpression.ToString());

        newChain = CreateMethodCall(newChain, "HasOne", hasOptional.Arguments);
        newChain = CreateMethodCall(newChain, "WithMany", withMany.Arguments);

        // Add other configurations
        foreach (var method in chain.Methods)
        {
            if (method.Name == "HasForeignKey")
            {
                newChain = CreateMethodCall(newChain, "HasForeignKey", method.Arguments);
            }
            else if (method.Name == "WillCascadeOnDelete")
            {
                newChain = TransformWillCascadeOnDelete(newChain, method);
            }
        }

        return newChain as InvocationExpressionSyntax;
    }

    private ExpressionSyntax TransformWillCascadeOnDelete(
        ExpressionSyntax chain,
        ChainMethod method)
    {
        var arg = method.Arguments.FirstOrDefault();
        bool cascade = true;

        if (arg != null && arg.Expression is LiteralExpressionSyntax literal)
        {
            cascade = literal.Token.ValueText == "true";
        }

        var deleteBehavior = cascade ? "DeleteBehavior.Cascade" : "DeleteBehavior.Restrict";

        return CreateMethodCall(
            chain,
            "OnDelete",
            new List<ArgumentSyntax>
            {
                SyntaxFactory.Argument(SyntaxFactory.ParseExpression(deleteBehavior))
            });
    }

    private ExpressionSyntax CreateMethodCall(
        ExpressionSyntax expression,
        string methodName,
        List<ArgumentSyntax> arguments)
    {
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                SyntaxFactory.IdentifierName(methodName)),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(arguments)));
    }

    private string InferDependentType(FluentChain chain)
    {
        // Logic to determine which entity is the dependent
        // Usually the one with the foreign key property
        return "DependentEntity"; // Simplified
    }
}

public class FluentChain
{
    public List<ChainMethod> Methods { get; set; } = new();
    public ExpressionSyntax EntityExpression { get; set; }
}

public class ChainMethod
{
    public string Name { get; set; }
    public List<ArgumentSyntax> Arguments { get; set; }
    public InvocationExpressionSyntax FullExpression { get; set; }
}
```

### Parameter Transformation for DbModelBuilder to ModelBuilder

```csharp
public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
{
    if (node.Identifier.Text != "OnModelCreating")
    {
        return base.VisitMethodDeclaration(node);
    }

    // Change parameter type from DbModelBuilder to ModelBuilder
    var parameter = node.ParameterList.Parameters.FirstOrDefault();
    if (parameter != null)
    {
        var newParameter = parameter.WithType(
            SyntaxFactory.ParseTypeName("ModelBuilder"));

        var newParameterList = node.ParameterList
            .WithParameters(SyntaxFactory.SingletonSeparatedList(newParameter));

        node = node.WithParameterList(newParameterList);
    }

    return base.VisitMethodDeclaration(node);
}
```

### Unit Tests

```csharp
public class FluentApiHasRequiredTransformerTests
{
    [Fact]
    public async Task Transform_HasRequiredWithMany_ConvertsToHasOneWithMany()
    {
        // Arrange
        var code = @"
modelBuilder.Entity<Order>()
    .HasRequired(o => o.Customer)
    .WithMany(c => c.Orders)
    .HasForeignKey(o => o.CustomerId);";

        var expected = @"
modelBuilder.Entity<Order>()
    .HasOne(o => o.Customer)
    .WithMany(c => c.Orders)
    .HasForeignKey(o => o.CustomerId);";

        // Act
        var result = await TransformFluentApi(code);

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_WillCascadeOnDelete_ConvertsToOnDelete()
    {
        // Arrange
        var code = @"
modelBuilder.Entity<Order>()
    .HasRequired(o => o.Customer)
    .WithMany(c => c.Orders)
    .WillCascadeOnDelete(false);";

        var expected = @"
modelBuilder.Entity<Order>()
    .HasOne(o => o.Customer)
    .WithMany(c => c.Orders)
    .OnDelete(DeleteBehavior.Restrict);";

        // Act
        var result = await TransformFluentApi(code);

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_HasRequiredWithOptional_ConvertsToOneToOne()
    {
        // Arrange
        var code = @"
modelBuilder.Entity<User>()
    .HasRequired(u => u.Profile)
    .WithRequiredPrincipal(p => p.User);";

        var expected = @"
modelBuilder.Entity<User>()
    .HasOne(u => u.Profile)
    .WithOne(p => p.User)
    .HasForeignKey<Profile>(p => p.UserId)
    .IsRequired();";

        // Act
        var result = await TransformFluentApi(code);

        // Assert
        Assert.Contains("HasOne", result);
        Assert.Contains("WithOne", result);
        Assert.Contains("IsRequired", result);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
