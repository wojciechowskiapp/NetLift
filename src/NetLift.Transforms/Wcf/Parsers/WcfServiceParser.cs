using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Wcf;

namespace NetLift.Transforms.Wcf.Parsers;

/// <summary>
/// Parses C# source code to extract WCF service contracts using Roslyn.
/// Identifies interfaces with [ServiceContract] and methods with [OperationContract].
/// </summary>
public sealed class WcfServiceParser : IWcfServiceParser
{
    private readonly List<string> _diagnostics = new();

    /// <inheritdoc />
    public IReadOnlyCollection<string> Diagnostics => _diagnostics.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<WcfServiceContract> Parse(string sourceCode)
    {
        _diagnostics.Clear();

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Array.Empty<WcfServiceContract>();
        }

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            // Create compilation for semantic analysis
            var compilation = CSharpCompilation.Create("WcfAnalysis")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree);

            var model = compilation.GetSemanticModel(tree);

            var contracts = new List<WcfServiceContract>();

            // Find all interfaces with [ServiceContract] attribute
            var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();

            foreach (var interfaceDecl in interfaces)
            {
                var symbol = model.GetDeclaredSymbol(interfaceDecl);
                if (symbol == null)
                {
                    continue;
                }

                var hasServiceContract = symbol.GetAttributes()
                    .Any(a => IsServiceContractAttribute(a.AttributeClass));

                if (!hasServiceContract)
                {
                    continue;
                }

                var contract = ParseServiceContract(interfaceDecl, symbol, model);
                if (contract != null)
                {
                    contracts.Add(contract);
                }
            }

            return contracts;
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"Error parsing WCF service contracts: {ex.Message}");
            return Array.Empty<WcfServiceContract>();
        }
    }

    /// <summary>
    /// Parses a single service contract interface.
    /// </summary>
    private WcfServiceContract? ParseServiceContract(
        InterfaceDeclarationSyntax interfaceDecl,
        INamedTypeSymbol symbol,
        SemanticModel model)
    {
        try
        {
            var serviceContractAttr = symbol.GetAttributes()
                .FirstOrDefault(a => IsServiceContractAttribute(a.AttributeClass));

            if (serviceContractAttr == null)
            {
                return null;
            }

            // Extract ServiceContract attribute properties from syntax
            var attributeSyntax = FindAttributeSyntax(interfaceDecl, "ServiceContract");
            var namespaceProp = GetAttributeArgumentValue(attributeSyntax, "Namespace");
            var nameProp = GetAttributeArgumentValue(attributeSyntax, "Name");
            var sessionRequired = false; // Will be set if SessionMode argument is present
            var callbackContract = GetAttributeArgumentValue(attributeSyntax, "CallbackContract");

            // Parse all operations
            var operations = new List<WcfOperation>();
            foreach (var member in interfaceDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                var methodSymbol = model.GetDeclaredSymbol(member);
                if (methodSymbol == null)
                {
                    continue;
                }

                var hasOperationContract = methodSymbol.GetAttributes()
                    .Any(a => IsOperationContractAttribute(a.AttributeClass));

                if (hasOperationContract)
                {
                    var operation = ParseOperation(member, methodSymbol);
                    if (operation != null)
                    {
                        operations.Add(operation);
                    }
                }
            }

            return new WcfServiceContract
            {
                InterfaceName = symbol.Name,
                FullyQualifiedName = symbol.ToDisplayString(),
                Namespace = namespaceProp,
                Name = nameProp,
                SessionRequired = sessionRequired,
                CallbackContract = callbackContract,
                Operations = operations
            };
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"Error parsing service contract '{symbol.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses a single operation contract method.
    /// </summary>
    private WcfOperation? ParseOperation(MethodDeclarationSyntax methodDecl, IMethodSymbol methodSymbol)
    {
        try
        {
            var operationContractAttr = methodSymbol.GetAttributes()
                .FirstOrDefault(a => IsOperationContractAttribute(a.AttributeClass));

            if (operationContractAttr == null)
            {
                return null;
            }

            // Extract OperationContract attribute properties from syntax
            var attributeSyntax = FindAttributeSyntax(methodDecl, "OperationContract");
            var isOneWay = GetAttributeArgumentValueBool(attributeSyntax, "IsOneWay");
            var action = GetAttributeArgumentValue(attributeSyntax, "Action");
            var replyAction = GetAttributeArgumentValue(attributeSyntax, "ReplyAction");

            // Determine return type and if async
            var returnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var fullReturnType = methodSymbol.ReturnType.ToDisplayString();
            var isAsync = IsTaskType(methodSymbol.ReturnType);

            // Extract XML documentation
            var xmlDoc = GetXmlDocumentation(methodDecl);

            // Parse parameters
            var parameters = new List<WcfParameter>();
            foreach (var param in methodSymbol.Parameters)
            {
                var paramInfo = ParseParameter(param);
                if (paramInfo != null)
                {
                    parameters.Add(paramInfo);
                }
            }

            return new WcfOperation
            {
                Name = methodSymbol.Name,
                ReturnType = returnType,
                IsOneWay = isOneWay,
                IsAsync = isAsync,
                Action = action,
                ReplyAction = replyAction,
                XmlDocumentation = xmlDoc,
                Parameters = parameters
            };
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"Error parsing operation '{methodSymbol.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses a method parameter.
    /// </summary>
    private static WcfParameter? ParseParameter(IParameterSymbol paramSymbol)
    {
        var type = paramSymbol.Type;
        var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var fullTypeName = type.ToDisplayString();

        return new WcfParameter
        {
            Name = paramSymbol.Name,
            Type = typeName,
            FullTypeName = fullTypeName,
            IsArray = type.TypeKind == TypeKind.Array,
            IsNullable = IsNullableType(type),
            IsGeneric = type is INamedTypeSymbol { IsGenericType: true }
        };
    }

    /// <summary>
    /// Extracts XML documentation from a method declaration.
    /// </summary>
    private static string? GetXmlDocumentation(MethodDeclarationSyntax methodDecl)
    {
        var trivia = methodDecl.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                       t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .FirstOrDefault();

        if (trivia == default)
        {
            return null;
        }

        var structure = trivia.GetStructure();
        if (structure == null)
        {
            return null;
        }

        // Extract summary text from XML doc comment
        var summaryElement = structure.DescendantNodes()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag?.Name?.ToString() == "summary");

        if (summaryElement != null)
        {
            var content = summaryElement.Content.ToString().Trim();
            // Clean up the content by removing extra whitespace
            return string.Join(" ", content.Split(new[] { '\r', '\n', ' ' },
                StringSplitOptions.RemoveEmptyEntries));
        }

        return null;
    }

    /// <summary>
    /// Finds an attribute syntax node by name on a member declaration.
    /// </summary>
    private static AttributeSyntax? FindAttributeSyntax(MemberDeclarationSyntax member, string attributeName)
    {
        return member.AttributeLists
            .SelectMany(list => list.Attributes)
            .FirstOrDefault(attr =>
            {
                var name = attr.Name.ToString();
                return name == attributeName ||
                       name == $"{attributeName}Attribute" ||
                       name.EndsWith($".{attributeName}") ||
                       name.EndsWith($".{attributeName}Attribute");
            });
    }

    /// <summary>
    /// Gets a string argument value from an attribute syntax by name.
    /// </summary>
    private static string? GetAttributeArgumentValue(AttributeSyntax? attribute, string argumentName)
    {
        if (attribute?.ArgumentList == null)
        {
            return null;
        }

        // Look for named argument
        var namedArg = attribute.ArgumentList.Arguments
            .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.Text == argumentName);

        if (namedArg != null)
        {
            return ExtractStringValue(namedArg.Expression);
        }

        return null;
    }

    /// <summary>
    /// Gets a boolean argument value from an attribute syntax by name.
    /// </summary>
    private static bool GetAttributeArgumentValueBool(AttributeSyntax? attribute, string argumentName)
    {
        if (attribute?.ArgumentList == null)
        {
            return false;
        }

        // Look for named argument
        var namedArg = attribute.ArgumentList.Arguments
            .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.Text == argumentName);

        if (namedArg != null)
        {
            if (namedArg.Expression is LiteralExpressionSyntax literal)
            {
                if (literal.IsKind(SyntaxKind.TrueLiteralExpression))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts a string value from an expression (handles string literals and typeof expressions).
    /// </summary>
    private static string? ExtractStringValue(ExpressionSyntax? expression)
    {
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        // For typeof expressions, return the type name
        if (expression is TypeOfExpressionSyntax typeOfExpr)
        {
            return typeOfExpr.Type.ToString();
        }

        return null;
    }

    /// <summary>
    /// Checks if a type is a ServiceContract attribute.
    /// </summary>
    private static bool IsServiceContractAttribute(INamedTypeSymbol? attributeClass)
    {
        return attributeClass?.Name is "ServiceContractAttribute" or "ServiceContract";
    }

    /// <summary>
    /// Checks if a type is an OperationContract attribute.
    /// </summary>
    private static bool IsOperationContractAttribute(INamedTypeSymbol? attributeClass)
    {
        return attributeClass?.Name is "OperationContractAttribute" or "OperationContract";
    }

    /// <summary>
    /// Checks if a type is Task or Task&lt;T&gt;.
    /// </summary>
    private static bool IsTaskType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var typeName = namedType.Name;
        return typeName == "Task" &&
               (namedType.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks");
    }

    /// <summary>
    /// Checks if a type is nullable.
    /// </summary>
    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            return namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }

        return false;
    }
}
