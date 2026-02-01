using System.Globalization;
using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Wcf;

namespace NetLift.Transforms.Wcf.Parsers;

/// <summary>
/// Parses WCF system.serviceModel configuration from Web.config or App.config files.
/// </summary>
public class ServiceModelParser : IServiceModelParser
{
    private readonly List<string> _diagnostics = new();

    /// <inheritdoc />
    public IReadOnlyCollection<string> Diagnostics => _diagnostics.AsReadOnly();

    /// <inheritdoc />
    public WcfServiceConfiguration? Parse(string configContent)
    {
        _diagnostics.Clear();

        if (string.IsNullOrWhiteSpace(configContent))
        {
            _diagnostics.Add("Configuration content is empty");
            return null;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(configContent);
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"Failed to parse XML: {ex.Message}");
            return null;
        }

        var serviceModel = doc.Root?.Element("system.serviceModel");
        if (serviceModel == null)
        {
            return null; // No WCF configuration present - this is not an error
        }

        var services = ParseServices(serviceModel);
        var bindings = ParseBindings(serviceModel);
        var behaviors = ParseBehaviors(serviceModel);
        var multipleSiteBindingsEnabled = ParseServiceHostingEnvironment(serviceModel);

        return new WcfServiceConfiguration
        {
            Services = services,
            Bindings = bindings,
            Behaviors = behaviors,
            MultipleSiteBindingsEnabled = multipleSiteBindingsEnabled
        };
    }

    private IReadOnlyList<WcfServiceDefinition> ParseServices(XElement serviceModel)
    {
        var servicesElement = serviceModel.Element("services");
        if (servicesElement == null)
        {
            return Array.Empty<WcfServiceDefinition>();
        }

        var services = new List<WcfServiceDefinition>();

        foreach (var serviceElement in servicesElement.Elements("service"))
        {
            var service = ParseService(serviceElement);
            if (service != null)
            {
                services.Add(service);
            }
        }

        return services;
    }

    private WcfServiceDefinition? ParseService(XElement serviceElement)
    {
        var serviceName = serviceElement.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            _diagnostics.Add("Service definition missing 'name' attribute");
            return null;
        }

        var behaviorConfiguration = serviceElement.Attribute("behaviorConfiguration")?.Value;
        var endpoints = ParseEndpoints(serviceElement);

        return new WcfServiceDefinition
        {
            ServiceName = serviceName,
            BehaviorConfiguration = behaviorConfiguration,
            Endpoints = endpoints
        };
    }

    private IReadOnlyList<WcfEndpoint> ParseEndpoints(XElement serviceElement)
    {
        var endpoints = new List<WcfEndpoint>();

        foreach (var endpointElement in serviceElement.Elements("endpoint"))
        {
            var endpoint = ParseEndpoint(endpointElement);
            if (endpoint != null)
            {
                endpoints.Add(endpoint);
            }
        }

        return endpoints;
    }

    private WcfEndpoint? ParseEndpoint(XElement endpointElement)
    {
        var address = endpointElement.Attribute("address")?.Value ?? string.Empty;
        var binding = endpointElement.Attribute("binding")?.Value;
        var contract = endpointElement.Attribute("contract")?.Value;

        if (string.IsNullOrWhiteSpace(binding))
        {
            _diagnostics.Add("Endpoint missing 'binding' attribute");
            return null;
        }

        if (string.IsNullOrWhiteSpace(contract))
        {
            _diagnostics.Add("Endpoint missing 'contract' attribute");
            return null;
        }

        var bindingConfiguration = endpointElement.Attribute("bindingConfiguration")?.Value;
        var isMetadataExchange = IsMetadataExchangeContract(contract);

        return new WcfEndpoint
        {
            Address = address,
            Binding = binding,
            BindingConfiguration = bindingConfiguration,
            Contract = contract,
            IsMetadataExchange = isMetadataExchange
        };
    }

    private static bool IsMetadataExchangeContract(string contract)
    {
        return contract.Equals("IMetadataExchange", StringComparison.OrdinalIgnoreCase) ||
               contract.EndsWith(".IMetadataExchange", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<WcfBindingConfig> ParseBindings(XElement serviceModel)
    {
        var bindings = new List<WcfBindingConfig>();

        var bindingsElement = serviceModel.Element("bindings");
        if (bindingsElement == null)
        {
            return bindings;
        }

        // Parse all binding types
        ParseBindingType(bindingsElement, "basicHttpBinding", bindings);
        ParseBindingType(bindingsElement, "wsHttpBinding", bindings);
        ParseBindingType(bindingsElement, "netTcpBinding", bindings);
        ParseBindingType(bindingsElement, "netNamedPipeBinding", bindings);
        ParseBindingType(bindingsElement, "netMsmqBinding", bindings);
        ParseBindingType(bindingsElement, "customBinding", bindings);

        return bindings;
    }

    private void ParseBindingType(XElement bindingsElement, string bindingType, List<WcfBindingConfig> bindings)
    {
        var bindingTypeElement = bindingsElement.Element(bindingType);
        if (bindingTypeElement == null)
        {
            return;
        }

        foreach (var bindingElement in bindingTypeElement.Elements("binding"))
        {
            var binding = ParseBinding(bindingElement, bindingType);
            if (binding != null)
            {
                bindings.Add(binding);
            }
        }
    }

    private WcfBindingConfig? ParseBinding(XElement bindingElement, string bindingType)
    {
        var name = bindingElement.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            _diagnostics.Add($"Binding configuration for {bindingType} missing 'name' attribute");
            return null;
        }

        var maxReceivedMessageSize = ParseLongAttribute(bindingElement, "maxReceivedMessageSize") ?? 65536;
        var maxBufferSize = ParseLongAttribute(bindingElement, "maxBufferSize") ?? 65536;
        var receiveTimeout = ParseTimeSpanAttribute(bindingElement, "receiveTimeout") ?? TimeSpan.FromMinutes(10);
        var sendTimeout = ParseTimeSpanAttribute(bindingElement, "sendTimeout") ?? TimeSpan.FromMinutes(1);

        var securityConfig = ParseSecurityConfiguration(bindingElement);

        return new WcfBindingConfig
        {
            BindingType = bindingType,
            Name = name,
            MaxReceivedMessageSize = maxReceivedMessageSize,
            MaxBufferSize = maxBufferSize,
            ReceiveTimeout = receiveTimeout,
            SendTimeout = sendTimeout,
            SecurityMode = securityConfig.Mode,
            ClientCredentialType = securityConfig.ClientCredentialType
        };
    }

    private (WcfSecurityMode Mode, string? ClientCredentialType) ParseSecurityConfiguration(XElement bindingElement)
    {
        var securityElement = bindingElement.Element("security");
        if (securityElement == null)
        {
            return (WcfSecurityMode.None, null);
        }

        var modeAttr = securityElement.Attribute("mode")?.Value;
        var mode = ParseSecurityMode(modeAttr);

        string? clientCredentialType = null;

        // Parse transport or message security
        var transportElement = securityElement.Element("transport");
        if (transportElement != null)
        {
            clientCredentialType = transportElement.Attribute("clientCredentialType")?.Value;
        }
        else
        {
            var messageElement = securityElement.Element("message");
            if (messageElement != null)
            {
                clientCredentialType = messageElement.Attribute("clientCredentialType")?.Value;
            }
        }

        return (mode, clientCredentialType);
    }

    private static WcfSecurityMode ParseSecurityMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return WcfSecurityMode.None;
        }

        return mode.ToLowerInvariant() switch
        {
            "none" => WcfSecurityMode.None,
            "transport" => WcfSecurityMode.Transport,
            "message" => WcfSecurityMode.Message,
            "transportwithmessagecredential" => WcfSecurityMode.TransportWithMessageCredential,
            _ => WcfSecurityMode.None
        };
    }

    private IReadOnlyList<WcfBehaviorConfig> ParseBehaviors(XElement serviceModel)
    {
        var behaviors = new List<WcfBehaviorConfig>();

        var behaviorsElement = serviceModel.Element("behaviors");
        if (behaviorsElement == null)
        {
            return behaviors;
        }

        var serviceBehaviorsElement = behaviorsElement.Element("serviceBehaviors");
        if (serviceBehaviorsElement == null)
        {
            return behaviors;
        }

        foreach (var behaviorElement in serviceBehaviorsElement.Elements("behavior"))
        {
            var behavior = ParseBehavior(behaviorElement);
            if (behavior != null)
            {
                behaviors.Add(behavior);
            }
        }

        return behaviors;
    }

    private WcfBehaviorConfig? ParseBehavior(XElement behaviorElement)
    {
        var name = behaviorElement.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            _diagnostics.Add("Behavior configuration missing 'name' attribute");
            return null;
        }

        var serviceMetadata = behaviorElement.Element("serviceMetadata");
        var metadataHttpGetEnabled = false;
        var metadataHttpsGetEnabled = false;

        if (serviceMetadata != null)
        {
            metadataHttpGetEnabled = ParseBoolAttribute(serviceMetadata, "httpGetEnabled", false);
            metadataHttpsGetEnabled = ParseBoolAttribute(serviceMetadata, "httpsGetEnabled", false);
        }

        var serviceDebug = behaviorElement.Element("serviceDebug");
        var includeExceptionDetailInFaults = false;

        if (serviceDebug != null)
        {
            includeExceptionDetailInFaults = ParseBoolAttribute(serviceDebug, "includeExceptionDetailInFaults", false);
        }

        var serviceThrottling = behaviorElement.Element("serviceThrottling");
        int? maxConcurrentCalls = null;
        int? maxConcurrentInstances = null;
        int? maxConcurrentSessions = null;

        if (serviceThrottling != null)
        {
            maxConcurrentCalls = ParseIntAttribute(serviceThrottling, "maxConcurrentCalls");
            maxConcurrentInstances = ParseIntAttribute(serviceThrottling, "maxConcurrentInstances");
            maxConcurrentSessions = ParseIntAttribute(serviceThrottling, "maxConcurrentSessions");
        }

        return new WcfBehaviorConfig
        {
            Name = name,
            MetadataHttpGetEnabled = metadataHttpGetEnabled,
            MetadataHttpsGetEnabled = metadataHttpsGetEnabled,
            IncludeExceptionDetailInFaults = includeExceptionDetailInFaults,
            MaxConcurrentCalls = maxConcurrentCalls,
            MaxConcurrentInstances = maxConcurrentInstances,
            MaxConcurrentSessions = maxConcurrentSessions
        };
    }

    private static bool ParseServiceHostingEnvironment(XElement serviceModel)
    {
        var serviceHostingEnvironment = serviceModel.Element("serviceHostingEnvironment");
        if (serviceHostingEnvironment == null)
        {
            return false;
        }

        return ParseBoolAttribute(serviceHostingEnvironment, "multipleSiteBindingsEnabled", false);
    }

    private static bool ParseBoolAttribute(XElement element, string attributeName, bool defaultValue)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ParseIntAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static long? ParseLongAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private TimeSpan? ParseTimeSpanAttribute(XElement element, string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // WCF timeouts can be in various formats:
        // - "00:01:00" (hh:mm:ss)
        // - "00:00:30" (30 seconds)
        // - "1.00:00:00" (days.hh:mm:ss)
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        _diagnostics.Add($"Failed to parse TimeSpan attribute '{attributeName}' with value '{value}'");
        return null;
    }
}
