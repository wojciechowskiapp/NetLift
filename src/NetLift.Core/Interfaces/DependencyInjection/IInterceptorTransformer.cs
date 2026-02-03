using NetLift.Core.Models.DependencyInjection;

namespace NetLift.Core.Interfaces.DependencyInjection;

/// <summary>
/// Handles interceptor and decorator pattern migration.
/// </summary>
public interface IInterceptorTransformer
{
    /// <summary>
    /// Analyzes an interceptor from a service registration.
    /// </summary>
    /// <param name="registration">The registration with interceptor.</param>
    /// <returns>Interceptor information.</returns>
    Task<InterceptorInfo> AnalyzeInterceptorAsync(ServiceRegistrationInfo registration);

    /// <summary>
    /// Generates migration guidance for an interceptor.
    /// </summary>
    /// <param name="interceptor">The interceptor to generate guidance for.</param>
    /// <returns>A transformation note with guidance.</returns>
    TransformationNote GenerateMigrationGuidance(InterceptorInfo interceptor);

    /// <summary>
    /// Determines if Scrutor can be used for the interceptor pattern.
    /// </summary>
    /// <param name="interceptor">The interceptor to check.</param>
    /// <returns>True if Scrutor's Decorate pattern can be used.</returns>
    bool CanUseScrutor(InterceptorInfo interceptor);

    /// <summary>
    /// Generates Scrutor decorator code for an interceptor.
    /// </summary>
    /// <param name="interceptor">The interceptor to convert.</param>
    /// <returns>The Scrutor decorator code.</returns>
    string GenerateScrutorDecorator(InterceptorInfo interceptor);

    /// <summary>
    /// Generates a TODO comment for manual interceptor migration.
    /// </summary>
    /// <param name="interceptor">The interceptor.</param>
    /// <returns>The TODO comment with options.</returns>
    string GenerateTodoComment(InterceptorInfo interceptor);
}
