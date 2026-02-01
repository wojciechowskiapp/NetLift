using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Wcf;

namespace NetLift.Transforms.Wcf.Analyzers;

/// <summary>
/// Detects WCF duplex/callback patterns that cannot be automatically migrated to .NET Core.
/// </summary>
public sealed class DuplexDetector : IDuplexDetector
{
    private readonly List<string> _diagnostics = [];

    /// <inheritdoc />
    public IReadOnlyCollection<string> Diagnostics => _diagnostics.AsReadOnly();

    /// <inheritdoc />
    public DuplexWarningReport Detect(
        IReadOnlyList<WcfServiceContract> contracts,
        WcfServiceConfiguration? config)
    {
        _diagnostics.Clear();

        var warnings = new List<DuplexWarning>();

        // Detect callback contracts from service interfaces
        foreach (var contract in contracts)
        {
            if (!string.IsNullOrEmpty(contract.CallbackContract))
            {
                var warning = CreateCallbackWarning(contract, config);
                warnings.Add(warning);
                _diagnostics.Add($"Detected duplex contract: {contract.InterfaceName} with callback {contract.CallbackContract}");
            }
        }

        // Detect duplex bindings in configuration
        if (config != null)
        {
            var duplexBindingWarnings = DetectDuplexBindings(config, contracts);
            warnings.AddRange(duplexBindingWarnings);
        }

        var guidanceMarkdown = GenerateMigrationGuidance(warnings);

        return new DuplexWarningReport
        {
            Warnings = warnings,
            MigrationGuidanceMarkdown = guidanceMarkdown
        };
    }

    /// <summary>
    /// Creates a duplex warning for a service contract with a callback contract.
    /// </summary>
    private DuplexWarning CreateCallbackWarning(
        WcfServiceContract contract,
        WcfServiceConfiguration? config)
    {
        var duplexBindings = GetDuplexBindingsForService(contract, config);

        return new DuplexWarning
        {
            ServiceName = contract.InterfaceName,
            CallbackContractName = contract.CallbackContract!,
            Severity = DuplexWarningSeverity.High,
            CallbackMethods = [], // Will be populated if we can parse the callback interface
            DuplexBindings = duplexBindings
        };
    }

    /// <summary>
    /// Detects duplex bindings in the WCF configuration that don't have corresponding callback contracts.
    /// </summary>
    private List<DuplexWarning> DetectDuplexBindings(
        WcfServiceConfiguration config,
        IReadOnlyList<WcfServiceContract> contracts)
    {
        var warnings = new List<DuplexWarning>();
        var knownDuplexBindings = new HashSet<string>
        {
            "wsDualHttpBinding",
            "netTcpBinding" // Can be duplex if used with callback contract
        };

        // Build a set of contract names that already have callback warnings
        var contractsWithCallbacks = new HashSet<string>(
            contracts
                .Where(c => !string.IsNullOrEmpty(c.CallbackContract))
                .SelectMany(c => new[] { c.InterfaceName, c.FullyQualifiedName }));

        foreach (var binding in config.Bindings)
        {
            if (knownDuplexBindings.Contains(binding.BindingType))
            {
                // Find services using this binding
                var servicesUsingBinding = config.Services
                    .SelectMany(s => s.Endpoints
                        .Where(e => e.Binding == binding.BindingType || e.BindingConfiguration == binding.Name)
                        .Select(e => new { Service = s, Endpoint = e }))
                    .ToList();

                foreach (var svcEndpoint in servicesUsingBinding)
                {
                    _diagnostics.Add($"Detected duplex binding {binding.BindingType} for service {svcEndpoint.Service.ServiceName}");

                    // Only create warning if not already covered by callback contract detection
                    var contractName = svcEndpoint.Endpoint.Contract;
                    if (!contractsWithCallbacks.Contains(contractName))
                    {
                        warnings.Add(new DuplexWarning
                        {
                            ServiceName = contractName,
                            CallbackContractName = "Unknown (binding-based detection)",
                            Severity = DuplexWarningSeverity.Medium,
                            CallbackMethods = [],
                            DuplexBindings = [binding.BindingType]
                        });
                    }
                }
            }
        }

        return warnings;
    }

    /// <summary>
    /// Gets the list of duplex bindings used by endpoints implementing the specified service.
    /// </summary>
    private IReadOnlyList<string> GetDuplexBindingsForService(
        WcfServiceContract contract,
        WcfServiceConfiguration? config)
    {
        if (config == null)
        {
            return [];
        }

        var duplexBindings = new HashSet<string>
        {
            "wsDualHttpBinding",
            "netTcpBinding"
        };

        var bindings = new HashSet<string>();

        foreach (var service in config.Services)
        {
            foreach (var endpoint in service.Endpoints)
            {
                // Match by contract interface name
                if (endpoint.Contract == contract.FullyQualifiedName ||
                    endpoint.Contract == contract.InterfaceName)
                {
                    if (duplexBindings.Contains(endpoint.Binding))
                    {
                        bindings.Add(endpoint.Binding);
                    }
                }
            }
        }

        return bindings.ToList();
    }

    /// <summary>
    /// Generates markdown migration guidance based on detected duplex warnings.
    /// </summary>
    private string GenerateMigrationGuidance(List<DuplexWarning> warnings)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# WCF Duplex Contract Migration Guidance");
        sb.AppendLine();

        if (warnings.Count == 0)
        {
            sb.AppendLine("No duplex contracts detected. Your WCF services use request-response patterns that can be migrated to gRPC or REST APIs.");
            return sb.ToString();
        }

        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"Detected **{warnings.Count}** duplex contract(s) that require manual migration.");
        sb.AppendLine();
        sb.AppendLine("WCF duplex contracts (callback patterns) cannot be automatically migrated because:");
        sb.AppendLine("- gRPC does not support client callbacks in the same way as WCF");
        sb.AppendLine("- REST APIs are inherently request-response only");
        sb.AppendLine("- Modern alternatives use different architectural patterns");
        sb.AppendLine();

        sb.AppendLine("## Detected Duplex Contracts");
        sb.AppendLine();

        foreach (var warning in warnings.Where(w => w.Severity == DuplexWarningSeverity.High))
        {
            sb.AppendLine($"### {warning.ServiceName}");
            sb.AppendLine();
            sb.AppendLine($"- **Callback Contract**: `{warning.CallbackContractName}`");
            if (warning.DuplexBindings.Count > 0)
            {
                sb.AppendLine($"- **Duplex Bindings**: {string.Join(", ", warning.DuplexBindings)}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Migration Options");
        sb.AppendLine();

        sb.AppendLine("### Option 1: SignalR (Recommended for Web Clients)");
        sb.AppendLine();
        sb.AppendLine("**Best for**: Real-time web applications, browser clients, mobile apps");
        sb.AppendLine();
        sb.AppendLine("```csharp");
        sb.AppendLine("// Server-side Hub");
        sb.AppendLine("public class NotificationHub : Hub");
        sb.AppendLine("{");
        sb.AppendLine("    public async Task SendNotification(string message)");
        sb.AppendLine("    {");
        sb.AppendLine("        await Clients.All.SendAsync(\"ReceiveNotification\", message);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("// Client-side (JavaScript/TypeScript)");
        sb.AppendLine("const connection = new signalR.HubConnectionBuilder()");
        sb.AppendLine("    .withUrl(\"/notificationHub\")");
        sb.AppendLine("    .build();");
        sb.AppendLine();
        sb.AppendLine("connection.on(\"ReceiveNotification\", (message) => {");
        sb.AppendLine("    console.log(message);");
        sb.AppendLine("});");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**NuGet Packages**:");
        sb.AppendLine("- `Microsoft.AspNetCore.SignalR`");
        sb.AppendLine();

        sb.AppendLine("### Option 2: gRPC Server Streaming (Recommended for .NET Clients)");
        sb.AppendLine();
        sb.AppendLine("**Best for**: Server-to-client push, .NET-to-.NET communication");
        sb.AppendLine();
        sb.AppendLine("```protobuf");
        sb.AppendLine("service NotificationService {");
        sb.AppendLine("    rpc Subscribe (SubscribeRequest) returns (stream Notification);");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("```csharp");
        sb.AppendLine("// Server implementation");
        sb.AppendLine("public override async Task Subscribe(");
        sb.AppendLine("    SubscribeRequest request,");
        sb.AppendLine("    IServerStreamWriter<Notification> responseStream,");
        sb.AppendLine("    ServerCallContext context)");
        sb.AppendLine("{");
        sb.AppendLine("    while (!context.CancellationToken.IsCancellationRequested)");
        sb.AppendLine("    {");
        sb.AppendLine("        await responseStream.WriteAsync(new Notification { Message = \"Update\" });");
        sb.AppendLine("        await Task.Delay(1000);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**NuGet Packages**:");
        sb.AppendLine("- `Grpc.AspNetCore`");
        sb.AppendLine("- `Grpc.Net.Client`");
        sb.AppendLine();

        sb.AppendLine("### Option 3: WebSockets (For Custom Implementations)");
        sb.AppendLine();
        sb.AppendLine("**Best for**: Custom protocols, low-level control");
        sb.AppendLine();
        sb.AppendLine("```csharp");
        sb.AppendLine("// ASP.NET Core WebSocket middleware");
        sb.AppendLine("app.UseWebSockets();");
        sb.AppendLine("app.Use(async (context, next) =>");
        sb.AppendLine("{");
        sb.AppendLine("    if (context.Request.Path == \"/ws\")");
        sb.AppendLine("    {");
        sb.AppendLine("        if (context.WebSockets.IsWebSocketRequest)");
        sb.AppendLine("        {");
        sb.AppendLine("            var webSocket = await context.WebSockets.AcceptWebSocketAsync();");
        sb.AppendLine("            await HandleWebSocket(webSocket);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    else await next();");
        sb.AppendLine("});");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Comparison Matrix");
        sb.AppendLine();
        sb.AppendLine("| Feature | SignalR | gRPC Streaming | WebSockets |");
        sb.AppendLine("|---------|---------|----------------|------------|");
        sb.AppendLine("| Browser Support | Excellent | Limited | Good |");
        sb.AppendLine("| .NET Client | Excellent | Excellent | Good |");
        sb.AppendLine("| Ease of Use | High | Medium | Low |");
        sb.AppendLine("| Performance | Good | Excellent | Excellent |");
        sb.AppendLine("| Fallback Support | Yes (Long Polling) | No | No |");
        sb.AppendLine("| Type Safety | Medium | High | Low |");
        sb.AppendLine();

        sb.AppendLine("## Migration Checklist");
        sb.AppendLine();
        sb.AppendLine("For each duplex contract:");
        sb.AppendLine();
        sb.AppendLine("1. [ ] Identify the callback methods and their purpose");
        sb.AppendLine("2. [ ] Determine the client types (browser, .NET, mobile)");
        sb.AppendLine("3. [ ] Choose the appropriate migration option (SignalR, gRPC, WebSockets)");
        sb.AppendLine("4. [ ] Design the new communication pattern");
        sb.AppendLine("5. [ ] Implement server-side hub/service");
        sb.AppendLine("6. [ ] Update client code to connect and listen for messages");
        sb.AppendLine("7. [ ] Test bidirectional communication");
        sb.AppendLine("8. [ ] Update authentication/authorization if needed");
        sb.AppendLine("9. [ ] Performance test under load");
        sb.AppendLine("10. [ ] Update documentation");
        sb.AppendLine();

        sb.AppendLine("## Additional Resources");
        sb.AppendLine();
        sb.AppendLine("- [ASP.NET Core SignalR Documentation](https://learn.microsoft.com/aspnet/core/signalr/)");
        sb.AppendLine("- [gRPC on .NET Documentation](https://learn.microsoft.com/aspnet/core/grpc/)");
        sb.AppendLine("- [WebSockets in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/websockets)");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Parses a callback interface from source code to extract callback methods.
    /// This is a helper method that can be used if the callback interface source is available.
    /// </summary>
    public IReadOnlyList<CallbackMethod> ParseCallbackMethods(string callbackInterfaceSource)
    {
        var methods = new List<CallbackMethod>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(callbackInterfaceSource);
            var root = tree.GetRoot();

            var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();

            foreach (var interfaceDecl in interfaces)
            {
                var methodDeclarations = interfaceDecl.Members.OfType<MethodDeclarationSyntax>();

                foreach (var method in methodDeclarations)
                {
                    var isOneWay = method.AttributeLists
                        .SelectMany(al => al.Attributes)
                        .Any(attr => attr.Name.ToString().Contains("OperationContract") &&
                                   attr.ArgumentList?.Arguments.Any(arg =>
                                       arg.ToString().Contains("IsOneWay") &&
                                       arg.ToString().Contains("true")) == true);

                    var parameters = method.ParameterList.Parameters
                        .Select(p => new MethodParameter
                        {
                            Name = p.Identifier.Text,
                            Type = p.Type?.ToString() ?? "unknown"
                        })
                        .ToList();

                    methods.Add(new CallbackMethod
                    {
                        Name = method.Identifier.Text,
                        IsOneWay = isOneWay,
                        Parameters = parameters
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"Error parsing callback interface: {ex.Message}");
        }

        return methods;
    }
}
