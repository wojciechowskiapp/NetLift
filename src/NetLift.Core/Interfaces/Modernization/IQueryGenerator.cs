using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Generates CQRS Query classes from controller actions.
/// </summary>
public interface IQueryGenerator
{
    /// <summary>
    /// Generates a Query class for an action.
    /// </summary>
    /// <param name="queryInfo">Information about the query to generate</param>
    /// <returns>Generated C# source code for the query class</returns>
    string Generate(QueryInfo queryInfo);
    
    /// <summary>
    /// Generates a QueryHandler class.
    /// </summary>
    /// <param name="queryInfo">Information about the query handler to generate</param>
    /// <returns>Generated C# source code for the query handler class</returns>
    string GenerateHandler(QueryInfo queryInfo);
}
