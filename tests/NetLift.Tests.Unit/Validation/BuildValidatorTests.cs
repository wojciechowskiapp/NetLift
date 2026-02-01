namespace NetLift.Tests.Unit.Validation;

using NetLift.Core.Models;
using NetLift.Validation;
using Xunit;
using FluentAssertions;

/// <summary>
/// Unit tests for <see cref="BuildValidator"/>.
/// </summary>
public class BuildValidatorTests
{
    [Fact]
    public void ParseDiagnostics_WithNullInput_ShouldReturnEmptyList()
    {
        // Act
        var result = BuildValidator.ParseDiagnostics(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseDiagnostics_WithEmptyInput_ShouldReturnEmptyList()
    {
        // Act
        var result = BuildValidator.ParseDiagnostics("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseDiagnostics_WithWhitespaceInput_ShouldReturnEmptyList()
    {
        // Act
        var result = BuildValidator.ParseDiagnostics("   \n   \t   ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseDiagnostics_WithSingleError_ShouldParseCorrectly()
    {
        // Arrange
        var output = "Program.cs(10,15): error CS0103: The name 'Foo' does not exist in the current context";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(1);
        result[0].File.Should().Be("Program.cs");
        result[0].Line.Should().Be(10);
        result[0].Column.Should().Be(15);
        result[0].Severity.Should().Be(DiagnosticSeverity.Error);
        result[0].Code.Should().Be("CS0103");
        result[0].Message.Should().Be("The name 'Foo' does not exist in the current context");
    }

    [Fact]
    public void ParseDiagnostics_WithSingleWarning_ShouldParseCorrectly()
    {
        // Arrange
        var output = "Controller.cs(25,20): warning CS0168: The variable 'result' is declared but never used";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(1);
        result[0].File.Should().Be("Controller.cs");
        result[0].Line.Should().Be(25);
        result[0].Column.Should().Be(20);
        result[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        result[0].Code.Should().Be("CS0168");
        result[0].Message.Should().Be("The variable 'result' is declared but never used");
    }

    [Fact]
    public void ParseDiagnostics_WithMultipleDiagnostics_ShouldParseAll()
    {
        // Arrange
        var output = @"
Program.cs(10,15): error CS0103: The name 'Foo' does not exist in the current context
Controller.cs(25,20): warning CS0168: The variable 'result' is declared but never used
Model.cs(5,10): error CS0246: The type or namespace name 'Bar' could not be found
";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(3);
        result[0].Code.Should().Be("CS0103");
        result[0].Severity.Should().Be(DiagnosticSeverity.Error);
        result[1].Code.Should().Be("CS0168");
        result[1].Severity.Should().Be(DiagnosticSeverity.Warning);
        result[2].Code.Should().Be("CS0246");
        result[2].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void ParseDiagnostics_WithMsBuildErrors_ShouldParseCorrectly()
    {
        // Arrange
        var output = "CSC(0,0): error MSB3644: The reference assemblies for .NETFramework,Version=v4.7.2 were not found";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(1);
        result[0].File.Should().Be("CSC");
        result[0].Line.Should().Be(0);
        result[0].Column.Should().Be(0);
        result[0].Severity.Should().Be(DiagnosticSeverity.Error);
        result[0].Code.Should().Be("MSB3644");
        result[0].Message.Should().Be("The reference assemblies for .NETFramework,Version=v4.7.2 were not found");
    }

    [Fact]
    public void ParseDiagnostics_WithFullPathFile_ShouldParseCorrectly()
    {
        // Arrange
        var output = @"C:\Projects\MyApp\Program.cs(15,20): error CS0103: The name 'Test' does not exist in the current context";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(1);
        result[0].File.Should().Be(@"C:\Projects\MyApp\Program.cs");
        result[0].Line.Should().Be(15);
        result[0].Column.Should().Be(20);
    }

    [Fact]
    public void ParseDiagnostics_WithMixedCaseErrorWarning_ShouldParseCorrectly()
    {
        // Arrange
        var output = @"
Program.cs(10,15): ERROR CS0103: Test error
Controller.cs(25,20): WARNING CS0168: Test warning
";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(2);
        result[0].Severity.Should().Be(DiagnosticSeverity.Error);
        result[1].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ParseDiagnostics_WithNonMatchingLines_ShouldIgnoreThem()
    {
        // Arrange
        var output = @"
Build started...
Program.cs(10,15): error CS0103: The name 'Foo' does not exist
Build succeeded
Some random text
Controller.cs(25,20): warning CS0168: Variable not used
Build FAILED.
";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(2);
        result[0].Code.Should().Be("CS0103");
        result[1].Code.Should().Be("CS0168");
    }

    [Fact]
    public void ParseDiagnostics_WithComplexMessage_ShouldParseFullMessage()
    {
        // Arrange
        var output = "Program.cs(10,15): error CS0103: The name 'Foo' does not exist in the current context [C:\\Projects\\MyApp\\MyApp.csproj]";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(1);
        result[0].Message.Should().Be("The name 'Foo' does not exist in the current context [C:\\Projects\\MyApp\\MyApp.csproj]");
    }

    [Fact]
    public void ParseDiagnostics_WithNumericCodesOnly_ShouldParseCorrectly()
    {
        // Arrange
        var output = "Program.cs(10,15): error NU1234: Some NuGet error occurred";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(1);
        result[0].Code.Should().Be("NU1234");
    }

    [Fact]
    public void ParseDiagnostics_WithAlphanumericCodes_ShouldParseCorrectly()
    {
        // Arrange
        var output = @"
Program.cs(10,15): error CS0103: C# error
Build.targets(50,30): warning MSB3270: MSBuild warning
Package.json(1,1): error NU1101: NuGet error
";

        // Act
        var result = BuildValidator.ParseDiagnostics(output);

        // Assert
        result.Should().HaveCount(3);
        result[0].Code.Should().Be("CS0103");
        result[1].Code.Should().Be("MSB3270");
        result[2].Code.Should().Be("NU1101");
    }

    [Fact]
    public async Task ValidateAsync_WithNullPath_ShouldThrowArgumentException()
    {
        // Arrange
        var validator = new BuildValidator();

        // Act
        var act = () => validator.ValidateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("solutionOrProjectPath");
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var validator = new BuildValidator();

        // Act
        var act = () => validator.ValidateAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("solutionOrProjectPath");
    }

    [Fact]
    public async Task ValidateAsync_WithWhitespacePath_ShouldThrowArgumentException()
    {
        // Arrange
        var validator = new BuildValidator();

        // Act
        var act = () => validator.ValidateAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("solutionOrProjectPath");
    }

    [Fact]
    public async Task ValidateAsync_WithNonExistentPath_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var validator = new BuildValidator();
        var nonExistentPath = "C:\\NonExistent\\Solution.sln";

        // Act
        var act = () => validator.ValidateAsync(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage($"*{nonExistentPath}*");
    }

    [Fact]
    public void BuildResult_ShouldBeRecord()
    {
        // Arrange
        var result1 = new BuildResult { Success = true, ExitCode = 0 };
        var result2 = new BuildResult { Success = true, ExitCode = 0 };

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void BuildDiagnostic_ShouldBeRecord()
    {
        // Arrange
        var diag1 = new BuildDiagnostic
        {
            Code = "CS0103",
            Message = "Test",
            Severity = DiagnosticSeverity.Error
        };
        var diag2 = new BuildDiagnostic
        {
            Code = "CS0103",
            Message = "Test",
            Severity = DiagnosticSeverity.Error
        };

        // Assert
        diag1.Should().Be(diag2);
    }

    [Fact]
    public void BuildResult_WithDefaultValues_ShouldHaveEmptyCollections()
    {
        // Arrange & Act
        var result = new BuildResult();

        // Assert
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
        result.RawOutput.Should().BeEmpty();
    }

    [Fact]
    public void DiagnosticSeverity_ShouldHaveCorrectValues()
    {
        // Assert
        ((int)DiagnosticSeverity.Error).Should().Be(0);
        ((int)DiagnosticSeverity.Warning).Should().Be(1);
        ((int)DiagnosticSeverity.Info).Should().Be(2);
    }
}
