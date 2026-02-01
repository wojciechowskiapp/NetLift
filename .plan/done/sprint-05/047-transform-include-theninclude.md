# [TASK-047] Transform Include Chaining to ThenInclude Pattern

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

- **Depends on:** TASK-044, TASK-045, TASK-046
- **Blocks:** -

---

## Description

Transform EF6 nested `.Include()` chaining patterns to EF Core `.ThenInclude()` pattern. EF6 allowed string-based or lambda-based nested includes using Select, while EF Core requires explicit ThenInclude for navigation property chains.

---

## Acceptance Criteria

- [ ] Detect nested Include patterns using Select (e.g., `.Include(o => o.Items.Select(i => i.Product))`)
- [ ] Transform to ThenInclude chain (e.g., `.Include(o => o.Items).ThenInclude(i => i.Product)`)
- [ ] Handle string-based Include paths (e.g., `Include("Items.Product")`) and convert to typed ThenInclude
- [ ] Preserve proper generic type arguments for ThenInclude
- [ ] Handle collection vs reference navigation properties correctly
- [ ] Support multiple levels of nesting (ThenInclude chains)
- [ ] Unit tests covering various nesting depths and property types

---

## Technical Notes

### Transformation Patterns

**Pattern 1: Lambda-based Nested Include with Select**

```csharp
// EF6
var orders = context.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items.Select(i => i.Product))
    .Include(o => o.Items.Select(i => i.Product.Category))
    .ToList();

// EF Core
var orders = context.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items)
        .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Category)
    .ToList();
```

**Pattern 2: String-based Include Path**

```csharp
// EF6
var orders = context.Orders
    .Include("Customer")
    .Include("Items.Product.Category")
    .ToList();

// EF Core
var orders = context.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items)
        .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Category)
    .ToList();
```

**Pattern 3: Multiple Nested Branches**

```csharp
// EF6
var orders = context.Orders
    .Include(o => o.Items.Select(i => i.Product))
    .Include(o => o.Items.Select(i => i.Warehouse))
    .ToList();

// EF Core
var orders = context.Orders
    .Include(o => o.Items)
        .ThenInclude(i => i.Product)
    .Include(o => o.Items)
        .ThenInclude(i => i.Warehouse)
    .ToList();
```

### Roslyn Transformation Implementation

```csharp
public class IncludeToThenIncludeTransformer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;

    public IncludeToThenIncludeTransformer(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Check if this is an Include call
        if (!IsIncludeMethod(node))
        {
            return base.VisitInvocationExpression(node);
        }

        var argument = node.ArgumentList.Arguments.FirstOrDefault();
        if (argument == null)
        {
            return base.VisitInvocationExpression(node);
        }

        // Check for string-based include
        if (argument.Expression is LiteralExpressionSyntax literal)
        {
            return TransformStringInclude(node, literal.Token.ValueText);
        }

        // Check for lambda with nested Select
        if (argument.Expression is SimpleLambdaExpressionSyntax lambda)
        {
            return TransformLambdaInclude(node, lambda);
        }

        return base.VisitInvocationExpression(node);
    }

    private bool IsIncludeMethod(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text == "Include";
        }
        return false;
    }

    private SyntaxNode TransformLambdaInclude(
        InvocationExpressionSyntax includeCall,
        SimpleLambdaExpressionSyntax lambda)
    {
        // Check if body contains .Select() for nested includes
        if (!ContainsSelectCall(lambda.Body))
        {
            return includeCall; // Simple include, no transformation needed
        }

        // Extract the navigation path
        var navigationPath = ExtractNavigationPath(lambda);

        // Build Include().ThenInclude() chain
        return BuildThenIncludeChain(includeCall, navigationPath, lambda.Parameter);
    }

    private bool ContainsSelectCall(SyntaxNode node)
    {
        return node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma
                        && ma.Name.Identifier.Text == "Select");
    }

    private List<NavigationStep> ExtractNavigationPath(SimpleLambdaExpressionSyntax lambda)
    {
        var steps = new List<NavigationStep>();
        ExtractNavigationStepsRecursive(lambda.Body, steps);
        return steps;
    }

    private void ExtractNavigationStepsRecursive(SyntaxNode node, List<NavigationStep> steps)
    {
        // Handle member access (e.g., o.Items)
        if (node is MemberAccessExpressionSyntax memberAccess)
        {
            // Check if this is followed by .Select()
            var parent = memberAccess.Parent;
            if (parent is MemberAccessExpressionSyntax parentMember
                && parentMember.Name.Identifier.Text == "Select")
            {
                // This is a collection navigation
                steps.Add(new NavigationStep
                {
                    PropertyName = memberAccess.Name.Identifier.Text,
                    IsCollection = true
                });

                // Find the Select's lambda and continue
                var selectInvocation = parentMember.Parent as InvocationExpressionSyntax;
                var selectLambda = selectInvocation?.ArgumentList.Arguments
                    .FirstOrDefault()?.Expression as SimpleLambdaExpressionSyntax;

                if (selectLambda != null)
                {
                    ExtractNavigationStepsRecursive(selectLambda.Body, steps);
                }
            }
            else if (!(memberAccess.Expression is IdentifierNameSyntax))
            {
                // Nested member access without Select (reference navigation)
                ExtractNavigationStepsRecursive(memberAccess.Expression, steps);
                steps.Add(new NavigationStep
                {
                    PropertyName = memberAccess.Name.Identifier.Text,
                    IsCollection = false
                });
            }
            else
            {
                // Simple member access (leaf)
                steps.Add(new NavigationStep
                {
                    PropertyName = memberAccess.Name.Identifier.Text,
                    IsCollection = false
                });
            }
        }
    }

    private InvocationExpressionSyntax BuildThenIncludeChain(
        InvocationExpressionSyntax originalInclude,
        List<NavigationStep> navigationPath,
        ParameterSyntax originalParameter)
    {
        if (navigationPath.Count == 0)
        {
            return originalInclude;
        }

        // Start with Include for the first navigation
        var firstStep = navigationPath[0];
        var currentExpr = BuildIncludeCall(
            ((MemberAccessExpressionSyntax)originalInclude.Expression).Expression,
            originalParameter.Identifier.Text,
            firstStep.PropertyName);

        // Add ThenInclude for each subsequent navigation
        for (int i = 1; i < navigationPath.Count; i++)
        {
            var step = navigationPath[i];
            var paramName = GenerateParameterName(i);
            currentExpr = BuildThenIncludeCall(currentExpr, paramName, step.PropertyName);
        }

        return currentExpr;
    }

    private InvocationExpressionSyntax BuildIncludeCall(
        ExpressionSyntax source,
        string paramName,
        string propertyName)
    {
        var lambda = SyntaxFactory.SimpleLambdaExpression(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName)),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(paramName),
                SyntaxFactory.IdentifierName(propertyName)));

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                source,
                SyntaxFactory.IdentifierName("Include")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(lambda))));
    }

    private InvocationExpressionSyntax BuildThenIncludeCall(
        ExpressionSyntax source,
        string paramName,
        string propertyName)
    {
        var lambda = SyntaxFactory.SimpleLambdaExpression(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName)),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(paramName),
                SyntaxFactory.IdentifierName(propertyName)));

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                source,
                SyntaxFactory.IdentifierName("ThenInclude")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(lambda))));
    }

    private string GenerateParameterName(int depth)
    {
        // Generate parameter names: x, y, z, a, b, c...
        var chars = "xyzabcdefghijklmnopqrstuvw";
        return chars[depth % chars.Length].ToString();
    }

    private class NavigationStep
    {
        public string PropertyName { get; set; }
        public bool IsCollection { get; set; }
    }
}
```

### String Include Path Transformation

```csharp
private SyntaxNode TransformStringInclude(
    InvocationExpressionSyntax includeCall,
    string includePath)
{
    var pathParts = includePath.Split('.');

    if (pathParts.Length <= 1)
    {
        // Simple single-level include, convert to lambda
        return ConvertToLambdaInclude(includeCall, pathParts[0]);
    }

    // Multi-level path needs Include().ThenInclude() chain
    var source = ((MemberAccessExpressionSyntax)includeCall.Expression).Expression;

    // Determine the entity type from semantic model
    var entityType = GetEntityTypeFromSource(source);

    // Build the chain
    var currentExpr = BuildIncludeCall(source, "x", pathParts[0]);

    for (int i = 1; i < pathParts.Length; i++)
    {
        var paramName = GenerateParameterName(i);
        currentExpr = BuildThenIncludeCall(currentExpr, paramName, pathParts[i]);
    }

    return currentExpr;
}
```

### Unit Tests

```csharp
public class IncludeToThenIncludeTransformerTests
{
    [Fact]
    public async Task Transform_NestedSelectInclude_TransformsToThenInclude()
    {
        // Arrange
        var code = @"
var orders = context.Orders
    .Include(o => o.Items.Select(i => i.Product))
    .ToList();";

        var expected = @"
var orders = context.Orders
    .Include(o => o.Items)
        .ThenInclude(x => x.Product)
    .ToList();";

        // Act
        var result = await TransformCode(code);

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_MultiLevelNestedInclude_CreatesThenIncludeChain()
    {
        // Arrange
        var code = @"
var orders = context.Orders
    .Include(o => o.Items.Select(i => i.Product.Category))
    .ToList();";

        var expected = @"
var orders = context.Orders
    .Include(o => o.Items)
        .ThenInclude(x => x.Product)
            .ThenInclude(y => y.Category)
    .ToList();";

        // Act
        var result = await TransformCode(code);

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_StringBasedIncludePath_ConvertsToTypedThenInclude()
    {
        // Arrange
        var code = @"
var orders = context.Orders
    .Include(""Items.Product.Category"")
    .ToList();";

        var expected = @"
var orders = context.Orders
    .Include(o => o.Items)
        .ThenInclude(x => x.Product)
            .ThenInclude(y => y.Category)
    .ToList();";

        // Act
        var result = await TransformCode(code);

        // Assert
        AssertCodeEquals(expected, result);
    }

    [Fact]
    public async Task Transform_SimpleInclude_RemainsUnchanged()
    {
        // Arrange
        var code = @"
var orders = context.Orders
    .Include(o => o.Customer)
    .ToList();";

        // Act
        var result = await TransformCode(code);

        // Assert
        Assert.DoesNotContain("ThenInclude", result);
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
