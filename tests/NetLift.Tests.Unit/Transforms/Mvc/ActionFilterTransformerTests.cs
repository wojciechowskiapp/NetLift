using FluentAssertions;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Mvc.Rewriters;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class ActionFilterTransformerTests
{
    private readonly IActionFilterTransformer _transformer = new ActionFilterTransformer();

    [Fact]
    public void TransformsFilterBaseClass()
    {
        // Arrange
        const string input = @"
using System.Web.Mvc;

public class LogFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context) { }
}";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain("IActionFilter");
        result.Should().NotContain("ActionFilterAttribute");
        _transformer.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc.Filters");
        _transformer.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void RemovesOverrideKeyword()
    {
        // Arrange
        const string input = @"
using System.Web.Mvc;

public class LogFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Log action
    }
}";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain("public void OnActionExecuting");
        result.Should().NotContain("public override void OnActionExecuting");
        _transformer.Diagnostics.Should().Contain(d => d.Message.Contains("Removed 'override' keyword"));
    }

    [Fact]
    public void TransformsRolesToPolicy()
    {
        // Arrange
        const string input = @"
[Authorize(Roles = ""Admin"")]
public class AdminController : Controller { }";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain(@"Policy=""AdminPolicy""");
        result.Should().NotContain("Roles=");
        _transformer.GeneratedPolicies.Should().ContainSingle();
        _transformer.GeneratedPolicies.First().Name.Should().Be("AdminPolicy");
        _transformer.GeneratedPolicies.First().Roles.Should().Equal("Admin");
        _transformer.ConfidenceScore.Should().Be(90);
    }

    [Fact]
    public void HandlesCombinedRoles()
    {
        // Arrange
        const string input = @"
[Authorize(Roles = ""Admin,Manager"")]
public class SecureController : Controller { }";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain(@"Policy=""AdminManagerPolicy""");
        result.Should().NotContain("Roles=");
        _transformer.GeneratedPolicies.Should().ContainSingle();
        _transformer.GeneratedPolicies.First().Name.Should().Be("AdminManagerPolicy");
        _transformer.GeneratedPolicies.First().Roles.Should().Equal("Admin", "Manager");
    }

    [Fact]
    public void PreservesSimpleAuthorize()
    {
        // Arrange
        const string input = @"
[Authorize]
public class SecureController : Controller { }";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain("[Authorize]");
        result.Should().NotContain("Policy");
        _transformer.GeneratedPolicies.Should().BeEmpty();
        _transformer.ConfidenceScore.Should().Be(100);
    }

    [Fact]
    public void TransformsHandleError()
    {
        // Arrange
        const string input = @"
[HandleError]
public class HomeController : Controller { }";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain("TypeFilter(typeof(GlobalExceptionFilter))");
        result.Should().NotContain("[HandleError]");
        _transformer.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc");
        _transformer.ConfidenceScore.Should().Be(90);
    }

    [Fact]
    public void TracksGeneratedPolicies()
    {
        // Arrange
        const string input = @"
[Authorize(Roles = ""Admin"")]
public class AdminController : Controller { }

[Authorize(Roles = ""User,Manager"")]
public class UserController : Controller { }";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        _transformer.GeneratedPolicies.Should().HaveCount(2);
        _transformer.GeneratedPolicies.Should().Contain(p => p.Name == "AdminPolicy");
        _transformer.GeneratedPolicies.Should().Contain(p => p.Name == "UserManagerPolicy");
    }

    [Fact]
    public void PreservesMethodBody()
    {
        // Arrange
        const string input = @"
using System.Web.Mvc;

public class LogFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var actionName = context.ActionDescriptor.ActionName;
        Console.WriteLine($""Executing {actionName}"");
    }
}";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain("var actionName = context.ActionDescriptor.ActionName;");
        result.Should().Contain(@"Console.WriteLine($""Executing {actionName}"");");
    }

    [Fact]
    public void TransformsMultipleFilterMethods()
    {
        // Arrange
        const string input = @"
using System.Web.Mvc;

public class TimingFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Start timer
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        // Stop timer
    }
}";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain("public void OnActionExecuting");
        result.Should().Contain("public void OnActionExecuted");
        result.Should().NotContain("override");
        _transformer.Diagnostics.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public void HandlesEmptySourceCode()
    {
        // Arrange
        const string input = "";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().BeEmpty();
        _transformer.RequiredUsings.Should().BeEmpty();
        _transformer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void HandlesNullSourceCode()
    {
        // Arrange
        string? input = null;

        // Act
        var result = _transformer.Rewrite(input!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void HandlesAuthorizeWithOtherArguments()
    {
        // Arrange
        const string input = @"
[Authorize(Roles = ""Admin"", AuthenticationSchemes = ""Bearer"")]
public class ApiController : Controller { }";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain(@"Policy=""AdminPolicy""");
        result.Should().Contain(@"AuthenticationSchemes");
        result.Should().NotContain("Roles=");
    }

    [Fact]
    public void TransformsIAuthorizationFilterInterface()
    {
        // Arrange
        const string input = @"
using System.Web.Mvc;

public class CustomAuthFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationContext context) { }
}";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain("IAuthorizationFilter");
        _transformer.RequiredUsings.Should().Contain("Microsoft.AspNetCore.Mvc.Filters");
    }

    [Fact]
    public void TransformsIExceptionFilterInterface()
    {
        // Arrange
        const string input = @"
using System.Web.Mvc;

public class CustomExceptionFilter : IExceptionFilter
{
    public override void OnException(ExceptionContext context) { }
}";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        result.Should().Contain("IExceptionFilter");
        result.Should().Contain("public void OnException");
        result.Should().NotContain("override");
    }

    [Fact]
    public void ProducesDiagnosticsForAllTransformations()
    {
        // Arrange
        const string input = @"
using System.Web.Mvc;

[Authorize(Roles = ""Admin"")]
[HandleError]
public class SecureController : Controller
{
    public class LogFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context) { }
    }
}";

        // Act
        var result = _transformer.Rewrite(input);

        // Assert
        _transformer.Diagnostics.Should().NotBeEmpty();
        _transformer.Diagnostics.Should().Contain(d => d.Message.Contains("Rewritten filter base class"));
        _transformer.Diagnostics.Should().Contain(d => d.Message.Contains("Removed 'override' keyword"));
        _transformer.Diagnostics.Should().Contain(d => d.Message.Contains("Transformed [Authorize"));
        _transformer.Diagnostics.Should().Contain(d => d.Message.Contains("Transformed [HandleError]"));
    }
}
