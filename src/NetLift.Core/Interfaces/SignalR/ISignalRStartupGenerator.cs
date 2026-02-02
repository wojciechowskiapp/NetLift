using NetLift.Core.Models.SignalR;

namespace NetLift.Core.Interfaces.SignalR;

/// <summary>
/// Generates ASP.NET Core SignalR startup configuration code.
/// </summary>
public interface ISignalRStartupGenerator
{
    /// <summary>
    /// Generates AddSignalR() service registration code.
    /// </summary>
    /// <returns>The service registration code snippet.</returns>
    string GenerateServiceRegistration();

    /// <summary>
    /// Generates MapHub endpoint mappings for discovered hubs.
    /// </summary>
    /// <param name="hubs">The discovered SignalR hubs.</param>
    /// <returns>The endpoint mapping code snippet.</returns>
    string GenerateEndpointMappings(IReadOnlyList<SignalRHubInfo> hubs);

    /// <summary>
    /// Generates the complete SignalR configuration for Program.cs.
    /// </summary>
    /// <param name="hubs">The discovered SignalR hubs.</param>
    /// <returns>Complete configuration code with comments.</returns>
    string GenerateFullConfiguration(IReadOnlyList<SignalRHubInfo> hubs);
}
