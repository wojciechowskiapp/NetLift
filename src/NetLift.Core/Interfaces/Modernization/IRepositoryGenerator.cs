namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Generates repository pattern interfaces and implementations.
/// </summary>
public interface IRepositoryGenerator
{
    /// <summary>
    /// Generates a repository interface for an entity.
    /// </summary>
    /// <param name="entityName">The name of the entity</param>
    /// <param name="namespaceName">The namespace for the interface</param>
    /// <param name="methods">Custom methods to include in the interface</param>
    /// <returns>Generated C# source code for the repository interface</returns>
    string GenerateInterface(string entityName, string namespaceName, IEnumerable<RepositoryMethod> methods);

    /// <summary>
    /// Generates a repository implementation.
    /// </summary>
    /// <param name="entityName">The name of the entity</param>
    /// <param name="interfaceNamespace">The namespace of the interface</param>
    /// <param name="implNamespace">The namespace for the implementation</param>
    /// <returns>Generated C# source code for the repository implementation</returns>
    string GenerateImplementation(string entityName, string interfaceNamespace, string implNamespace);

    /// <summary>
    /// Generates the generic IRepository base interface.
    /// </summary>
    /// <param name="namespaceName">The namespace for the interface</param>
    /// <returns>Generated C# source code for the base repository interface</returns>
    string GenerateBaseInterface(string namespaceName);

    /// <summary>
    /// Generates the generic Repository base class.
    /// </summary>
    /// <param name="namespaceName">The namespace for the class</param>
    /// <returns>Generated C# source code for the base repository class</returns>
    string GenerateBaseClass(string namespaceName);
}

/// <summary>
/// Represents a custom repository method.
/// </summary>
public sealed class RepositoryMethod
{
    /// <summary>
    /// Gets or sets the method name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the return type.
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the method parameters.
    /// </summary>
    public List<(string Type, string Name)> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the method is async.
    /// </summary>
    public bool IsAsync { get; set; } = true;
}
