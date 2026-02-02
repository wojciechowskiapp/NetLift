using NetLift.Core.Interfaces.Modernization;
using System.Text;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Generates repository pattern interfaces and implementations.
/// </summary>
public sealed class RepositoryGenerator : IRepositoryGenerator
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";

    /// <summary>
    /// Generates a repository interface for an entity.
    /// </summary>
    /// <param name="entityName">The name of the entity</param>
    /// <param name="namespaceName">The namespace for the interface</param>
    /// <param name="methods">Custom methods to include in the interface</param>
    /// <returns>Generated C# source code for the repository interface</returns>
    public string GenerateInterface(string entityName, string namespaceName, IEnumerable<RepositoryMethod> methods)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);
        ArgumentNullException.ThrowIfNull(methods);

        var sb = new StringBuilder();

        // Add namespace
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        // Add XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Repository interface for {entityName} entity.");
        sb.AppendLine("/// </summary>");

        // Generate interface declaration
        sb.AppendLine($"public interface I{entityName}Repository : IRepository<{entityName}>");
        sb.AppendLine("{");

        // Add custom methods
        var methodsList = methods.ToList();
        if (methodsList.Count > 0)
        {
            for (int i = 0; i < methodsList.Count; i++)
            {
                var method = methodsList[i];
                GenerateInterfaceMethod(sb, method);

                // Add blank line between methods (but not after the last one)
                if (i < methodsList.Count - 1)
                {
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a repository implementation.
    /// </summary>
    /// <param name="entityName">The name of the entity</param>
    /// <param name="interfaceNamespace">The namespace of the interface</param>
    /// <param name="implNamespace">The namespace for the implementation</param>
    /// <returns>Generated C# source code for the repository implementation</returns>
    public string GenerateImplementation(string entityName, string interfaceNamespace, string implNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(implNamespace);

        var sb = new StringBuilder();

        // Add namespace
        sb.AppendLine($"namespace {implNamespace};");
        sb.AppendLine();

        // Add usings
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine($"using {interfaceNamespace};");
        sb.AppendLine();

        // Add XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Repository implementation for {entityName} entity.");
        sb.AppendLine("/// </summary>");

        // Generate class declaration
        sb.AppendLine($"public sealed class {entityName}Repository : Repository<{entityName}>, I{entityName}Repository");
        sb.AppendLine("{");

        // Add constructor
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"{entityName}Repository\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"context\">The database context</param>");
        sb.AppendLine($"{Indent}public {entityName}Repository(ApplicationDbContext context) : base(context) {{ }}");

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the generic IRepository base interface.
    /// </summary>
    /// <param name="namespaceName">The namespace for the interface</param>
    /// <returns>Generated C# source code for the base repository interface</returns>
    public string GenerateBaseInterface(string namespaceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);

        var sb = new StringBuilder();

        // Add namespace
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        // Add XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Base repository interface for generic entity operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"T\">The entity type</typeparam>");

        // Generate interface
        sb.AppendLine("public interface IRepository<T> where T : class");
        sb.AppendLine("{");

        // GetByIdAsync
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets an entity by its identifier.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"id\">The entity identifier</param>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">The cancellation token</param>");
        sb.AppendLine($"{Indent}/// <returns>The entity if found; otherwise, null</returns>");
        sb.AppendLine($"{Indent}Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);");
        sb.AppendLine();

        // GetAllAsync
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets all entities.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">The cancellation token</param>");
        sb.AppendLine($"{Indent}/// <returns>A read-only collection of all entities</returns>");
        sb.AppendLine($"{Indent}Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);");
        sb.AppendLine();

        // AddAsync
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Adds a new entity.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"entity\">The entity to add</param>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">The cancellation token</param>");
        sb.AppendLine($"{Indent}/// <returns>The added entity</returns>");
        sb.AppendLine($"{Indent}Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);");
        sb.AppendLine();

        // UpdateAsync
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Updates an existing entity.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"entity\">The entity to update</param>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">The cancellation token</param>");
        sb.AppendLine($"{Indent}Task UpdateAsync(T entity, CancellationToken cancellationToken = default);");
        sb.AppendLine();

        // DeleteAsync
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Deletes an entity.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"entity\">The entity to delete</param>");
        sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">The cancellation token</param>");
        sb.AppendLine($"{Indent}Task DeleteAsync(T entity, CancellationToken cancellationToken = default);");
        sb.AppendLine();

        // AsQueryable
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Gets a queryable collection of entities.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <returns>A queryable collection</returns>");
        sb.AppendLine($"{Indent}IQueryable<T> AsQueryable();");

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the generic Repository base class.
    /// </summary>
    /// <param name="namespaceName">The namespace for the class</param>
    /// <returns>Generated C# source code for the base repository class</returns>
    public string GenerateBaseClass(string namespaceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);

        var sb = new StringBuilder();

        // Add namespace
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        // Add usings
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();

        // Add XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Base repository implementation for generic entity operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"T\">The entity type</typeparam>");

        // Generate class
        sb.AppendLine("public class Repository<T> : IRepository<T> where T : class");
        sb.AppendLine("{");

        // Add protected fields
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// The database context.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}protected readonly ApplicationDbContext Context;");
        sb.AppendLine();
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// The database set for the entity type.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}protected readonly DbSet<T> DbSet;");
        sb.AppendLine();

        // Add constructor
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Initializes a new instance of the <see cref=\"Repository{{T}}\"/> class.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}/// <param name=\"context\">The database context</param>");
        sb.AppendLine($"{Indent}public Repository(ApplicationDbContext context)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}Context = context;");
        sb.AppendLine($"{DoubleIndent}DbSet = context.Set<T>();");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();

        // GetByIdAsync
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}return await DbSet.FindAsync(new object[] {{ id }}, cancellationToken);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();

        // GetAllAsync
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}return await DbSet.ToListAsync(cancellationToken);");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();

        // AddAsync
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}await DbSet.AddAsync(entity, cancellationToken);");
        sb.AppendLine($"{DoubleIndent}return entity;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();

        // UpdateAsync
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}Context.Entry(entity).State = EntityState.Modified;");
        sb.AppendLine($"{DoubleIndent}return Task.CompletedTask;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();

        // DeleteAsync
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}DbSet.Remove(entity);");
        sb.AppendLine($"{DoubleIndent}return Task.CompletedTask;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();

        // AsQueryable
        sb.AppendLine($"{Indent}/// <inheritdoc />");
        sb.AppendLine($"{Indent}public virtual IQueryable<T> AsQueryable() => DbSet.AsQueryable();");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateInterfaceMethod(StringBuilder sb, RepositoryMethod method)
    {
        // Add XML documentation
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// {method.Name}");
        sb.AppendLine($"{Indent}/// </summary>");

        foreach (var param in method.Parameters)
        {
            sb.AppendLine($"{Indent}/// <param name=\"{param.Name}\">The {param.Name}</param>");
        }

        if (method.IsAsync)
        {
            sb.AppendLine($"{Indent}/// <param name=\"cancellationToken\">The cancellation token</param>");
        }

        sb.AppendLine($"{Indent}/// <returns>{method.ReturnType}</returns>");

        // Generate method signature
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));

        if (method.IsAsync)
        {
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                parameters += ", ";
            }
            parameters += "CancellationToken cancellationToken = default";
        }

        sb.AppendLine($"{Indent}{method.ReturnType} {method.Name}({parameters});");
    }
}
