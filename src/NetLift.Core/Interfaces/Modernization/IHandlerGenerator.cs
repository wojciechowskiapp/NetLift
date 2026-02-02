namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Generates supplementary handler components for MediatR-based architecture.
/// Produces Result wrapper classes, DTOs, and IApplicationDbContext interface.
/// </summary>
public interface IHandlerGenerator
{
    /// <summary>
    /// Generates the Result&lt;T&gt; wrapper class for operation results.
    /// </summary>
    /// <param name="namespaceName">The namespace for the Result class (e.g., "ContosoUniversity.Application.Common").</param>
    /// <returns>The generated Result&lt;T&gt; class source code.</returns>
    string GenerateResultClass(string namespaceName);

    /// <summary>
    /// Generates a DTO class from an entity with specified properties.
    /// </summary>
    /// <param name="entityName">The name of the entity (e.g., "Student").</param>
    /// <param name="properties">Collection of property name and type tuples.</param>
    /// <param name="namespaceName">The namespace for the DTO (e.g., "ContosoUniversity.Application.Students.Queries").</param>
    /// <returns>The generated DTO record source code.</returns>
    string GenerateDto(
        string entityName,
        IEnumerable<(string Name, string Type)> properties,
        string namespaceName);

    /// <summary>
    /// Generates the IApplicationDbContext interface with DbSet properties for all entities.
    /// </summary>
    /// <param name="entityNames">Collection of entity names to include as DbSet properties.</param>
    /// <param name="namespaceName">The namespace for the interface (e.g., "ContosoUniversity.Application.Common.Interfaces").</param>
    /// <returns>The generated IApplicationDbContext interface source code.</returns>
    string GenerateDbContextInterface(IEnumerable<string> entityNames, string namespaceName);

    /// <summary>
    /// Generates lightweight MediatR replacement interfaces (IRequest, IRequestHandler, Unit).
    /// </summary>
    /// <param name="namespaceName">The namespace for the interfaces (e.g., "ContosoUniversity.Application.Common").</param>
    /// <returns>The generated interfaces and Unit struct source code.</returns>
    string GenerateMediatorInterfaces(string namespaceName);

    /// <summary>
    /// Generates the IMediator interface for sending requests.
    /// </summary>
    /// <param name="namespaceName">The namespace for the interface (e.g., "ContosoUniversity.Application.Common.Interfaces").</param>
    /// <returns>The generated IMediator interface source code.</returns>
    string GenerateMediatorInterface(string namespaceName);

    /// <summary>
    /// Generates the Mediator implementation class.
    /// </summary>
    /// <param name="namespaceName">The namespace for the implementation (e.g., "ContosoUniversity.Infrastructure").</param>
    /// <param name="rootNamespace">The root namespace for referencing Application.Common types.</param>
    /// <returns>The generated Mediator class source code.</returns>
    string GenerateMediatorImplementation(string namespaceName, string rootNamespace);
}
