using NetLift.Core.Models.Modernization;

namespace NetLift.Core.Interfaces.Modernization;

/// <summary>
/// Generates FluentValidation validators for Commands and Queries.
/// </summary>
public interface IValidatorGenerator
{
    /// <summary>
    /// Generates a FluentValidation validator class.
    /// </summary>
    /// <param name="validatorInfo">Information about the validator to generate</param>
    /// <returns>Generated C# source code for the validator class</returns>
    string Generate(ValidatorInfo validatorInfo);
    
    /// <summary>
    /// Generates a validator for a command.
    /// </summary>
    /// <param name="commandInfo">Information about the command</param>
    /// <returns>Generated C# source code for the validator class</returns>
    string GenerateForCommand(CommandInfo commandInfo);
    
    /// <summary>
    /// Generates a validator for a query.
    /// </summary>
    /// <param name="queryInfo">Information about the query</param>
    /// <returns>Generated C# source code for the validator class</returns>
    string GenerateForQuery(QueryInfo queryInfo);
}
