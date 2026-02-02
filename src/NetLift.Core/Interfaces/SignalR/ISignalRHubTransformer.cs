using NetLift.Core.Models.SignalR;

namespace NetLift.Core.Interfaces.SignalR;

/// <summary>
/// Transforms ASP.NET SignalR Hub code to ASP.NET Core SignalR.
/// </summary>
public interface ISignalRHubTransformer
{
    /// <summary>
    /// Transforms a SignalR Hub source file to ASP.NET Core SignalR.
    /// </summary>
    /// <param name="sourceCode">The original C# source code.</param>
    /// <param name="hubInfo">The analyzed hub information.</param>
    /// <returns>The transformed file result.</returns>
    TransformedSignalRFile TransformHub(string sourceCode, SignalRHubInfo hubInfo);

    /// <summary>
    /// Transforms a file containing GlobalHost usage to use IHubContext.
    /// </summary>
    /// <param name="sourceCode">The original C# source code.</param>
    /// <param name="globalHostInfo">The analyzed GlobalHost usage information.</param>
    /// <returns>The transformed file result.</returns>
    TransformedSignalRFile TransformGlobalHostUsage(string sourceCode, GlobalHostUsageInfo globalHostInfo);
}
