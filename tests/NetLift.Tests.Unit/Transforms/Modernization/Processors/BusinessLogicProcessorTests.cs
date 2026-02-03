using NetLift.Transforms.Modernization.Processors;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Modernization.Processors;

/// <summary>
/// Tests for BusinessLogicProcessor async/await detection using Roslyn.
/// </summary>
public sealed class BusinessLogicProcessorTests
{
    [Fact]
    public void Process_AsyncMethodCall_AddsAwait()
    {
        // Arrange
        var code = "var result = dbContext.SaveChangesAsync();";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: true);

        // Assert
        Assert.Contains("await", processed.Code);
        Assert.Contains("dbContext.SaveChangesAsync()", processed.Code);
    }

    [Fact]
    public void Process_AsyncMethodInLambda_AddsAwait()
    {
        // Arrange
        var code = "var items = list.Select(x => repository.GetByIdAsync(x.Id));";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: true);

        // Assert
        Assert.Contains("await", processed.Code);
    }

    [Fact]
    public void Process_AsyncMethodInConditional_AddsAwait()
    {
        // Arrange
        var code = "var value = condition ? service.GetAsync() : service.GetDefaultAsync();";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: true);

        // Assert
        // Should add await to both async calls in the ternary
        Assert.Contains("await", processed.Code);
    }

    [Fact]
    public void Process_AsyncMethodInChain_AddsAwait()
    {
        // Arrange
        var code = "var result = query.Where(x => x.IsActive).ToListAsync();";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: true);

        // Assert
        Assert.Contains("await", processed.Code);
        Assert.Contains("ToListAsync()", processed.Code);
    }

    [Fact]
    public void Process_AlreadyAwaitedMethod_DoesNotDuplicateAwait()
    {
        // Arrange
        var code = "var result = await dbContext.SaveChangesAsync();";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: true);

        // Assert
        // Should not have multiple awaits
        var awaitCount = processed.Code.Split("await").Length - 1;
        Assert.Equal(1, awaitCount);
    }

    [Fact]
    public void Process_NonAsyncHandler_DoesNotAddAwait()
    {
        // Arrange
        var code = "var result = dbContext.SaveChangesAsync();";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: false);

        // Assert
        Assert.DoesNotContain("await", processed.Code);
    }

    [Fact]
    public void Process_ViewDataTransform_ReplacesWithResult()
    {
        // Arrange
        var code = "ViewData[\"Message\"] = \"Hello\";";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: false);

        // Assert
        Assert.Contains("result.Message", processed.Code);
        Assert.DoesNotContain("ViewData", processed.Code);
    }

    [Fact]
    public void Process_ViewBagTransform_ReplacesWithResult()
    {
        // Arrange
        var code = "ViewBag.Title = \"Home\";";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: false);

        // Assert
        Assert.Contains("result.Title", processed.Code);
        Assert.DoesNotContain("ViewBag", processed.Code);
    }

    [Fact]
    public void Process_HttpContextAccess_TransformsToAccessor()
    {
        // Arrange
        var code = "var user = HttpContext.User;";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: false);

        // Assert
        Assert.Contains("_httpContextAccessor.HttpContext?.User", processed.Code);
        Assert.DoesNotContain("HttpContext.User", processed.Code);
    }

    [Fact]
    public void Process_DetectsHttpContextAccessorDependency()
    {
        // Arrange
        var code = "var user = HttpContext.User;";

        // Act
        var processed = BusinessLogicProcessor.Process(code, isAsync: false);

        // Assert
        Assert.Contains(processed.RequiredDependencies,
            dep => dep.InterfaceType == "IHttpContextAccessor");
    }
}
