# [TASK-048] Transform SqlQuery to FromSqlRaw/FromSqlInterpolated

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 5 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-044
- **Blocks:** -

---

## Description

Transform EF6 `Database.SqlQuery<T>()` calls to EF Core `FromSqlRaw()` or `FromSqlInterpolated()` patterns. EF Core removed the Database.SqlQuery method and requires using DbSet<T>.FromSqlRaw or FromSqlInterpolated for raw SQL queries that return entities.

---

## Acceptance Criteria

- [ ] Detect `Database.SqlQuery<T>()` usage patterns
- [ ] Transform to `DbSet<T>.FromSqlRaw()` for string concatenation queries
- [ ] Transform to `DbSet<T>.FromSqlInterpolated()` for interpolated string queries
- [ ] Handle `SqlParameter` objects and convert to parameterized queries
- [ ] Detect non-entity types and add TODO comments for keyless entity configuration
- [ ] Generate warning for queries returning anonymous types (not supported in EF Core)
- [ ] Unit tests covering various SqlQuery patterns

---

## Technical Notes

### Transformation Patterns

**Pattern 1: Simple SqlQuery with String**

```csharp
// EF6
var products = context.Database
    .SqlQuery<Product>("SELECT * FROM Products WHERE CategoryId = @p0", categoryId)
    .ToList();

// EF Core
var products = context.Products
    .FromSqlRaw("SELECT * FROM Products WHERE CategoryId = {0}", categoryId)
    .ToList();
```

**Pattern 2: SqlQuery with Interpolated String**

```csharp
// EF6
var products = context.Database
    .SqlQuery<Product>($"SELECT * FROM Products WHERE Price > {minPrice}")
    .ToList();

// EF Core
var products = context.Products
    .FromSqlInterpolated($"SELECT * FROM Products WHERE Price > {minPrice}")
    .ToList();
```

**Pattern 3: SqlQuery with SqlParameter**

```csharp
// EF6
var param = new SqlParameter("@name", searchName);
var customers = context.Database
    .SqlQuery<Customer>("SELECT * FROM Customers WHERE Name LIKE @name", param)
    .ToList();

// EF Core
var customers = context.Customers
    .FromSqlRaw("SELECT * FROM Customers WHERE Name LIKE @name",
        new SqlParameter("@name", searchName))
    .ToList();
```

**Pattern 4: Non-Entity Query (Keyless)**

```csharp
// EF6
public class ProductSummary
{
    public int CategoryId { get; set; }
    public int ProductCount { get; set; }
    public decimal TotalValue { get; set; }
}

var summaries = context.Database
    .SqlQuery<ProductSummary>("SELECT CategoryId, COUNT(*) AS ProductCount, SUM(Price) AS TotalValue FROM Products GROUP BY CategoryId")
    .ToList();

// EF Core (requires keyless entity configuration)
// In DbContext.OnModelCreating:
// modelBuilder.Entity<ProductSummary>().HasNoKey().ToView(null);

var summaries = context.Set<ProductSummary>()
    .FromSqlRaw("SELECT CategoryId, COUNT(*) AS ProductCount, SUM(Price) AS TotalValue FROM Products GROUP BY CategoryId")
    .ToList();
```

**Pattern 5: ExecuteSqlCommand Transformation**

```csharp
// EF6
context.Database.ExecuteSqlCommand("UPDATE Products SET Price = Price * 1.1 WHERE CategoryId = @p0", categoryId);

// EF Core
context.Database.ExecuteSqlRaw("UPDATE Products SET Price = Price * 1.1 WHERE CategoryId = {0}", categoryId);
// Or with interpolation:
context.Database.ExecuteSqlInterpolated($"UPDATE Products SET Price = Price * 1.1 WHERE CategoryId = {categoryId}");
```

### Roslyn Transformation Implementation

```csharp
public class SqlQueryTransformer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly HashSet<string> _dbSetEntityTypes;
    private readonly List<string> _keylessTypesToConfigure;

    public SqlQueryTransformer(SemanticModel semanticModel, HashSet<string> dbSetEntityTypes)
    {
        _semanticModel = semanticModel;
        _dbSetEntityTypes = dbSetEntityTypes;
        _keylessTypesToConfigure = new List<string>();
    }

    public IReadOnlyList<string> KeylessTypesToConfigure => _keylessTypesToConfigure;

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (IsSqlQueryCall(node, out var typeArgument))
        {
            return TransformSqlQuery(node, typeArgument);
        }

        if (IsExecuteSqlCommandCall(node))
        {
            return TransformExecuteSqlCommand(node);
        }

        return base.VisitInvocationExpression(node);
    }

    private bool IsSqlQueryCall(InvocationExpressionSyntax node, out string typeArgument)
    {
        typeArgument = null;

        if (node.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Name is GenericNameSyntax genericName
                && genericName.Identifier.Text == "SqlQuery")
            {
                var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                typeArgument = typeArg?.ToString();

                // Verify it's called on Database property
                if (memberAccess.Expression is MemberAccessExpressionSyntax parentAccess
                    && parentAccess.Name.Identifier.Text == "Database")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsExecuteSqlCommandCall(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text == "ExecuteSqlCommand"
                   && memberAccess.Expression is MemberAccessExpressionSyntax parentAccess
                   && parentAccess.Name.Identifier.Text == "Database";
        }
        return false;
    }

    private SyntaxNode TransformSqlQuery(InvocationExpressionSyntax node, string typeArgument)
    {
        var arguments = node.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return node;
        }

        var sqlArgument = arguments[0];
        var isInterpolated = sqlArgument.Expression is InterpolatedStringExpressionSyntax;
        var methodName = isInterpolated ? "FromSqlInterpolated" : "FromSqlRaw";

        // Determine the source - DbSet<T> or Set<T>()
        var contextExpression = GetContextExpression(node);
        ExpressionSyntax sourceExpression;

        if (_dbSetEntityTypes.Contains(typeArgument))
        {
            // Entity type with DbSet - use context.EntityName
            var dbSetName = GetDbSetName(typeArgument);
            sourceExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                contextExpression,
                SyntaxFactory.IdentifierName(dbSetName));
        }
        else
        {
            // Non-entity type - use context.Set<T>() and track for keyless config
            _keylessTypesToConfigure.Add(typeArgument);

            sourceExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    contextExpression,
                    SyntaxFactory.GenericName("Set")
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(typeArgument))))));
        }

        // Transform arguments for FromSqlRaw (convert @p0 to {0})
        var transformedArguments = TransformSqlArguments(arguments, isInterpolated);

        // Build the new invocation
        var newInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                sourceExpression,
                SyntaxFactory.IdentifierName(methodName)),
            SyntaxFactory.ArgumentList(transformedArguments));

        // Add comment for keyless types
        if (!_dbSetEntityTypes.Contains(typeArgument))
        {
            newInvocation = newInvocation.WithLeadingTrivia(
                SyntaxFactory.Comment($"// TODO: Configure {typeArgument} as keyless entity in OnModelCreating"),
                SyntaxFactory.EndOfLine("\r\n"));
        }

        return newInvocation;
    }

    private SeparatedSyntaxList<ArgumentSyntax> TransformSqlArguments(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        bool isInterpolated)
    {
        if (isInterpolated)
        {
            // For interpolated strings, just pass through
            return arguments;
        }

        var newArguments = new List<ArgumentSyntax>();
        var sqlArg = arguments[0];

        // Transform SQL string to replace @p0, @p1 with {0}, {1}
        if (sqlArg.Expression is LiteralExpressionSyntax literal)
        {
            var sql = literal.Token.ValueText;
            var transformedSql = TransformParameterPlaceholders(sql);

            newArguments.Add(SyntaxFactory.Argument(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(transformedSql))));
        }
        else
        {
            newArguments.Add(sqlArg);
        }

        // Add remaining arguments (parameters)
        for (int i = 1; i < arguments.Count; i++)
        {
            newArguments.Add(arguments[i]);
        }

        return SyntaxFactory.SeparatedList(newArguments);
    }

    private string TransformParameterPlaceholders(string sql)
    {
        // Replace @p0, @p1, etc. with {0}, {1}, etc.
        var regex = new System.Text.RegularExpressions.Regex(@"@p(\d+)");
        return regex.Replace(sql, "{$1}");
    }

    private SyntaxNode TransformExecuteSqlCommand(InvocationExpressionSyntax node)
    {
        var arguments = node.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return node;
        }

        var sqlArgument = arguments[0];
        var isInterpolated = sqlArgument.Expression is InterpolatedStringExpressionSyntax;
        var methodName = isInterpolated ? "ExecuteSqlInterpolated" : "ExecuteSqlRaw";

        // Get context.Database expression
        var memberAccess = (MemberAccessExpressionSyntax)node.Expression;
        var databaseAccess = memberAccess.Expression;

        // Transform arguments
        var transformedArguments = TransformSqlArguments(arguments, isInterpolated);

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                databaseAccess,
                SyntaxFactory.IdentifierName(methodName)),
            SyntaxFactory.ArgumentList(transformedArguments));
    }

    private ExpressionSyntax GetContextExpression(InvocationExpressionSyntax node)
    {
        // Navigate from context.Database.SqlQuery to context
        var memberAccess = (MemberAccessExpressionSyntax)node.Expression;
        var databaseAccess = (MemberAccessExpressionSyntax)memberAccess.Expression;
        return databaseAccess.Expression;
    }

    private string GetDbSetName(string entityTypeName)
    {
        // Convention: pluralize entity name
        // This should ideally check the actual DbContext for the DbSet property name
        if (entityTypeName.EndsWith("y"))
        {
            return entityTypeName.Substring(0, entityTypeName.Length - 1) + "ies";
        }
        return entityTypeName + "s";
    }
}
```

### Keyless Entity Configuration Generator

```csharp
public class KeylessEntityConfigurationGenerator
{
    public string GenerateOnModelCreatingCode(IEnumerable<string> keylessTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("{");
        sb.AppendLine("    base.OnModelCreating(modelBuilder);");
        sb.AppendLine();
        sb.AppendLine("    // Keyless entity configurations for raw SQL queries");

        foreach (var typeName in keylessTypes)
        {
            sb.AppendLine($"    modelBuilder.Entity<{typeName}>().HasNoKey().ToView(null);");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }
}
```

### Unit Tests

```csharp
public class SqlQueryTransformerTests
{
    [Fact]
    public async Task Transform_SimpleSqlQuery_TransformsToFromSqlRaw()
    {
        // Arrange
        var code = @"
var products = context.Database
    .SqlQuery<Product>(""SELECT * FROM Products WHERE CategoryId = @p0"", categoryId)
    .ToList();";

        var expected = @"
var products = context.Products
    .FromSqlRaw(""SELECT * FROM Products WHERE CategoryId = {0}"", categoryId)
    .ToList();";

        // Act
        var result = await TransformCode(code);

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_InterpolatedSqlQuery_TransformsToFromSqlInterpolated()
    {
        // Arrange
        var code = @"
var products = context.Database
    .SqlQuery<Product>($""SELECT * FROM Products WHERE Price > {minPrice}"")
    .ToList();";

        var expected = @"
var products = context.Products
    .FromSqlInterpolated($""SELECT * FROM Products WHERE Price > {minPrice}"")
    .ToList();";

        // Act
        var result = await TransformCode(code);

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_NonEntitySqlQuery_UsesSetAndAddsKeylessComment()
    {
        // Arrange
        var code = @"
var summaries = context.Database
    .SqlQuery<ProductSummary>(""SELECT CategoryId, COUNT(*) AS ProductCount FROM Products GROUP BY CategoryId"")
    .ToList();";

        // Act
        var result = await TransformCode(code);

        // Assert
        Assert.Contains("context.Set<ProductSummary>()", result);
        Assert.Contains("FromSqlRaw", result);
        Assert.Contains("TODO: Configure ProductSummary as keyless entity", result);
    }

    [Fact]
    public async Task Transform_ExecuteSqlCommand_TransformsToExecuteSqlRaw()
    {
        // Arrange
        var code = @"
context.Database.ExecuteSqlCommand(""UPDATE Products SET Price = Price * 1.1 WHERE CategoryId = @p0"", categoryId);";

        var expected = @"
context.Database.ExecuteSqlRaw(""UPDATE Products SET Price = Price * 1.1 WHERE CategoryId = {0}"", categoryId);";

        // Act
        var result = await TransformCode(code);

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_SqlQueryWithSqlParameter_PreservesParameters()
    {
        // Arrange
        var code = @"
var param = new SqlParameter(""@name"", searchName);
var customers = context.Database
    .SqlQuery<Customer>(""SELECT * FROM Customers WHERE Name LIKE @name"", param)
    .ToList();";

        var expected = @"
var param = new SqlParameter(""@name"", searchName);
var customers = context.Customers
    .FromSqlRaw(""SELECT * FROM Customers WHERE Name LIKE @name"", param)
    .ToList();";

        // Act
        var result = await TransformCode(code);

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
