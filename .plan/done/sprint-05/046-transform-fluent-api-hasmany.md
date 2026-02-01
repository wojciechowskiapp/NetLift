# [TASK-046] Transform Fluent API HasMany and WithMany

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

- **Depends on:** TASK-043, TASK-045
- **Blocks:** TASK-050

---

## Description

Transform EF6 many-to-many relationship configurations from HasMany().WithMany() pattern to EF Core's approach. EF Core 5.0+ supports direct many-to-many without explicit join entities, while earlier versions require manual configuration.

---

## Acceptance Criteria

- [ ] Detect HasMany().WithMany() many-to-many relationships
- [ ] Transform to EF Core 5.0+ UsingEntity() pattern
- [ ] Handle custom join table names (Map().ToTable())
- [ ] Handle custom column names (MapLeftKey/MapRightKey)
- [ ] Preserve existing HasMany().WithRequired/WithOptional patterns
- [ ] Generate using Microsoft.EntityFrameworkCore
- [ ] Unit tests with various many-to-many patterns

---

## Technical Notes

### Transformation Patterns

**Pattern 1: Simple Many-to-Many (EF Core 5.0+)**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Student>()
        .HasMany(s => s.Courses)
        .WithMany(c => c.Students);
}

// EF Core 5.0+
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Student>()
        .HasMany(s => s.Courses)
        .WithMany(c => c.Students);
}
```

**Pattern 2: Many-to-Many with Custom Table Name**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Student>()
        .HasMany(s => s.Courses)
        .WithMany(c => c.Students)
        .Map(m =>
        {
            m.ToTable("StudentCourseEnrollment");
        });
}

// EF Core 5.0+
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Student>()
        .HasMany(s => s.Courses)
        .WithMany(c => c.Students)
        .UsingEntity(j => j.ToTable("StudentCourseEnrollment"));
}
```

**Pattern 3: Many-to-Many with Custom Column Names**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Student>()
        .HasMany(s => s.Courses)
        .WithMany(c => c.Students)
        .Map(m =>
        {
            m.ToTable("StudentCourse");
            m.MapLeftKey("StudentId");
            m.MapRightKey("CourseId");
        });
}

// EF Core 5.0+
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Student>()
        .HasMany(s => s.Courses)
        .WithMany(c => c.Students)
        .UsingEntity(j =>
        {
            j.ToTable("StudentCourse");
            j.Property("StudentId").HasColumnName("StudentId");
            j.Property("CourseId").HasColumnName("CourseId");
        });
}
```

**Pattern 4: Many-to-Many with Composite Keys**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Author>()
        .HasMany(a => a.Books)
        .WithMany(b => b.Authors)
        .Map(m =>
        {
            m.ToTable("BookAuthors");
            m.MapLeftKey(new[] { "AuthorId", "AuthorCountry" });
            m.MapRightKey(new[] { "ISBN", "Edition" });
        });
}

// EF Core 5.0+
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Author>()
        .HasMany(a => a.Books)
        .WithMany(b => b.Authors)
        .UsingEntity<Dictionary<string, object>>(
            "BookAuthors",
            j => j.HasOne<Book>().WithMany()
                .HasForeignKey("ISBN", "Edition"),
            j => j.HasOne<Author>().WithMany()
                .HasForeignKey("AuthorId", "AuthorCountry"),
            j =>
            {
                j.ToTable("BookAuthors");
                j.HasKey("AuthorId", "AuthorCountry", "ISBN", "Edition");
            });
}
```

**Pattern 5: Explicit Join Entity (EF Core < 5.0 or with payload)**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Student>()
        .HasMany(s => s.Courses)
        .WithMany(c => c.Students);
}

// EF Core (Pre-5.0 or with additional columns)
// Create explicit join entity
public class StudentCourse
{
    public int StudentId { get; set; }
    public Student Student { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; }

    // Additional columns
    public DateTime EnrolledDate { get; set; }
    public string Grade { get; set; }
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<StudentCourse>()
        .HasKey(sc => new { sc.StudentId, sc.CourseId });

    modelBuilder.Entity<StudentCourse>()
        .HasOne(sc => sc.Student)
        .WithMany(s => s.StudentCourses)
        .HasForeignKey(sc => sc.StudentId);

    modelBuilder.Entity<StudentCourse>()
        .HasOne(sc => sc.Course)
        .WithMany(c => c.StudentCourses)
        .HasForeignKey(sc => sc.CourseId);
}
```

**Pattern 6: HasMany with Required/Optional (Not Many-to-Many)**

```csharp
// EF6
protected override void OnModelCreating(DbModelBuilder modelBuilder)
{
    modelBuilder.Entity<Department>()
        .HasMany(d => d.Employees)
        .WithRequired(e => e.Department)
        .HasForeignKey(e => e.DepartmentId);
}

// EF Core
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Department>()
        .HasMany(d => d.Employees)
        .WithOne(e => e.Department)
        .HasForeignKey(e => e.DepartmentId)
        .IsRequired();
}
```

### Roslyn Transformation Implementation

```csharp
public class FluentApiHasManyTransformer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly string _efCoreVersion;

    public FluentApiHasManyTransformer(
        SemanticModel semanticModel,
        string efCoreVersion = "5.0")
    {
        _semanticModel = semanticModel;
        _efCoreVersion = efCoreVersion;
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var chain = ExtractFluentChain(node);

        if (IsManyToManyPattern(chain))
        {
            return TransformManyToMany(chain);
        }

        if (IsHasManyWithRequiredOptional(chain))
        {
            return TransformHasManyWithOne(chain);
        }

        return base.VisitInvocationExpression(node);
    }

    private bool IsManyToManyPattern(FluentChain chain)
    {
        return chain.Methods.Any(m => m.Name == "HasMany") &&
               chain.Methods.Any(m => m.Name == "WithMany");
    }

    private bool IsHasManyWithRequiredOptional(FluentChain chain)
    {
        return chain.Methods.Any(m => m.Name == "HasMany") &&
               (chain.Methods.Any(m => m.Name == "WithRequired") ||
                chain.Methods.Any(m => m.Name == "WithOptional"));
    }

    private InvocationExpressionSyntax TransformManyToMany(FluentChain chain)
    {
        var hasMany = chain.Methods.First(m => m.Name == "HasMany");
        var withMany = chain.Methods.First(m => m.Name == "WithMany");
        var mapMethod = chain.Methods.FirstOrDefault(m => m.Name == "Map");

        var newChain = SyntaxFactory.ParseExpression(
            chain.EntityExpression.ToString());

        // HasMany
        newChain = CreateMethodCall(newChain, "HasMany", hasMany.Arguments);

        // WithMany
        newChain = CreateMethodCall(newChain, "WithMany", withMany.Arguments);

        // Handle Map configuration
        if (mapMethod != null)
        {
            var mapConfig = ExtractMapConfiguration(mapMethod);
            newChain = CreateUsingEntityCall(newChain, mapConfig);
        }

        return newChain as InvocationExpressionSyntax;
    }

    private MapConfiguration ExtractMapConfiguration(ChainMethod mapMethod)
    {
        var config = new MapConfiguration();

        // Extract lambda body
        if (mapMethod.Arguments.FirstOrDefault()?.Expression is SimpleLambdaExpressionSyntax lambda)
        {
            var body = lambda.Body;

            // Look for ToTable call
            var toTableCall = FindMethodCall(body, "ToTable");
            if (toTableCall != null && toTableCall.ArgumentList.Arguments.Count > 0)
            {
                var arg = toTableCall.ArgumentList.Arguments[0];
                if (arg.Expression is LiteralExpressionSyntax literal)
                {
                    config.TableName = literal.Token.ValueText;
                }
            }

            // Look for MapLeftKey
            var leftKeyCall = FindMethodCall(body, "MapLeftKey");
            if (leftKeyCall != null)
            {
                config.LeftKeys = ExtractKeyNames(leftKeyCall);
            }

            // Look for MapRightKey
            var rightKeyCall = FindMethodCall(body, "MapRightKey");
            if (rightKeyCall != null)
            {
                config.RightKeys = ExtractKeyNames(rightKeyCall);
            }
        }

        return config;
    }

    private List<string> ExtractKeyNames(InvocationExpressionSyntax invocation)
    {
        var keys = new List<string>();

        if (invocation.ArgumentList.Arguments.Count == 0)
            return keys;

        var arg = invocation.ArgumentList.Arguments[0];

        // Single key: MapLeftKey("KeyName")
        if (arg.Expression is LiteralExpressionSyntax literal)
        {
            keys.Add(literal.Token.ValueText);
        }
        // Array: MapLeftKey(new[] { "Key1", "Key2" })
        else if (arg.Expression is ArrayCreationExpressionSyntax arrayCreation)
        {
            if (arrayCreation.Initializer != null)
            {
                foreach (var expr in arrayCreation.Initializer.Expressions)
                {
                    if (expr is LiteralExpressionSyntax keyLiteral)
                    {
                        keys.Add(keyLiteral.Token.ValueText);
                    }
                }
            }
        }

        return keys;
    }

    private ExpressionSyntax CreateUsingEntityCall(
        ExpressionSyntax chain,
        MapConfiguration config)
    {
        if (config.LeftKeys.Count == 0 && config.RightKeys.Count == 0)
        {
            // Simple: UsingEntity(j => j.ToTable("TableName"))
            var lambda = $"j => j.ToTable(\"{config.TableName}\")";

            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    chain,
                    SyntaxFactory.IdentifierName("UsingEntity")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.ParseExpression(lambda)))));
        }
        else
        {
            // Complex: UsingEntity with full configuration
            var usingEntityConfig = BuildComplexUsingEntity(config);
            return SyntaxFactory.ParseExpression(
                $"{chain}.{usingEntityConfig}");
        }
    }

    private string BuildComplexUsingEntity(MapConfiguration config)
    {
        var leftKeys = string.Join(", ", config.LeftKeys.Select(k => $"\"{k}\""));
        var rightKeys = string.Join(", ", config.RightKeys.Select(k => $"\"{k}\""));

        return $@"UsingEntity(j =>
{{
    j.ToTable(""{config.TableName}"");
    {(config.LeftKeys.Any() ? $"j.Property(\"LeftKey\").HasColumnName({leftKeys});" : "")}
    {(config.RightKeys.Any() ? $"j.Property(\"RightKey\").HasColumnName({rightKeys});" : "")}
}})";
    }

    private InvocationExpressionSyntax TransformHasManyWithOne(FluentChain chain)
    {
        var hasMany = chain.Methods.First(m => m.Name == "HasMany");
        var withMethod = chain.Methods.First(m =>
            m.Name == "WithRequired" || m.Name == "WithOptional");

        var isRequired = withMethod.Name == "WithRequired";

        var newChain = SyntaxFactory.ParseExpression(
            chain.EntityExpression.ToString());

        // HasMany
        newChain = CreateMethodCall(newChain, "HasMany", hasMany.Arguments);

        // WithOne (replace WithRequired/WithOptional)
        newChain = CreateMethodCall(newChain, "WithOne", withMethod.Arguments);

        // Add HasForeignKey
        var foreignKey = chain.Methods.FirstOrDefault(m => m.Name == "HasForeignKey");
        if (foreignKey != null)
        {
            newChain = CreateMethodCall(newChain, "HasForeignKey", foreignKey.Arguments);
        }

        // Add IsRequired() for required relationships
        if (isRequired)
        {
            newChain = CreateMethodCall(newChain, "IsRequired", new List<ArgumentSyntax>());
        }

        return newChain as InvocationExpressionSyntax;
    }

    private InvocationExpressionSyntax FindMethodCall(SyntaxNode node, string methodName)
    {
        return node.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv =>
            {
                if (inv.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    return memberAccess.Name.Identifier.Text == methodName;
                }
                return false;
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
}

public class MapConfiguration
{
    public string TableName { get; set; }
    public List<string> LeftKeys { get; set; } = new();
    public List<string> RightKeys { get; set; } = new();
}
```

### Unit Tests

```csharp
public class FluentApiHasManyTransformerTests
{
    [Fact]
    public async Task Transform_SimpleManyToMany_PreservesPattern()
    {
        // Arrange
        var code = @"
modelBuilder.Entity<Student>()
    .HasMany(s => s.Courses)
    .WithMany(c => c.Students);";

        // Act
        var result = await TransformFluentApi(code);

        // Assert (EF Core 5.0+ supports same syntax)
        Assert.Contains("HasMany", result);
        Assert.Contains("WithMany", result);
    }

    [Fact]
    public async Task Transform_ManyToManyWithTableName_AddsUsingEntity()
    {
        // Arrange
        var code = @"
modelBuilder.Entity<Student>()
    .HasMany(s => s.Courses)
    .WithMany(c => c.Students)
    .Map(m => m.ToTable(""StudentCourseEnrollment""));";

        var expected = @"
modelBuilder.Entity<Student>()
    .HasMany(s => s.Courses)
    .WithMany(c => c.Students)
    .UsingEntity(j => j.ToTable(""StudentCourseEnrollment""));";

        // Act
        var result = await TransformFluentApi(code);

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_HasManyWithRequired_ConvertsToWithOne()
    {
        // Arrange
        var code = @"
modelBuilder.Entity<Department>()
    .HasMany(d => d.Employees)
    .WithRequired(e => e.Department)
    .HasForeignKey(e => e.DepartmentId);";

        var expected = @"
modelBuilder.Entity<Department>()
    .HasMany(d => d.Employees)
    .WithOne(e => e.Department)
    .HasForeignKey(e => e.DepartmentId)
    .IsRequired();";

        // Act
        var result = await TransformFluentApi(code);

        // Assert
        AssertCodeEquals(expected, result);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
