using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Tests.Unit.Analysis;

/// <summary>
/// Verification tests to ensure Roslyn packages are correctly installed and functional.
/// </summary>
public sealed class RoslynVerificationTests
{
    [Fact]
    public void CanParseSimpleCSharpClass()
    {
        // Arrange
        var code = "public class Foo { }";

        // Act
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        // Assert
        Assert.Equal("Foo", classDecl.Identifier.Text);
    }

    [Fact]
    public void CanParseClassWithMembers()
    {
        // Arrange
        var code = @"
            public class TestClass
            {
                public int Property { get; set; }
                public void Method() { }
            }";

        // Act
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        // Assert
        Assert.Equal("TestClass", classDecl.Identifier.Text);
        Assert.NotEmpty(classDecl.Members);
        Assert.Equal(2, classDecl.Members.Count);
    }

    [Fact]
    public void CanParseNamespaceDeclaration()
    {
        // Arrange
        var code = @"
            namespace TestNamespace
            {
                public class TestClass { }
            }";

        // Act
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var namespaceDecl = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .First();

        // Assert
        Assert.NotNull(namespaceDecl);
        Assert.Equal("TestNamespace", namespaceDecl.Name.ToString());
    }
}
