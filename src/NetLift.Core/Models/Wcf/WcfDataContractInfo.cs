namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents a WCF DataContract parsed from source code.
/// </summary>
public sealed record WcfDataContract
{
    /// <summary>
    /// Gets the type name without namespace.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets the fully qualified type name including namespace.
    /// </summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>
    /// Gets the DataContract namespace from [DataContract(Namespace = "...")].
    /// Null if not specified.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Gets the DataContract name from [DataContract(Name = "...")].
    /// Null if not specified (defaults to TypeName).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a class type.
    /// </summary>
    public bool IsClass { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is an enum type.
    /// </summary>
    public bool IsEnum { get; init; }

    /// <summary>
    /// Gets the base type fully qualified name, or null if no base type.
    /// </summary>
    public string? BaseType { get; init; }

    /// <summary>
    /// Gets the list of data members (properties) for class types.
    /// </summary>
    public IReadOnlyList<WcfDataMember> Properties { get; init; } = [];

    /// <summary>
    /// Gets the list of enum members for enum types.
    /// </summary>
    public IReadOnlyList<WcfEnumMember> EnumMembers { get; init; } = [];

    /// <summary>
    /// Gets the list of known types from [KnownType] attributes.
    /// </summary>
    public IReadOnlyList<string> KnownTypes { get; init; } = [];
}

/// <summary>
/// Represents a WCF DataMember property.
/// </summary>
public sealed record WcfDataMember
{
    /// <summary>
    /// Gets the property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the property type simple name.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the property type fully qualified name.
    /// </summary>
    public required string FullTypeName { get; init; }

    /// <summary>
    /// Gets the serialization order from [DataMember(Order = n)].
    /// Default is 0.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets a value indicating whether the member is required from [DataMember(IsRequired = true)].
    /// Default is false.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is nullable.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is a collection.
    /// </summary>
    public bool IsCollection { get; init; }

    /// <summary>
    /// Gets a value indicating whether to emit default values from [DataMember(EmitDefaultValue = false)].
    /// Default is true.
    /// </summary>
    public bool EmitDefaultValue { get; init; } = true;
}

/// <summary>
/// Represents a WCF EnumMember.
/// </summary>
public sealed record WcfEnumMember
{
    /// <summary>
    /// Gets the enum member name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the enum member value.
    /// </summary>
    public int Value { get; init; }

    /// <summary>
    /// Gets the serialized name from [EnumMember(Value = "...")].
    /// Null if not specified (defaults to Name).
    /// </summary>
    public string? SerializedName { get; init; }
}
