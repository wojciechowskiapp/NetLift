# [TASK-053] Parse system.serviceModel Configuration

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P1 |
| **Estimate** | M |
| **Sprint** | 6 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** none
- **Blocks:** TASK-057 (Extract Business Logic)

---

## Description

Parse WCF configuration from Web.config or App.config files to extract service endpoints, bindings, behaviors, and security settings. This information helps understand the original WCF service configuration and informs the modern API design.

---

## Acceptance Criteria

- [ ] Parse <system.serviceModel> section from config files
- [ ] Extract service endpoints (address, binding, contract)
- [ ] Parse binding configurations (basicHttpBinding, wsHttpBinding, netTcpBinding)
- [ ] Extract behavior configurations (service and endpoint behaviors)
- [ ] Detect security settings (transport, message, none)
- [ ] Parse metadata exposure settings (MEX endpoints)
- [ ] Store configuration in WcfServiceConfiguration model
- [ ] Unit tests with various WCF config scenarios

---

## Technical Notes

### WCF Web.config Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.serviceModel>

    <!-- Service Definitions -->
    <services>
      <service name="LegacyApp.Services.CustomerService"
               behaviorConfiguration="CustomerServiceBehavior">

        <!-- HTTP Endpoint -->
        <endpoint address=""
                  binding="basicHttpBinding"
                  bindingConfiguration="SecureBasicBinding"
                  contract="LegacyApp.Services.ICustomerService" />

        <!-- HTTPS Endpoint -->
        <endpoint address="secure"
                  binding="wsHttpBinding"
                  bindingConfiguration="SecureWsBinding"
                  contract="LegacyApp.Services.ICustomerService" />

        <!-- TCP Endpoint -->
        <endpoint address="net.tcp://localhost:8090/CustomerService"
                  binding="netTcpBinding"
                  bindingConfiguration="SecureTcpBinding"
                  contract="LegacyApp.Services.ICustomerService" />

        <!-- Metadata Exchange -->
        <endpoint address="mex"
                  binding="mexHttpBinding"
                  contract="IMetadataExchange" />
      </service>

      <service name="LegacyApp.Services.OrderService"
               behaviorConfiguration="OrderServiceBehavior">
        <endpoint address=""
                  binding="basicHttpBinding"
                  contract="LegacyApp.Services.IOrderService" />
        <endpoint address="mex"
                  binding="mexHttpBinding"
                  contract="IMetadataExchange" />
      </service>
    </services>

    <!-- Binding Configurations -->
    <bindings>
      <basicHttpBinding>
        <binding name="SecureBasicBinding"
                 maxReceivedMessageSize="1048576"
                 maxBufferSize="1048576"
                 receiveTimeout="00:10:00"
                 sendTimeout="00:10:00">
          <security mode="Transport">
            <transport clientCredentialType="None" />
          </security>
        </binding>
      </basicHttpBinding>

      <wsHttpBinding>
        <binding name="SecureWsBinding"
                 maxReceivedMessageSize="2097152">
          <security mode="Message">
            <message clientCredentialType="UserName" />
          </security>
        </binding>
      </wsHttpBinding>

      <netTcpBinding>
        <binding name="SecureTcpBinding"
                 maxReceivedMessageSize="5242880"
                 receiveTimeout="00:20:00">
          <security mode="Transport">
            <transport clientCredentialType="Windows" />
          </security>
        </binding>
      </netTcpBinding>
    </bindings>

    <!-- Behaviors -->
    <behaviors>
      <serviceBehaviors>
        <behavior name="CustomerServiceBehavior">
          <serviceMetadata httpGetEnabled="true" httpsGetEnabled="true" />
          <serviceDebug includeExceptionDetailInFaults="false" />
          <serviceThrottling maxConcurrentCalls="100"
                           maxConcurrentInstances="50"
                           maxConcurrentSessions="50" />
        </behavior>

        <behavior name="OrderServiceBehavior">
          <serviceMetadata httpGetEnabled="true" />
          <serviceDebug includeExceptionDetailInFaults="true" />
        </behavior>
      </serviceBehaviors>
    </behaviors>

    <!-- Service Host -->
    <serviceHostingEnvironment multipleSiteBindingsEnabled="true" />

  </system.serviceModel>
</configuration>
```

### Parsed Configuration Output

```csharp
public record WcfServiceConfiguration
{
    public List<WcfServiceDefinition> Services { get; init; } = new();
    public List<WcfBindingConfiguration> Bindings { get; init; } = new();
    public List<WcfBehaviorConfiguration> Behaviors { get; init; } = new();
}

public record WcfServiceDefinition
{
    public string ServiceName { get; init; }
    public string BehaviorConfiguration { get; init; }
    public List<WcfEndpoint> Endpoints { get; init; } = new();
}

public record WcfEndpoint
{
    public string Address { get; init; }
    public string Binding { get; init; }
    public string BindingConfiguration { get; init; }
    public string Contract { get; init; }
    public bool IsMetadataExchange { get; init; }
}

public record WcfBindingConfiguration
{
    public string BindingType { get; init; } // basicHttpBinding, wsHttpBinding, netTcpBinding
    public string Name { get; init; }
    public long MaxReceivedMessageSize { get; init; }
    public TimeSpan ReceiveTimeout { get; init; }
    public TimeSpan SendTimeout { get; init; }
    public WcfSecurityMode SecurityMode { get; init; }
    public string ClientCredentialType { get; init; }
}

public record WcfBehaviorConfiguration
{
    public string Name { get; init; }
    public bool MetadataHttpGetEnabled { get; init; }
    public bool MetadataHttpsGetEnabled { get; init; }
    public bool IncludeExceptionDetailInFaults { get; init; }
    public int? MaxConcurrentCalls { get; init; }
    public int? MaxConcurrentInstances { get; init; }
    public int? MaxConcurrentSessions { get; init; }
}

public enum WcfSecurityMode
{
    None,
    Transport,
    Message,
    TransportWithMessageCredential
}
```

### Modern appsettings.json Equivalent

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      },
      "Https": {
        "Url": "https://localhost:5001"
      },
      "Grpc": {
        "Url": "http://localhost:5002",
        "Protocols": "Http2"
      }
    },
    "Limits": {
      "MaxRequestBodySize": 2097152,
      "RequestHeadersTimeout": "00:10:00"
    }
  },
  "ApiSettings": {
    "MaxPageSize": 100,
    "DefaultPageSize": 20,
    "RequestTimeout": "00:10:00"
  },
  "Authentication": {
    "Schemes": {
      "Bearer": {
        "Authority": "https://auth.company.com",
        "Audience": "customer-api"
      }
    }
  },
  "OpenApi": {
    "Info": {
      "Title": "Customer API",
      "Version": "v1",
      "Description": "Migrated from LegacyApp.Services.CustomerService"
    }
  }
}
```

### Parsing Strategy (XDocument)

```csharp
public class WcfConfigurationParser
{
    public WcfServiceConfiguration Parse(string configPath)
    {
        var doc = XDocument.Load(configPath);
        var ns = doc.Root.GetDefaultNamespace();
        var serviceModel = doc.Root.Element("system.serviceModel");

        if (serviceModel == null)
            return null;

        return new WcfServiceConfiguration
        {
            Services = ParseServices(serviceModel.Element("services")),
            Bindings = ParseBindings(serviceModel.Element("bindings")),
            Behaviors = ParseBehaviors(serviceModel.Element("behaviors"))
        };
    }

    private List<WcfServiceDefinition> ParseServices(XElement servicesElement)
    {
        if (servicesElement == null)
            return new();

        return servicesElement.Elements("service")
            .Select(s => new WcfServiceDefinition
            {
                ServiceName = s.Attribute("name")?.Value,
                BehaviorConfiguration = s.Attribute("behaviorConfiguration")?.Value,
                Endpoints = s.Elements("endpoint")
                    .Select(ParseEndpoint)
                    .ToList()
            })
            .ToList();
    }

    private WcfEndpoint ParseEndpoint(XElement endpointElement)
    {
        var contract = endpointElement.Attribute("contract")?.Value;

        return new WcfEndpoint
        {
            Address = endpointElement.Attribute("address")?.Value,
            Binding = endpointElement.Attribute("binding")?.Value,
            BindingConfiguration = endpointElement.Attribute("bindingConfiguration")?.Value,
            Contract = contract,
            IsMetadataExchange = contract == "IMetadataExchange"
        };
    }
}
```

### Configuration Migration Notes

| WCF Concept | Modern .NET Equivalent |
|-------------|------------------------|
| `basicHttpBinding` | REST API with HTTP/1.1 |
| `wsHttpBinding` | REST API with JWT auth |
| `netTcpBinding` | gRPC with HTTP/2 |
| `mexHttpBinding` | OpenAPI/Swagger |
| `serviceThrottling` | Rate limiting middleware |
| `serviceMetadata` | Swagger UI |
| Transport security | HTTPS + TLS |
| Message security | JWT Bearer tokens |

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
