using NetLift.Core.Models.DependencyInjection;

namespace NetLift.Core.Interfaces.DependencyInjection;

/// <summary>
/// Transforms DI registrations to Microsoft.Extensions.DependencyInjection.
/// </summary>
public interface IDIContainerTransformer
{
    /// <summary>
    /// Transforms DI container configuration to MEDI.
    /// </summary>
    /// <param name="containerInfo">The container information to transform.</param>
    /// <param name="options">Transformation options.</param>
    /// <returns>The transformation result.</returns>
    Task<DITransformResult> TransformAsync(DIContainerInfo containerInfo, DITransformOptions options);

    /// <summary>
    /// Generates IServiceCollection extension code.
    /// </summary>
    /// <param name="registrations">The registrations to generate code for.</param>
    /// <returns>The generated C# code.</returns>
    string GenerateServiceCollectionCode(IReadOnlyList<ServiceRegistrationInfo> registrations);

    /// <summary>
    /// Generates an extension method from a module.
    /// </summary>
    /// <param name="module">The module to convert.</param>
    /// <returns>The generated extension method code.</returns>
    string GenerateExtensionMethod(ModuleInfo module);

    /// <summary>
    /// Generates Program.cs integration code.
    /// </summary>
    /// <param name="containerInfo">The container information.</param>
    /// <returns>The generated integration code.</returns>
    string GenerateProgramCsIntegration(DIContainerInfo containerInfo);

    /// <summary>
    /// Transforms a single service registration to MEDI syntax.
    /// </summary>
    /// <param name="registration">The registration to transform.</param>
    /// <param name="options">Transformation options.</param>
    /// <returns>The transformed code line.</returns>
    string TransformRegistration(ServiceRegistrationInfo registration, DITransformOptions options);
}
