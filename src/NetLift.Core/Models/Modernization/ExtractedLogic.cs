namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents extracted logic from a method body for transformation.
/// </summary>
public sealed record ExtractedLogic
{
    /// <summary>
    /// Gets the variable declarations in the method.
    /// </summary>
    public IReadOnlyList<VariableInfo> Variables { get; init; } = [];

    /// <summary>
    /// Gets the statements in the method body.
    /// </summary>
    public IReadOnlyList<StatementInfo> Statements { get; init; } = [];

    /// <summary>
    /// Gets the service/repository calls made in the method.
    /// </summary>
    public IReadOnlyList<MethodCallInfo> ServiceCalls { get; init; } = [];

    /// <summary>
    /// Gets the DbContext operations (LINQ queries, SaveChanges, etc.).
    /// </summary>
    public IReadOnlyList<DbContextOperation> DbOperations { get; init; } = [];

    /// <summary>
    /// Gets the return statement information.
    /// </summary>
    public ReturnInfo? ReturnStatement { get; init; }

    /// <summary>
    /// Gets the dependencies used in this logic.
    /// </summary>
    public IReadOnlyList<string> UsedDependencies { get; init; } = [];

    /// <summary>
    /// Gets whether the logic contains ModelState validation.
    /// </summary>
    public bool HasModelStateValidation { get; init; }

    /// <summary>
    /// Gets whether the logic contains ViewBag/ViewData usage.
    /// </summary>
    public bool HasViewBagUsage { get; init; }

    /// <summary>
    /// Gets the confidence score for extraction (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets warnings or notes about the extraction.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets explicit loading operations (db.Entry().Collection/Reference().Load()).
    /// </summary>
    public IReadOnlyList<ExplicitLoadOperation> ExplicitLoads { get; init; } = [];

    /// <summary>
    /// Gets view model property mutations (viewModel.Property = value).
    /// </summary>
    public IReadOnlyList<ViewModelMutation> ViewModelMutations { get; init; } = [];

    /// <summary>
    /// Gets conditional blocks with parameter checks (if (id != null) { ... }).
    /// </summary>
    public IReadOnlyList<ConditionalBlock> ConditionalBlocks { get; init; } = [];
}

/// <summary>
/// Represents a variable declaration in extracted logic.
/// </summary>
public sealed record VariableInfo
{
    /// <summary>
    /// Gets the variable name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the declared type (or "var").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the initialization expression.
    /// </summary>
    public string? Initializer { get; init; }

    /// <summary>
    /// Gets the line number in the original source.
    /// </summary>
    public int LineNumber { get; init; }
}

/// <summary>
/// Represents a statement in extracted logic.
/// </summary>
public sealed record StatementInfo
{
    /// <summary>
    /// Gets the type of statement (Declaration, Assignment, MethodCall, If, Return, etc.).
    /// </summary>
    public required StatementType Type { get; init; }

    /// <summary>
    /// Gets the original source code of the statement.
    /// </summary>
    public required string SourceCode { get; init; }

    /// <summary>
    /// Gets the transformed code for the handler (async, etc.).
    /// </summary>
    public string? TransformedCode { get; init; }

    /// <summary>
    /// Gets the line number in the original source.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Gets whether this statement needs async transformation.
    /// </summary>
    public bool NeedsAsyncTransform { get; init; }

    /// <summary>
    /// Gets child statements (for blocks like if/else).
    /// </summary>
    public IReadOnlyList<StatementInfo> ChildStatements { get; init; } = [];
}

/// <summary>
/// Type of statement in extracted logic.
/// </summary>
public enum StatementType
{
    Declaration,
    Assignment,
    MethodCall,
    If,
    Else,
    Return,
    ForEach,
    For,
    While,
    Try,
    Throw,
    Switch,
    Other
}

/// <summary>
/// Represents a method call to a service or repository.
/// </summary>
public sealed record MethodCallInfo
{
    /// <summary>
    /// Gets the target object (e.g., "service", "_dbContext").
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Gets the method name being called.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// Gets the arguments passed to the method.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// Gets the original source code.
    /// </summary>
    public required string SourceCode { get; init; }

    /// <summary>
    /// Gets whether this is a DbContext method call.
    /// </summary>
    public bool IsDbContextCall { get; init; }

    /// <summary>
    /// Gets whether this call should be made async.
    /// </summary>
    public bool ShouldBeAsync { get; init; }

    /// <summary>
    /// Gets the async equivalent method name (e.g., ToList → ToListAsync).
    /// </summary>
    public string? AsyncEquivalent { get; init; }
}

/// <summary>
/// Represents a DbContext/EF operation.
/// </summary>
public sealed record DbContextOperation
{
    /// <summary>
    /// Gets the type of operation (Query, Add, Update, Remove, SaveChanges).
    /// </summary>
    public required DbOperationType OperationType { get; init; }

    /// <summary>
    /// Gets the entity type being operated on.
    /// </summary>
    public string? EntityType { get; init; }

    /// <summary>
    /// Gets the DbSet property name.
    /// </summary>
    public string? DbSetName { get; init; }

    /// <summary>
    /// Gets the original source code.
    /// </summary>
    public required string SourceCode { get; init; }

    /// <summary>
    /// Gets the LINQ operations applied (Include, Where, OrderBy, etc.).
    /// </summary>
    public IReadOnlyList<string> LinqOperations { get; init; } = [];
}

/// <summary>
/// Type of DbContext operation.
/// </summary>
public enum DbOperationType
{
    Query,
    Find,
    Add,
    Update,
    Remove,
    SaveChanges,
    Entry,
    Other
}

/// <summary>
/// Represents return statement information.
/// </summary>
public sealed record ReturnInfo
{
    /// <summary>
    /// Gets the return expression.
    /// </summary>
    public required string Expression { get; init; }

    /// <summary>
    /// Gets whether this returns a View.
    /// </summary>
    public bool IsViewReturn { get; init; }

    /// <summary>
    /// Gets whether this returns a RedirectToAction.
    /// </summary>
    public bool IsRedirect { get; init; }

    /// <summary>
    /// Gets whether this returns NotFound/BadRequest.
    /// </summary>
    public bool IsErrorReturn { get; init; }

    /// <summary>
    /// Gets the model/data being returned.
    /// </summary>
    public string? ReturnedModel { get; init; }

    /// <summary>
    /// Gets the transformed return for handler.
    /// </summary>
    public string? TransformedReturn { get; init; }
}

/// <summary>
/// Represents an explicit loading operation (db.Entry().Collection/Reference().Load()).
/// </summary>
public sealed record ExplicitLoadOperation
{
    /// <summary>
    /// Gets the entity variable being loaded (e.g., "selectedCourse").
    /// </summary>
    public required string EntityVariable { get; init; }

    /// <summary>
    /// Gets the navigation property being loaded (e.g., "Enrollments").
    /// </summary>
    public required string NavigationProperty { get; init; }

    /// <summary>
    /// Gets whether this is a collection navigation (true) or reference navigation (false).
    /// </summary>
    public bool IsCollection { get; init; }

    /// <summary>
    /// Gets the original source code.
    /// </summary>
    public required string SourceCode { get; init; }

    /// <summary>
    /// Gets the line number in the original source.
    /// </summary>
    public int LineNumber { get; init; }
}

/// <summary>
/// Represents a view model property mutation (viewModel.Property = value).
/// </summary>
public sealed record ViewModelMutation
{
    /// <summary>
    /// Gets the view model variable name (e.g., "viewModel").
    /// </summary>
    public required string ViewModelVariable { get; init; }

    /// <summary>
    /// Gets the property being assigned (e.g., "Instructors").
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets the value being assigned.
    /// </summary>
    public required string AssignedValue { get; init; }

    /// <summary>
    /// Gets the line number in the original source.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Gets whether this mutation is inside a conditional block.
    /// </summary>
    public bool IsConditional { get; init; }

    /// <summary>
    /// Gets the condition expression if inside a conditional block.
    /// </summary>
    public string? ConditionExpression { get; init; }
}

/// <summary>
/// Represents a conditional block with parameter checks (if (id != null) { ... }).
/// </summary>
public sealed record ConditionalBlock
{
    /// <summary>
    /// Gets the condition expression (e.g., "id != null").
    /// </summary>
    public required string Condition { get; init; }

    /// <summary>
    /// Gets the parameter name being checked (e.g., "id" from "id != null").
    /// </summary>
    public string? ParameterName { get; init; }

    /// <summary>
    /// Gets the statements inside the conditional block.
    /// </summary>
    public IReadOnlyList<StatementInfo> Statements { get; init; } = [];

    /// <summary>
    /// Gets the view model mutations inside this block.
    /// </summary>
    public IReadOnlyList<ViewModelMutation> Mutations { get; init; } = [];

    /// <summary>
    /// Gets the DbContext operations inside this block.
    /// </summary>
    public IReadOnlyList<DbContextOperation> DbOperations { get; init; } = [];

    /// <summary>
    /// Gets the explicit loads inside this block.
    /// </summary>
    public IReadOnlyList<ExplicitLoadOperation> ExplicitLoads { get; init; } = [];

    /// <summary>
    /// Gets the line number in the original source.
    /// </summary>
    public int LineNumber { get; init; }
}
