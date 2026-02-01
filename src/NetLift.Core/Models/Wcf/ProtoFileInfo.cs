namespace NetLift.Core.Models.Wcf;

/// <summary>
/// Represents a generated Protocol Buffer (.proto) file.
/// </summary>
public sealed record ProtoFileInfo
{
    /// <summary>
    /// Gets the proto file name (e.g., "customer_service.proto").
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the complete proto file content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the proto package name (e.g., "company.services").
    /// </summary>
    public required string PackageName { get; init; }

    /// <summary>
    /// Gets the C# namespace for generated code (e.g., "Company.Grpc").
    /// </summary>
    public required string CSharpNamespace { get; init; }

    /// <summary>
    /// Gets the list of proto imports (e.g., "google/protobuf/timestamp.proto").
    /// </summary>
    public IReadOnlyList<string> Imports { get; init; } = [];
}
