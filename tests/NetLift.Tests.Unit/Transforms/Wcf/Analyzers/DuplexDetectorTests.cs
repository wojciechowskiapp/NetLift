using FluentAssertions;
using NetLift.Core.Models.Wcf;
using NetLift.Transforms.Wcf.Analyzers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Wcf.Analyzers;

public class DuplexDetectorTests
{
    private readonly DuplexDetector _detector = new();

    [Fact]
    public void Detect_WithNoDuplexContracts_ReturnsEmptyWarnings()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "IProductService",
                FullyQualifiedName = "MyApp.Services.IProductService",
                Operations =
                [
                    new WcfOperation
                    {
                        Name = "GetProduct",
                        ReturnType = "Product",
                        Parameters = []
                    }
                ]
            }
        };

        var config = new WcfServiceConfiguration
        {
            Services = [],
            Bindings = []
        };

        // Act
        var report = _detector.Detect(contracts, config);

        // Assert
        report.Should().NotBeNull();
        report.HasDuplexContracts.Should().BeFalse();
        report.Warnings.Should().BeEmpty();
        report.MigrationGuidanceMarkdown.Should().Contain("No duplex contracts detected");
    }

    [Fact]
    public void Detect_WithCallbackContract_ReturnsHighSeverityWarning()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "INotificationService",
                FullyQualifiedName = "MyApp.Services.INotificationService",
                CallbackContract = "INotificationCallback",
                Operations =
                [
                    new WcfOperation
                    {
                        Name = "Subscribe",
                        ReturnType = "void",
                        Parameters = []
                    }
                ]
            }
        };

        // Act
        var report = _detector.Detect(contracts, null);

        // Assert
        report.Should().NotBeNull();
        report.HasDuplexContracts.Should().BeTrue();
        report.Warnings.Should().HaveCount(1);

        var warning = report.Warnings[0];
        warning.ServiceName.Should().Be("INotificationService");
        warning.CallbackContractName.Should().Be("INotificationCallback");
        warning.Severity.Should().Be(DuplexWarningSeverity.High);
    }

    [Fact]
    public void Detect_WithMultipleDuplexContracts_ReturnsMultipleWarnings()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "INotificationService",
                FullyQualifiedName = "MyApp.Services.INotificationService",
                CallbackContract = "INotificationCallback"
            },
            new()
            {
                InterfaceName = "IChatService",
                FullyQualifiedName = "MyApp.Services.IChatService",
                CallbackContract = "IChatCallback"
            },
            new()
            {
                InterfaceName = "IProductService",
                FullyQualifiedName = "MyApp.Services.IProductService"
            }
        };

        // Act
        var report = _detector.Detect(contracts, null);

        // Assert
        report.HasDuplexContracts.Should().BeTrue();
        report.Warnings.Should().HaveCount(2);
        report.Warnings.Should().Contain(w => w.ServiceName == "INotificationService");
        report.Warnings.Should().Contain(w => w.ServiceName == "IChatService");
    }

    [Fact]
    public void Detect_WithWsDualHttpBinding_DetectsDuplexBinding()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>();

        var config = new WcfServiceConfiguration
        {
            Services =
            [
                new WcfServiceDefinition
                {
                    ServiceName = "MyApp.Services.NotificationService",
                    Endpoints =
                    [
                        new WcfEndpoint
                        {
                            Address = "",
                            Binding = "wsDualHttpBinding",
                            Contract = "MyApp.Services.INotificationService"
                        }
                    ]
                }
            ],
            Bindings =
            [
                new WcfBindingConfig
                {
                    BindingType = "wsDualHttpBinding",
                    Name = "duplexBinding"
                }
            ]
        };

        // Act
        var report = _detector.Detect(contracts, config);

        // Assert
        report.HasDuplexContracts.Should().BeTrue();
        report.Warnings.Should().HaveCount(1);

        var warning = report.Warnings[0];
        warning.Severity.Should().Be(DuplexWarningSeverity.Medium);
        warning.DuplexBindings.Should().Contain("wsDualHttpBinding");
    }

    [Fact]
    public void Detect_WithNetTcpBinding_DetectsDuplexBinding()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>();

        var config = new WcfServiceConfiguration
        {
            Services =
            [
                new WcfServiceDefinition
                {
                    ServiceName = "MyApp.Services.ChatService",
                    Endpoints =
                    [
                        new WcfEndpoint
                        {
                            Address = "net.tcp://localhost:8080/chat",
                            Binding = "netTcpBinding",
                            Contract = "MyApp.Services.IChatService"
                        }
                    ]
                }
            ],
            Bindings =
            [
                new WcfBindingConfig
                {
                    BindingType = "netTcpBinding",
                    Name = "tcpBinding"
                }
            ]
        };

        // Act
        var report = _detector.Detect(contracts, config);

        // Assert
        report.HasDuplexContracts.Should().BeTrue();
        report.Warnings.Should().HaveCount(1);
        report.Warnings[0].DuplexBindings.Should().Contain("netTcpBinding");
    }

    [Fact]
    public void Detect_WithCallbackContractAndDuplexBinding_CombinesInformation()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "INotificationService",
                FullyQualifiedName = "MyApp.Services.INotificationService",
                CallbackContract = "INotificationCallback"
            }
        };

        var config = new WcfServiceConfiguration
        {
            Services =
            [
                new WcfServiceDefinition
                {
                    ServiceName = "MyApp.Services.NotificationService",
                    Endpoints =
                    [
                        new WcfEndpoint
                        {
                            Address = "",
                            Binding = "wsDualHttpBinding",
                            Contract = "MyApp.Services.INotificationService"
                        }
                    ]
                }
            ],
            Bindings =
            [
                new WcfBindingConfig
                {
                    BindingType = "wsDualHttpBinding",
                    Name = "duplexBinding"
                }
            ]
        };

        // Act
        var report = _detector.Detect(contracts, config);

        // Assert
        report.HasDuplexContracts.Should().BeTrue();
        report.Warnings.Should().HaveCount(1);

        var warning = report.Warnings[0];
        warning.ServiceName.Should().Be("INotificationService");
        warning.CallbackContractName.Should().Be("INotificationCallback");
        warning.Severity.Should().Be(DuplexWarningSeverity.High);
        warning.DuplexBindings.Should().Contain("wsDualHttpBinding");
    }

    [Fact]
    public void Detect_WithBasicHttpBinding_DoesNotDetectDuplex()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "IProductService",
                FullyQualifiedName = "MyApp.Services.IProductService"
            }
        };

        var config = new WcfServiceConfiguration
        {
            Services =
            [
                new WcfServiceDefinition
                {
                    ServiceName = "MyApp.Services.ProductService",
                    Endpoints =
                    [
                        new WcfEndpoint
                        {
                            Address = "",
                            Binding = "basicHttpBinding",
                            Contract = "MyApp.Services.IProductService"
                        }
                    ]
                }
            ],
            Bindings =
            [
                new WcfBindingConfig
                {
                    BindingType = "basicHttpBinding",
                    Name = "basicBinding"
                }
            ]
        };

        // Act
        var report = _detector.Detect(contracts, config);

        // Assert
        report.HasDuplexContracts.Should().BeFalse();
        report.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Detect_GeneratesMigrationGuidance()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "INotificationService",
                FullyQualifiedName = "MyApp.Services.INotificationService",
                CallbackContract = "INotificationCallback"
            }
        };

        // Act
        var report = _detector.Detect(contracts, null);

        // Assert
        report.MigrationGuidanceMarkdown.Should().NotBeEmpty();
        report.MigrationGuidanceMarkdown.Should().Contain("# WCF Duplex Contract Migration Guidance");
        report.MigrationGuidanceMarkdown.Should().Contain("SignalR");
        report.MigrationGuidanceMarkdown.Should().Contain("gRPC Server Streaming");
        report.MigrationGuidanceMarkdown.Should().Contain("WebSockets");
        report.MigrationGuidanceMarkdown.Should().Contain("INotificationService");
        report.MigrationGuidanceMarkdown.Should().Contain("INotificationCallback");
    }

    [Fact]
    public void Detect_WithNullConfig_StillDetectsCallbackContracts()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "IChatService",
                FullyQualifiedName = "MyApp.Services.IChatService",
                CallbackContract = "IChatCallback"
            }
        };

        // Act
        var report = _detector.Detect(contracts, null);

        // Assert
        report.HasDuplexContracts.Should().BeTrue();
        report.Warnings.Should().HaveCount(1);
        report.Warnings[0].DuplexBindings.Should().BeEmpty();
    }

    [Fact]
    public void Detect_PopulatesDiagnostics()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "INotificationService",
                FullyQualifiedName = "MyApp.Services.INotificationService",
                CallbackContract = "INotificationCallback"
            }
        };

        // Act
        var report = _detector.Detect(contracts, null);

        // Assert
        _detector.Diagnostics.Should().NotBeEmpty();
        _detector.Diagnostics.Should().Contain(d =>
            d.Contains("Detected duplex contract") &&
            d.Contains("INotificationService"));
    }

    [Fact]
    public void ParseCallbackMethods_WithValidInterface_ExtractsCallbackMethods()
    {
        // Arrange
        var callbackSource = @"
using System.ServiceModel;

namespace MyApp.Services
{
    public interface INotificationCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnNotificationReceived(string message);

        [OperationContract]
        string OnStatusUpdate(int statusCode);
    }
}";

        // Act
        var methods = _detector.ParseCallbackMethods(callbackSource);

        // Assert
        methods.Should().HaveCount(2);

        var oneWayMethod = methods.Should().ContainSingle(m => m.Name == "OnNotificationReceived").Subject;
        oneWayMethod.IsOneWay.Should().BeTrue();
        oneWayMethod.Parameters.Should().HaveCount(1);
        oneWayMethod.Parameters[0].Name.Should().Be("message");
        oneWayMethod.Parameters[0].Type.Should().Be("string");

        var twoWayMethod = methods.Should().ContainSingle(m => m.Name == "OnStatusUpdate").Subject;
        twoWayMethod.IsOneWay.Should().BeFalse();
        twoWayMethod.Parameters.Should().HaveCount(1);
        twoWayMethod.Parameters[0].Name.Should().Be("statusCode");
        twoWayMethod.Parameters[0].Type.Should().Be("int");
    }

    [Fact]
    public void ParseCallbackMethods_WithInvalidSource_ReturnsEmptyListAndDiagnostics()
    {
        // Arrange
        var invalidSource = "this is not valid C# code { } [ ]";

        // Act
        var methods = _detector.ParseCallbackMethods(invalidSource);

        // Assert
        methods.Should().BeEmpty();
    }

    [Fact]
    public void ParseCallbackMethods_WithNoMethods_ReturnsEmptyList()
    {
        // Arrange
        var emptyInterface = @"
using System.ServiceModel;

namespace MyApp.Services
{
    public interface IEmptyCallback
    {
    }
}";

        // Act
        var methods = _detector.ParseCallbackMethods(emptyInterface);

        // Assert
        methods.Should().BeEmpty();
    }

    [Fact]
    public void Detect_MigrationGuidance_IncludesComparisonMatrix()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "IChatService",
                FullyQualifiedName = "MyApp.Services.IChatService",
                CallbackContract = "IChatCallback"
            }
        };

        // Act
        var report = _detector.Detect(contracts, null);

        // Assert
        report.MigrationGuidanceMarkdown.Should().Contain("Comparison Matrix");
        report.MigrationGuidanceMarkdown.Should().Contain("Browser Support");
        report.MigrationGuidanceMarkdown.Should().Contain("Performance");
        report.MigrationGuidanceMarkdown.Should().Contain("Type Safety");
    }

    [Fact]
    public void Detect_MigrationGuidance_IncludesCodeExamples()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "INotificationService",
                FullyQualifiedName = "MyApp.Services.INotificationService",
                CallbackContract = "INotificationCallback"
            }
        };

        // Act
        var report = _detector.Detect(contracts, null);

        // Assert
        report.MigrationGuidanceMarkdown.Should().Contain("```csharp");
        report.MigrationGuidanceMarkdown.Should().Contain("public class NotificationHub : Hub");
        report.MigrationGuidanceMarkdown.Should().Contain("IServerStreamWriter");
        report.MigrationGuidanceMarkdown.Should().Contain("WebSockets");
    }

    [Fact]
    public void Detect_MigrationGuidance_IncludesMigrationChecklist()
    {
        // Arrange
        var contracts = new List<WcfServiceContract>
        {
            new()
            {
                InterfaceName = "IChatService",
                FullyQualifiedName = "MyApp.Services.IChatService",
                CallbackContract = "IChatCallback"
            }
        };

        // Act
        var report = _detector.Detect(contracts, null);

        // Assert
        report.MigrationGuidanceMarkdown.Should().Contain("Migration Checklist");
        report.MigrationGuidanceMarkdown.Should().Contain("[ ] Identify the callback methods");
        report.MigrationGuidanceMarkdown.Should().Contain("[ ] Determine the client types");
        report.MigrationGuidanceMarkdown.Should().Contain("[ ] Choose the appropriate migration option");
    }
}
