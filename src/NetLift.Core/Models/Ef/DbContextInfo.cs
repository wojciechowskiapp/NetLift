namespace NetLift.Core.Models.Ef;

/// <summary>
/// Information about an EF6 DbContext class detected in source code.
/// </summary>
public sealed record DbContextInfo
{
    /// <summary>
    /// Gets the class name of the DbContext.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Gets the namespace where the DbContext is declared.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Gets the file path where the DbContext was found (optional).
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the DbContext overrides OnModelCreating method.
    /// </summary>
    public bool HasOnModelCreating { get; init; }

    /// <summary>
    /// Gets the collection of DbSet properties found in the DbContext.
    /// </summary>
    public IReadOnlyList<DbSetInfo> DbSets { get; init; } = [];

    /// <summary>
    /// Gets the collection of constructors found in the DbContext.
    /// </summary>
    public IReadOnlyList<ConstructorInfo> Constructors { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the DbContext uses a connection string name.
    /// </summary>
    public bool UsesConnectionStringName { get; init; }

    /// <summary>
    /// Gets the connection string name extracted from base("name=...") call (if any).
    /// </summary>
    public string? ConnectionStringName { get; init; }
}

/// <summary>
/// Information about a DbSet property in a DbContext.
/// </summary>
/// <param name="PropertyName">The name of the DbSet property.</param>
/// <param name="EntityTypeName">The entity type name (T in DbSet&lt;T&gt;).</param>
public sealed record DbSetInfo(string PropertyName, string EntityTypeName);

/// <summary>
/// Information about a constructor in a DbContext.
/// </summary>
/// <param name="Parameters">The constructor parameters.</param>
/// <param name="HasBaseCall">Indicates whether the constructor has a base() call.</param>
/// <param name="BaseCallArgument">The argument passed to base() if any.</param>
public sealed record ConstructorInfo(
    IReadOnlyList<ParameterInfo> Parameters,
    bool HasBaseCall,
    string? BaseCallArgument);

/// <summary>
/// Information about a constructor parameter.
/// </summary>
/// <param name="Name">The parameter name.</param>
/// <param name="TypeName">The parameter type name.</param>
public sealed record ParameterInfo(string Name, string TypeName);
