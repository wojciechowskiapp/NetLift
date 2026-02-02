using FluentAssertions;
using NetLift.Core.Models.Modernization;
using NetLift.Transforms.Modernization.Generators;
using System.Reflection;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Modernization.Generators;

/// <summary>
/// Tests for private helper methods in BusinessLogicBuilder using reflection.
/// </summary>
public sealed class BusinessLogicBuilderHelpersTests
{
    private readonly BusinessLogicBuilder _builder = new();
    private readonly Type _builderType = typeof(BusinessLogicBuilder);

    [Fact]
    public void ExtractMethodBodyWithoutSignature_SwitchExpression_ExtractsCorrectly()
    {
        // Arrange
        var methodBody = @"private string GetImageMimeType(string extension)
{
    return extension switch
    {
        "".jpg"" => ""image/jpeg"",
        "".png"" => ""image/png"",
        _ => ""application/octet-stream""
    };
}";

        var method = _builderType.GetMethod("ExtractMethodBodyWithoutSignature",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (string)method!.Invoke(null, new object[] { methodBody })!;

        // Assert
        result.Should().Contain("extension switch");
        result.Should().Contain(".jpg");
        result.Should().NotContain("private string");
        result.Should().NotContain("return ");
    }

    [Fact]
    public void ExtractMethodBodyWithoutSignature_SimpleReturn_ExtractsExpression()
    {
        // Arrange
        var methodBody = @"private decimal Calculate(decimal price)
{
    return price * 0.1m;
}";

        var method = _builderType.GetMethod("ExtractMethodBodyWithoutSignature",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (string)method!.Invoke(null, new object[] { methodBody })!;

        // Assert
        result.Should().Be("price * 0.1m");
    }

    [Fact]
    public void SubstituteParameters_SingleParameter_Substitutes()
    {
        // Arrange
        var body = "extension switch { \".jpg\" => \"image/jpeg\", _ => \"other\" }";
        var arguments = "request.Extension";
        var parameters = new List<ActionParameter>
        {
            new ActionParameter { Name = "extension", Type = "string" }
        };

        var method = _builderType.GetMethod("SubstituteParameters",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (string)method!.Invoke(null, new object[] { body, arguments, parameters })!;

        // Assert
        result.Should().Contain("request.Extension switch");
        result.Should().NotContain("extension switch");
    }

    [Fact]
    public void ParseArguments_SingleArgument_Parses()
    {
        // Arrange
        var arguments = "request.Extension";

        var method = _builderType.GetMethod("ParseArguments",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (List<string>)method!.Invoke(null, new object[] { arguments })!;

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be("request.Extension");
    }

    [Fact]
    public void ParseArguments_MultipleArguments_Parses()
    {
        // Arrange
        var arguments = "firstName, lastName";

        var method = _builderType.GetMethod("ParseArguments",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (List<string>)method!.Invoke(null, new object[] { arguments })!;

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be("firstName");
        result[1].Should().Be("lastName");
    }
}
