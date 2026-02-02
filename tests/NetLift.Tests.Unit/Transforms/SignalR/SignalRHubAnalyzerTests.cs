using FluentAssertions;
using NetLift.Core.Models.SignalR;
using NetLift.Transforms.SignalR.Analyzers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.SignalR;

public class SignalRHubAnalyzerTests
{
    private readonly SignalRHubAnalyzer _analyzer = new();

    #region Hub Detection Tests

    [Fact]
    public void AnalyzeFile_WithSimpleHub_DetectsHub()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

namespace MyApp.Hubs
{
    public class ChatHub : Hub
    {
        public void Send(string message)
        {
            Clients.All.broadcastMessage(message);
        }
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].ClassName.Should().Be("ChatHub");
        hubs[0].Namespace.Should().Be("MyApp.Hubs");
    }

    [Fact]
    public void AnalyzeFile_WithGenericHub_DetectsHub()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public interface IChatClient
{
    void ReceiveMessage(string message);
}

public class ChatHub : Hub<IChatClient>
{
    public void Send(string message)
    {
        Clients.All.ReceiveMessage(message);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].ClassName.Should().Be("ChatHub");
    }

    [Fact]
    public void AnalyzeFile_WithNoHub_ReturnsEmpty()
    {
        var source = @"
namespace MyApp
{
    public class RegularClass
    {
        public void DoSomething() { }
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "Regular.cs");

        hubs.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeFile_WithMultipleHubs_DetectsAll()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub { }
public class NotificationHub : Hub { }
public class StatusHub : Hub { }
";

        var hubs = _analyzer.AnalyzeFile(source, "Hubs.cs");

        hubs.Should().HaveCount(3);
        hubs.Select(h => h.ClassName).Should().Contain("ChatHub", "NotificationHub", "StatusHub");
    }

    #endregion

    #region Lifecycle Method Detection Tests

    [Fact]
    public void AnalyzeFile_WithOnConnected_DetectsLifecycleMethod()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public override void OnConnected()
    {
        base.OnConnected();
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].LifecycleMethods.Should().HaveCount(1);
        hubs[0].LifecycleMethods[0].MethodName.Should().Be("OnConnected");
        hubs[0].LifecycleMethods[0].CanAutoTransform.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeFile_WithOnDisconnected_DetectsLifecycleMethod()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public override void OnDisconnected(bool stopCalled)
    {
        base.OnDisconnected(stopCalled);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].LifecycleMethods.Should().HaveCount(1);
        hubs[0].LifecycleMethods[0].MethodName.Should().Be("OnDisconnected");
        hubs[0].LifecycleMethods[0].TransformationNote.Should().Contain("Parameter");
    }

    [Fact]
    public void AnalyzeFile_WithOnReconnected_MarksCantAutoTransform()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public override void OnReconnected()
    {
        // Reconnection logic
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].LifecycleMethods.Should().HaveCount(1);
        hubs[0].LifecycleMethods[0].MethodName.Should().Be("OnReconnected");
        hubs[0].LifecycleMethods[0].CanAutoTransform.Should().BeFalse();
        hubs[0].LifecycleMethods[0].TransformationNote.Should().Contain("does not exist");
    }

    [Fact]
    public void AnalyzeFile_WithAllLifecycleMethods_DetectsAll()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public override void OnConnected() { }
    public override void OnDisconnected(bool stopCalled) { }
    public override void OnReconnected() { }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].LifecycleMethods.Should().HaveCount(3);
    }

    #endregion

    #region Client Invocation Detection Tests

    [Fact]
    public void AnalyzeFile_WithClientsAll_DetectsInvocation()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public void Send(string message)
    {
        Clients.All.broadcastMessage(message);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].ClientInvocations.Should().NotBeEmpty();
        hubs[0].ClientInvocations.Should().Contain(c => c.Pattern == "Clients.All");
    }

    [Fact]
    public void AnalyzeFile_WithClientsCaller_DetectsInvocation()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public void Echo(string message)
    {
        Clients.Caller.receiveMessage(message);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].ClientInvocations.Should().Contain(c => c.Pattern == "Clients.Caller");
    }

    [Fact]
    public void AnalyzeFile_WithClientsOthers_DetectsInvocation()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public void Broadcast(string message)
    {
        Clients.Others.receiveMessage(message);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].ClientInvocations.Should().Contain(c => c.Pattern == "Clients.Others");
    }

    [Fact]
    public void AnalyzeFile_TransformsClientInvocationCorrectly()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public void Send(string user, string message)
    {
        Clients.All.broadcastMessage(user, message);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        var invocation = hubs[0].ClientInvocations.FirstOrDefault(c => c.MethodName == "broadcastMessage");
        invocation.Should().NotBeNull();
        invocation!.TransformedCode.Should().Contain("SendAsync");
        invocation.TransformedCode.Should().Contain("\"broadcastMessage\"");
    }

    #endregion

    #region Groups Operation Detection Tests

    [Fact]
    public void AnalyzeFile_WithGroupsAdd_DetectsOperation()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public void JoinRoom(string roomName)
    {
        Groups.Add(Context.ConnectionId, roomName);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].GroupsOperations.Should().HaveCount(1);
        hubs[0].GroupsOperations[0].OperationType.Should().Be(GroupsOperationType.Add);
    }

    [Fact]
    public void AnalyzeFile_WithGroupsRemove_DetectsOperation()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public void LeaveRoom(string roomName)
    {
        Groups.Remove(Context.ConnectionId, roomName);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].GroupsOperations.Should().HaveCount(1);
        hubs[0].GroupsOperations[0].OperationType.Should().Be(GroupsOperationType.Remove);
    }

    #endregion

    #region Hub Method Detection Tests

    [Fact]
    public void AnalyzeFile_WithPublicMethods_DetectsHubMethods()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public void Send(string message) { }
    public void Join(string room) { }
    private void PrivateMethod() { }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].HubMethods.Should().HaveCount(2);
        hubs[0].HubMethods.Select(m => m.Name).Should().Contain("Send", "Join");
        hubs[0].HubMethods.Select(m => m.Name).Should().NotContain("PrivateMethod");
    }

    [Fact]
    public void AnalyzeFile_WithAsyncMethod_DetectsAsync()
    {
        var source = @"
using Microsoft.AspNet.SignalR;
using System.Threading.Tasks;

public class ChatHub : Hub
{
    public async Task SendAsync(string message)
    {
        await Task.Delay(100);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].HubMethods.Should().HaveCount(1);
        hubs[0].HubMethods[0].IsAsync.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeFile_WithParameters_DetectsParameters()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public void Send(string user, string message, int priority)
    {
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].HubMethods.Should().HaveCount(1);
        hubs[0].HubMethods[0].Parameters.Should().HaveCount(3);
    }

    #endregion

    #region Hub Route Detection Tests

    [Fact]
    public void AnalyzeFile_WithHubNameAttribute_ExtractsRoute()
    {
        var source = @"
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

[HubName(""chat"")]
public class ChatHub : Hub
{
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].HubRoute.Should().Be("chat");
    }

    [Fact]
    public void AnalyzeFile_WithoutHubNameAttribute_GeneratesDefaultRoute()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class NotificationHub : Hub
{
}";

        var hubs = _analyzer.AnalyzeFile(source, "NotificationHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].HubRoute.Should().Contain("notification");
    }

    #endregion

    #region Authorization Detection Tests

    [Fact]
    public void AnalyzeFile_WithAuthorizeAttribute_DetectsAuthorization()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

[Authorize]
public class SecureHub : Hub
{
}";

        var hubs = _analyzer.AnalyzeFile(source, "SecureHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].HasCustomAuthorization.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeFile_WithMethodAuthorize_DetectsAuthorization()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    [Authorize(Roles = ""Admin"")]
    public void AdminAction()
    {
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].HasCustomAuthorization.Should().BeTrue();
    }

    #endregion

    #region Confidence Score Tests

    [Fact]
    public void AnalyzeFile_SimpleHub_HasHighConfidence()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class SimpleHub : Hub
{
    public void Send(string message)
    {
        Clients.All.receive(message);
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "SimpleHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].Confidence.Should().BeGreaterOrEqualTo(90);
    }

    [Fact]
    public void AnalyzeFile_HubWithOnReconnected_HasLowerConfidence()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class ChatHub : Hub
{
    public override void OnReconnected()
    {
        // Complex reconnection logic
    }
}";

        var hubs = _analyzer.AnalyzeFile(source, "ChatHub.cs");

        hubs.Should().HaveCount(1);
        hubs[0].Confidence.Should().BeLessThan(90);
    }

    #endregion

    #region ContainsSignalRHub Tests

    [Fact]
    public void ContainsSignalRHub_WithHubInheritance_ReturnsTrue()
    {
        var source = "public class MyHub : Hub { }";

        _analyzer.ContainsSignalRHub(source).Should().BeTrue();
    }

    [Fact]
    public void ContainsSignalRHub_WithSignalRUsing_ReturnsTrue()
    {
        var source = "using Microsoft.AspNet.SignalR;";

        _analyzer.ContainsSignalRHub(source).Should().BeTrue();
    }

    [Fact]
    public void ContainsSignalRHub_WithHubNameAttribute_ReturnsTrue()
    {
        var source = "[HubName(\"test\")]";

        _analyzer.ContainsSignalRHub(source).Should().BeTrue();
    }

    [Fact]
    public void ContainsSignalRHub_WithNoSignalR_ReturnsFalse()
    {
        var source = "public class MyClass { }";

        _analyzer.ContainsSignalRHub(source).Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AnalyzeFile_WithEmptySource_ReturnsEmpty()
    {
        var hubs = _analyzer.AnalyzeFile("", "empty.cs");

        hubs.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeFile_WithNullSource_ReturnsEmpty()
    {
        var hubs = _analyzer.AnalyzeFile(null!, "null.cs");

        hubs.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeFile_WithInvalidSyntax_ReturnsEmpty()
    {
        var source = "this is not valid C# code {{{{";

        var hubs = _analyzer.AnalyzeFile(source, "invalid.cs");

        hubs.Should().BeEmpty();
    }

    #endregion
}
