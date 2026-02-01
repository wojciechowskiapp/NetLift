using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Wcf;
using System.Collections.Immutable;

namespace NetLift.Transforms.Wcf.Parsers;

/// <summary>
/// Parses WCF DataContract and DataMember attributes from C# source code using Roslyn.
/// Supports classes, structs, enums, inheritance, KnownType, nullable, and collection types.
/// </summary>
public sealed class WcfDataContractParser : IWcfDataContractParser
{
    private readonly List<string> _diagnostics = new();

    /// <inheritdoc />
    public IReadOnlyCollection<string> Diagnostics => _diagnostics.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<WcfDataContract> Parse(string sourceCode)
    {
        _diagnostics.Clear();

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Array.Empty<WcfDataContract>();
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // Create compilation with necessary references for semantic analysis
        var compilation = CreateCompilation(tree);
        var semanticModel = compilation.GetSemanticModel(tree);

        var contracts = new List<WcfDataContract>();

        // Find all type declarations (classes, structs, enums)
        var typeDeclarations = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Concat(root.DescendantNodes().OfType<EnumDeclarationSyntax>().Cast<BaseTypeDeclarationSyntax>());

        foreach (var typeDeclaration in typeDeclarations)
        {
            var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
            if (symbol is not INamedTypeSymbol namedTypeSymbol)
            {
                continue;
            }

            // Check if type has [DataContract] attribute
            if (!HasDataContractAttribute(namedTypeSymbol))
            {
                continue;
            }

            var contract = ParseDataContract(namedTypeSymbol, typeDeclaration, semanticModel);
            if (contract != null)
            {
                contracts.Add(contract);
            }
        }

        return contracts;
    }

    /// <summary>
    /// Creates a compilation with references needed for semantic analysis.
    /// </summary>
    private static CSharpCompilation CreateCompilation(SyntaxTree tree)
    {
        // Add references to core assemblies and System.Runtime.Serialization
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        // Try to add System.Runtime.Serialization reference
        try
        {
            // Load System.Runtime.Serialization.Primitives which contains DataContract attributes in .NET Core/5+
            var primitivesAssemblyName = "System.Runtime.Serialization.Primitives";
            var primitivesAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == primitivesAssemblyName);

            if (primitivesAssembly == null)
            {
                // Try to load it
                var assembly = System.Reflection.Assembly.Load(primitivesAssemblyName);
                if (assembly != null)
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
            else
            {
                references.Add(MetadataReference.CreateFromFile(primitivesAssembly.Location));
            }

            // Also add System.Runtime.Serialization for full framework compatibility
            var systemRuntimeSerializationAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "System.Runtime.Serialization");

            if (systemRuntimeSerializationAssembly != null)
            {
                references.Add(MetadataReference.CreateFromFile(systemRuntimeSerializationAssembly.Location));
            }

            // Add System.Runtime for basic types
            var systemRuntimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "System.Runtime");

            if (systemRuntimeAssembly != null)
            {
                references.Add(MetadataReference.CreateFromFile(systemRuntimeAssembly.Location));
            }
        }
        catch
        {
            // Ignore if can't load serialization assemblies
        }

        return CSharpCompilation.Create(
            "WcfAnalysis",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Checks if a type has the [DataContract] attribute.
    /// </summary>
    private static bool HasDataContractAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name == "DataContractAttribute" ||
            attr.AttributeClass?.Name == "DataContract");
    }

    /// <summary>
    /// Parses a DataContract type declaration.
    /// </summary>
    private WcfDataContract? ParseDataContract(
        INamedTypeSymbol typeSymbol,
        BaseTypeDeclarationSyntax typeSyntax,
        SemanticModel semanticModel)
    {
        try
        {
            var dataContractAttr = typeSymbol.GetAttributes()
                .FirstOrDefault(attr =>
                    attr.AttributeClass?.Name == "DataContractAttribute" ||
                    attr.AttributeClass?.Name == "DataContract");

            if (dataContractAttr == null)
            {
                return null;
            }

            // Parse attribute arguments from syntax instead of semantic model
            var dataContractAttrSyntax = FindAttributeSyntax(typeSyntax, "DataContract");

            var isEnum = typeSymbol.TypeKind == TypeKind.Enum;
            var isClass = typeSymbol.TypeKind == TypeKind.Class || typeSymbol.TypeKind == TypeKind.Struct;

            var contract = new WcfDataContract
            {
                TypeName = typeSymbol.Name,
                FullyQualifiedName = typeSymbol.ToDisplayString(),
                Namespace = GetAttributeArgumentValueFromSyntax(dataContractAttrSyntax, "Namespace"),
                Name = GetAttributeArgumentValueFromSyntax(dataContractAttrSyntax, "Name"),
                IsClass = isClass,
                IsEnum = isEnum,
                BaseType = typeSymbol.BaseType?.ToDisplayString(),
                Properties = isClass ? ParseDataMembers(typeSymbol, semanticModel) : [],
                EnumMembers = isEnum ? ParseEnumMembers(typeSymbol) : [],
                KnownTypes = ParseKnownTypes(typeSymbol)
            };

            return contract;
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"Error parsing type {typeSymbol.Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Finds an attribute syntax by name on a type declaration.
    /// </summary>
    private static AttributeSyntax? FindAttributeSyntax(BaseTypeDeclarationSyntax typeDecl, string attributeName)
    {
        return typeDecl.AttributeLists
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
    private static string? GetAttributeArgumentValueFromSyntax(AttributeSyntax? attribute, string argumentName)
    {
        if (attribute?.ArgumentList == null)
        {
            return null;
        }

        // Look for named argument
        var namedArg = attribute.ArgumentList.Arguments
            .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.Text == argumentName);

        if (namedArg != null && namedArg.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Gets an integer argument value from an attribute syntax by name.
    /// </summary>
    private static int GetAttributeArgumentIntFromSyntax(AttributeSyntax? attribute, string argumentName)
    {
        if (attribute?.ArgumentList == null)
        {
            return 0;
        }

        var namedArg = attribute.ArgumentList.Arguments
            .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.Text == argumentName);

        if (namedArg != null && namedArg.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            if (literal.Token.Value is int intValue)
            {
                return intValue;
            }
        }

        return 0;
    }

    /// <summary>
    /// Gets a boolean argument value from an attribute syntax by name.
    /// </summary>
    private static bool GetAttributeArgumentBoolFromSyntax(AttributeSyntax? attribute, string argumentName, bool defaultValue = false)
    {
        if (attribute?.ArgumentList == null)
        {
            return defaultValue;
        }

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
                if (literal.IsKind(SyntaxKind.FalseLiteralExpression))
                {
                    return false;
                }
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Parses DataMember properties from a class/struct.
    /// </summary>
    private List<WcfDataMember> ParseDataMembers(INamedTypeSymbol typeSymbol, SemanticModel semanticModel)
    {
        var members = new List<WcfDataMember>();

        foreach (var member in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var dataMemberAttr = member.GetAttributes()
                .FirstOrDefault(attr =>
                    attr.AttributeClass?.Name == "DataMemberAttribute" ||
                    attr.AttributeClass?.Name == "DataMember");

            if (dataMemberAttr == null)
            {
                continue;
            }

            try
            {
                // Find the syntax for this property to parse attribute arguments
                var propertySyntax = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as PropertyDeclarationSyntax;
                var dataMemberAttrSyntax = FindAttributeSyntaxOnMember(propertySyntax, "DataMember");

                var dataMember = new WcfDataMember
                {
                    Name = member.Name,
                    Type = member.Type.Name,
                    FullTypeName = member.Type.ToDisplayString(),
                    Order = GetAttributeArgumentIntFromSyntax(dataMemberAttrSyntax, "Order"),
                    IsRequired = GetAttributeArgumentBoolFromSyntax(dataMemberAttrSyntax, "IsRequired"),
                    EmitDefaultValue = GetAttributeArgumentBoolFromSyntax(dataMemberAttrSyntax, "EmitDefaultValue", defaultValue: true),
                    IsNullable = IsNullableType(member.Type),
                    IsCollection = IsCollectionType(member.Type)
                };

                members.Add(dataMember);
            }
            catch (Exception ex)
            {
                _diagnostics.Add($"Error parsing property {member.Name}: {ex.Message}");
            }
        }

        return members;
    }

    /// <summary>
    /// Finds an attribute syntax on a member declaration (property, method, etc).
    /// </summary>
    private static AttributeSyntax? FindAttributeSyntaxOnMember(SyntaxNode? memberSyntax, string attributeName)
    {
        if (memberSyntax is not MemberDeclarationSyntax memberDecl)
        {
            return null;
        }

        return memberDecl.AttributeLists
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
    /// Checks if a type is nullable.
    /// </summary>
    private static bool IsNullableType(ITypeSymbol type)
    {
        // Check for nullable reference types (C# 8+)
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        // Check for Nullable<T> value types
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a type is a collection type.
    /// </summary>
    private static bool IsCollectionType(ITypeSymbol type)
    {
        // Arrays
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        // Generic collections
        if (type is INamedTypeSymbol named)
        {
            var typeName = named.Name;
            return typeName is "List" or "IList" or "ICollection" or "IEnumerable"
                or "Collection" or "HashSet" or "IReadOnlyList" or "IReadOnlyCollection"
                or "ObservableCollection" or "ISet";
        }

        return false;
    }

    /// <summary>
    /// Parses enum members. If [DataContract] is used, only includes members with [EnumMember] attribute.
    /// </summary>
    private List<WcfEnumMember> ParseEnumMembers(INamedTypeSymbol typeSymbol)
    {
        var members = new List<WcfEnumMember>();

        foreach (var member in typeSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.ConstantValue == null)
            {
                continue;
            }

            try
            {
                var enumMemberAttr = member.GetAttributes()
                    .FirstOrDefault(attr =>
                        attr.AttributeClass?.Name == "EnumMemberAttribute" ||
                        attr.AttributeClass?.Name == "EnumMember");

                // For DataContract enums, only include members with EnumMember attribute
                // This follows WCF serialization behavior
                if (enumMemberAttr == null)
                {
                    continue;
                }

                // Get the syntax to parse the Value argument
                var enumMemberSyntax = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                var enumMemberAttrSyntax = FindAttributeSyntaxOnMember(enumMemberSyntax, "EnumMember");

                var enumMember = new WcfEnumMember
                {
                    Name = member.Name,
                    Value = Convert.ToInt32(member.ConstantValue),
                    SerializedName = GetAttributeArgumentValueFromSyntax(enumMemberAttrSyntax, "Value")
                };

                members.Add(enumMember);
            }
            catch (Exception ex)
            {
                _diagnostics.Add($"Error parsing enum member {member.Name}: {ex.Message}");
            }
        }

        return members;
    }

    /// <summary>
    /// Parses [KnownType] attributes on a type.
    /// </summary>
    private List<string> ParseKnownTypes(INamedTypeSymbol typeSymbol)
    {
        var knownTypes = new List<string>();

        var knownTypeAttrs = typeSymbol.GetAttributes()
            .Where(attr =>
                attr.AttributeClass?.Name == "KnownTypeAttribute" ||
                attr.AttributeClass?.Name == "KnownType");

        foreach (var attr in knownTypeAttrs)
        {
            try
            {
                // KnownType can be specified as KnownType(typeof(T))
                if (attr.ConstructorArguments.Length > 0)
                {
                    var arg = attr.ConstructorArguments[0];
                    if (arg.Value is INamedTypeSymbol knownTypeSymbol)
                    {
                        knownTypes.Add(knownTypeSymbol.ToDisplayString());
                    }
                }
            }
            catch (Exception ex)
            {
                _diagnostics.Add($"Error parsing KnownType attribute: {ex.Message}");
            }
        }

        return knownTypes;
    }
}
