using FluentAssertions;
using NetLift.Core.Models.Wcf;
using NetLift.Transforms.Wcf.Parsers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Wcf.Parsers;

public class ServiceModelParserTests
{
    private readonly ServiceModelParser _parser = new();

    [Fact]
    public void Parse_EmptyContent_ReturnsNull()
    {
        // Act
        var result = _parser.Parse(string.Empty);

        // Assert
        result.Should().BeNull();
        _parser.Diagnostics.Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void Parse_InvalidXml_ReturnsNull()
    {
        // Arrange
        var invalidXml = "<configuration><system.serviceModel";

        // Act
        var result = _parser.Parse(invalidXml);

        // Assert
        result.Should().BeNull();
        _parser.Diagnostics.Should().ContainSingle()
            .Which.Should().Contain("Failed to parse XML");
    }

    [Fact]
    public void Parse_NoServiceModelSection_ReturnsNull()
    {
        // Arrange
        var config = @"
<configuration>
    <appSettings>
        <add key=""test"" value=""value"" />
    </appSettings>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().BeNull();
        _parser.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyServiceModelSection_ReturnsEmptyConfiguration()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Services.Should().BeEmpty();
        result.Bindings.Should().BeEmpty();
        result.Behaviors.Should().BeEmpty();
        result.MultipleSiteBindingsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Parse_SingleService_ParsesCorrectly()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <services>
            <service name=""MyNamespace.MyService"" behaviorConfiguration=""MyBehavior"">
                <endpoint address="""" binding=""basicHttpBinding"" contract=""MyNamespace.IMyService"" />
            </service>
        </services>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Services.Should().HaveCount(1);

        var service = result.Services[0];
        service.ServiceName.Should().Be("MyNamespace.MyService");
        service.BehaviorConfiguration.Should().Be("MyBehavior");
        service.Endpoints.Should().HaveCount(1);

        var endpoint = service.Endpoints[0];
        endpoint.Address.Should().Be(string.Empty);
        endpoint.Binding.Should().Be("basicHttpBinding");
        endpoint.Contract.Should().Be("MyNamespace.IMyService");
        endpoint.IsMetadataExchange.Should().BeFalse();
    }

    [Fact]
    public void Parse_MultipleServices_ParsesAll()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <services>
            <service name=""MyNamespace.Service1"">
                <endpoint address="""" binding=""basicHttpBinding"" contract=""MyNamespace.IService1"" />
            </service>
            <service name=""MyNamespace.Service2"">
                <endpoint address="""" binding=""wsHttpBinding"" contract=""MyNamespace.IService2"" />
            </service>
        </services>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Services.Should().HaveCount(2);
        result.Services[0].ServiceName.Should().Be("MyNamespace.Service1");
        result.Services[1].ServiceName.Should().Be("MyNamespace.Service2");
    }

    [Fact]
    public void Parse_ServiceWithMultipleEndpoints_ParsesAll()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <services>
            <service name=""MyNamespace.MyService"">
                <endpoint address="""" binding=""basicHttpBinding"" contract=""MyNamespace.IMyService"" />
                <endpoint address=""mex"" binding=""mexHttpBinding"" contract=""IMetadataExchange"" />
                <endpoint address=""net.tcp://localhost:8080/MyService"" binding=""netTcpBinding"" contract=""MyNamespace.IMyService"" />
            </service>
        </services>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Services.Should().HaveCount(1);

        var service = result.Services[0];
        service.Endpoints.Should().HaveCount(3);

        service.Endpoints[0].Binding.Should().Be("basicHttpBinding");
        service.Endpoints[0].IsMetadataExchange.Should().BeFalse();

        service.Endpoints[1].Address.Should().Be("mex");
        service.Endpoints[1].IsMetadataExchange.Should().BeTrue();

        service.Endpoints[2].Address.Should().Be("net.tcp://localhost:8080/MyService");
        service.Endpoints[2].Binding.Should().Be("netTcpBinding");
    }

    [Fact]
    public void Parse_EndpointWithBindingConfiguration_ParsesReference()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <services>
            <service name=""MyNamespace.MyService"">
                <endpoint address="""" binding=""basicHttpBinding"" bindingConfiguration=""LargeMessages"" contract=""MyNamespace.IMyService"" />
            </service>
        </services>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        var endpoint = result!.Services[0].Endpoints[0];
        endpoint.BindingConfiguration.Should().Be("LargeMessages");
    }

    [Fact]
    public void Parse_BasicHttpBinding_ParsesCorrectly()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <bindings>
            <basicHttpBinding>
                <binding name=""LargeMessages""
                         maxReceivedMessageSize=""2147483647""
                         maxBufferSize=""2147483647""
                         receiveTimeout=""00:10:00""
                         sendTimeout=""00:05:00"">
                    <security mode=""Transport"">
                        <transport clientCredentialType=""Windows"" />
                    </security>
                </binding>
            </basicHttpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Bindings.Should().HaveCount(1);

        var binding = result.Bindings[0];
        binding.BindingType.Should().Be("basicHttpBinding");
        binding.Name.Should().Be("LargeMessages");
        binding.MaxReceivedMessageSize.Should().Be(2147483647);
        binding.MaxBufferSize.Should().Be(2147483647);
        binding.ReceiveTimeout.Should().Be(TimeSpan.FromMinutes(10));
        binding.SendTimeout.Should().Be(TimeSpan.FromMinutes(5));
        binding.SecurityMode.Should().Be(WcfSecurityMode.Transport);
        binding.ClientCredentialType.Should().Be("Windows");
    }

    [Fact]
    public void Parse_WsHttpBinding_ParsesCorrectly()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <bindings>
            <wsHttpBinding>
                <binding name=""SecureBinding"">
                    <security mode=""Message"">
                        <message clientCredentialType=""UserName"" />
                    </security>
                </binding>
            </wsHttpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Bindings.Should().HaveCount(1);

        var binding = result.Bindings[0];
        binding.BindingType.Should().Be("wsHttpBinding");
        binding.Name.Should().Be("SecureBinding");
        binding.SecurityMode.Should().Be(WcfSecurityMode.Message);
        binding.ClientCredentialType.Should().Be("UserName");
    }

    [Fact]
    public void Parse_NetTcpBinding_ParsesCorrectly()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <bindings>
            <netTcpBinding>
                <binding name=""TcpBinding""
                         maxReceivedMessageSize=""10485760"">
                    <security mode=""TransportWithMessageCredential"">
                        <transport clientCredentialType=""Windows"" />
                    </security>
                </binding>
            </netTcpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Bindings.Should().HaveCount(1);

        var binding = result.Bindings[0];
        binding.BindingType.Should().Be("netTcpBinding");
        binding.Name.Should().Be("TcpBinding");
        binding.MaxReceivedMessageSize.Should().Be(10485760);
        binding.SecurityMode.Should().Be(WcfSecurityMode.TransportWithMessageCredential);
    }

    [Fact]
    public void Parse_MultipleBindingTypes_ParsesAll()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <bindings>
            <basicHttpBinding>
                <binding name=""BasicConfig"" />
            </basicHttpBinding>
            <wsHttpBinding>
                <binding name=""WsConfig"" />
            </wsHttpBinding>
            <netTcpBinding>
                <binding name=""TcpConfig"" />
            </netTcpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Bindings.Should().HaveCount(3);
        result.Bindings.Should().Contain(b => b.BindingType == "basicHttpBinding");
        result.Bindings.Should().Contain(b => b.BindingType == "wsHttpBinding");
        result.Bindings.Should().Contain(b => b.BindingType == "netTcpBinding");
    }

    [Fact]
    public void Parse_BindingWithDefaultValues_UsesDefaults()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <bindings>
            <basicHttpBinding>
                <binding name=""DefaultConfig"" />
            </basicHttpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        var binding = result!.Bindings[0];
        binding.MaxReceivedMessageSize.Should().Be(65536);
        binding.MaxBufferSize.Should().Be(65536);
        binding.ReceiveTimeout.Should().Be(TimeSpan.FromMinutes(10));
        binding.SendTimeout.Should().Be(TimeSpan.FromMinutes(1));
        binding.SecurityMode.Should().Be(WcfSecurityMode.None);
    }

    [Fact]
    public void Parse_ServiceBehavior_ParsesCorrectly()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <behaviors>
            <serviceBehaviors>
                <behavior name=""MyBehavior"">
                    <serviceMetadata httpGetEnabled=""true"" httpsGetEnabled=""true"" />
                    <serviceDebug includeExceptionDetailInFaults=""true"" />
                    <serviceThrottling maxConcurrentCalls=""100""
                                     maxConcurrentInstances=""50""
                                     maxConcurrentSessions=""200"" />
                </behavior>
            </serviceBehaviors>
        </behaviors>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Behaviors.Should().HaveCount(1);

        var behavior = result.Behaviors[0];
        behavior.Name.Should().Be("MyBehavior");
        behavior.MetadataHttpGetEnabled.Should().BeTrue();
        behavior.MetadataHttpsGetEnabled.Should().BeTrue();
        behavior.IncludeExceptionDetailInFaults.Should().BeTrue();
        behavior.MaxConcurrentCalls.Should().Be(100);
        behavior.MaxConcurrentInstances.Should().Be(50);
        behavior.MaxConcurrentSessions.Should().Be(200);
    }

    [Fact]
    public void Parse_BehaviorWithoutThrottling_ThrottlingIsNull()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <behaviors>
            <serviceBehaviors>
                <behavior name=""SimpleBehavior"">
                    <serviceMetadata httpGetEnabled=""true"" />
                </behavior>
            </serviceBehaviors>
        </behaviors>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        var behavior = result!.Behaviors[0];
        behavior.MaxConcurrentCalls.Should().BeNull();
        behavior.MaxConcurrentInstances.Should().BeNull();
        behavior.MaxConcurrentSessions.Should().BeNull();
    }

    [Fact]
    public void Parse_MultipleBehaviors_ParsesAll()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <behaviors>
            <serviceBehaviors>
                <behavior name=""Behavior1"">
                    <serviceMetadata httpGetEnabled=""true"" />
                </behavior>
                <behavior name=""Behavior2"">
                    <serviceDebug includeExceptionDetailInFaults=""true"" />
                </behavior>
            </serviceBehaviors>
        </behaviors>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Behaviors.Should().HaveCount(2);
        result.Behaviors[0].Name.Should().Be("Behavior1");
        result.Behaviors[1].Name.Should().Be("Behavior2");
    }

    [Fact]
    public void Parse_ServiceHostingEnvironment_ParsesMultipleSiteBindings()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <serviceHostingEnvironment multipleSiteBindingsEnabled=""true"" />
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.MultipleSiteBindingsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Parse_CompleteConfiguration_ParsesAllSections()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <services>
            <service name=""MyNamespace.MyService"" behaviorConfiguration=""ServiceBehavior"">
                <endpoint address="""" binding=""basicHttpBinding"" bindingConfiguration=""LargeMessages"" contract=""MyNamespace.IMyService"" />
                <endpoint address=""mex"" binding=""mexHttpBinding"" contract=""IMetadataExchange"" />
            </service>
        </services>
        <bindings>
            <basicHttpBinding>
                <binding name=""LargeMessages"" maxReceivedMessageSize=""2147483647"">
                    <security mode=""Transport"">
                        <transport clientCredentialType=""Windows"" />
                    </security>
                </binding>
            </basicHttpBinding>
        </bindings>
        <behaviors>
            <serviceBehaviors>
                <behavior name=""ServiceBehavior"">
                    <serviceMetadata httpGetEnabled=""true"" />
                    <serviceDebug includeExceptionDetailInFaults=""false"" />
                </behavior>
            </serviceBehaviors>
        </behaviors>
        <serviceHostingEnvironment multipleSiteBindingsEnabled=""true"" />
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Services.Should().HaveCount(1);
        result.Bindings.Should().HaveCount(1);
        result.Behaviors.Should().HaveCount(1);
        result.MultipleSiteBindingsEnabled.Should().BeTrue();

        // Verify service references behavior and binding
        var service = result.Services[0];
        service.BehaviorConfiguration.Should().Be("ServiceBehavior");
        service.Endpoints[0].BindingConfiguration.Should().Be("LargeMessages");
    }

    [Fact]
    public void Parse_ServiceWithoutName_AddsDiagnostic()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <services>
            <service>
                <endpoint address="""" binding=""basicHttpBinding"" contract=""MyNamespace.IMyService"" />
            </service>
        </services>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Services.Should().BeEmpty();
        _parser.Diagnostics.Should().ContainSingle()
            .Which.Should().Contain("Service definition missing 'name' attribute");
    }

    [Fact]
    public void Parse_EndpointWithoutBinding_AddsDiagnostic()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <services>
            <service name=""MyNamespace.MyService"">
                <endpoint address="""" contract=""MyNamespace.IMyService"" />
            </service>
        </services>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Services[0].Endpoints.Should().BeEmpty();
        _parser.Diagnostics.Should().ContainSingle()
            .Which.Should().Contain("Endpoint missing 'binding' attribute");
    }

    [Fact]
    public void Parse_BindingWithoutName_AddsDiagnostic()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <bindings>
            <basicHttpBinding>
                <binding maxReceivedMessageSize=""1000000"" />
            </basicHttpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Bindings.Should().BeEmpty();
        _parser.Diagnostics.Should().ContainSingle()
            .Which.Should().Contain("Binding configuration for basicHttpBinding missing 'name' attribute");
    }

    [Fact]
    public void Parse_SecurityModeVariations_ParsesCorrectly()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <bindings>
            <basicHttpBinding>
                <binding name=""None"">
                    <security mode=""None"" />
                </binding>
                <binding name=""Transport"">
                    <security mode=""Transport"" />
                </binding>
                <binding name=""Message"">
                    <security mode=""Message"" />
                </binding>
                <binding name=""TransportWithMessageCredential"">
                    <security mode=""TransportWithMessageCredential"" />
                </binding>
            </basicHttpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        result!.Bindings.Should().HaveCount(4);
        result.Bindings[0].SecurityMode.Should().Be(WcfSecurityMode.None);
        result.Bindings[1].SecurityMode.Should().Be(WcfSecurityMode.Transport);
        result.Bindings[2].SecurityMode.Should().Be(WcfSecurityMode.Message);
        result.Bindings[3].SecurityMode.Should().Be(WcfSecurityMode.TransportWithMessageCredential);
    }

    [Fact]
    public void Parse_MetadataExchangeContract_IsIdentified()
    {
        // Arrange
        var config = @"
<configuration>
    <system.serviceModel>
        <services>
            <service name=""MyNamespace.MyService"">
                <endpoint address=""mex1"" binding=""mexHttpBinding"" contract=""IMetadataExchange"" />
                <endpoint address=""mex2"" binding=""mexHttpBinding"" contract=""System.ServiceModel.Description.IMetadataExchange"" />
                <endpoint address="""" binding=""basicHttpBinding"" contract=""MyNamespace.IMyService"" />
            </service>
        </services>
    </system.serviceModel>
</configuration>";

        // Act
        var result = _parser.Parse(config);

        // Assert
        result.Should().NotBeNull();
        var endpoints = result!.Services[0].Endpoints;
        endpoints[0].IsMetadataExchange.Should().BeTrue();
        endpoints[1].IsMetadataExchange.Should().BeTrue();
        endpoints[2].IsMetadataExchange.Should().BeFalse();
    }
}
