using FluentAssertions;
using NetLift.Transforms.SignalR.Analyzers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.SignalR;

public class GlobalHostAnalyzerTests
{
    private readonly GlobalHostAnalyzer _analyzer = new();

    [Fact]
    public void AnalyzeFile_WithGetHubContext_DetectsUsage()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class NotificationService
{
    public void SendNotification(string message)
    {
        var context = GlobalHost.ConnectionManager.GetHubContext<NotificationHub>();
        context.Clients.All.notify(message);
    }
}";

        var info = _analyzer.AnalyzeFile(source, "NotificationService.cs");

        info.Should().NotBeNull();
        info!.ClassName.Should().Be("NotificationService");
        info.Usages.Should().NotBeEmpty();
        info.ReferencedHubTypes.Should().Contain("NotificationHub");
    }

    [Fact]
    public void AnalyzeFile_WithMultipleHubTypes_DetectsAll()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class BroadcastService
{
    public void BroadcastChat(string message)
    {
        var chat = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
        chat.Clients.All.receive(message);
    }

    public void BroadcastNotification(string message)
    {
        var notif = GlobalHost.ConnectionManager.GetHubContext<NotificationHub>();
        notif.Clients.All.notify(message);
    }
}";

        var info = _analyzer.AnalyzeFile(source, "BroadcastService.cs");

        info.Should().NotBeNull();
        info!.ReferencedHubTypes.Should().HaveCount(2);
        info.ReferencedHubTypes.Should().Contain("ChatHub", "NotificationHub");
    }

    [Fact]
    public void AnalyzeFile_WithNoGlobalHost_ReturnsNull()
    {
        var source = @"
public class RegularService
{
    public void DoSomething() { }
}";

        var info = _analyzer.AnalyzeFile(source, "RegularService.cs");

        info.Should().BeNull();
    }

    [Fact]
    public void AnalyzeFile_GeneratesSuggestedTransformation()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class NotificationService
{
    public void Send(string message)
    {
        var context = GlobalHost.ConnectionManager.GetHubContext<NotificationHub>();
        context.Clients.All.notify(message);
    }
}";

        var info = _analyzer.AnalyzeFile(source, "NotificationService.cs");

        info.Should().NotBeNull();
        var usage = info!.Usages.FirstOrDefault();
        usage.Should().NotBeNull();
        usage!.SuggestedTransformation.Should().Contain("IHubContext<NotificationHub>");
        usage.SuggestedTransformation.Should().Contain("constructor");
    }

    [Fact]
    public void AnalyzeFile_IdentifiesPattern()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class Service
{
    public void Method()
    {
        var context = GlobalHost.ConnectionManager.GetHubContext<MyHub>();
    }
}";

        var info = _analyzer.AnalyzeFile(source, "Service.cs");

        info.Should().NotBeNull();
        info!.Usages[0].Pattern.Should().Contain("GetHubContext");
    }

    [Fact]
    public void ContainsGlobalHost_WithGlobalHost_ReturnsTrue()
    {
        var source = "var context = GlobalHost.ConnectionManager.GetHubContext<MyHub>();";

        _analyzer.ContainsGlobalHost(source).Should().BeTrue();
    }

    [Fact]
    public void ContainsGlobalHost_WithoutGlobalHost_ReturnsFalse()
    {
        var source = "var context = _hubContext;";

        _analyzer.ContainsGlobalHost(source).Should().BeFalse();
    }

    [Fact]
    public void AnalyzeFile_WithEmptySource_ReturnsNull()
    {
        var info = _analyzer.AnalyzeFile("", "empty.cs");

        info.Should().BeNull();
    }

    [Fact]
    public void AnalyzeFile_CalculatesConfidenceScore()
    {
        var source = @"
using Microsoft.AspNet.SignalR;

public class Service
{
    public void Method()
    {
        var context = GlobalHost.ConnectionManager.GetHubContext<MyHub>();
    }
}";

        var info = _analyzer.AnalyzeFile(source, "Service.cs");

        info.Should().NotBeNull();
        info!.Confidence.Should().BeGreaterOrEqualTo(50);
        info.Confidence.Should().BeLessOrEqualTo(100);
    }
}
