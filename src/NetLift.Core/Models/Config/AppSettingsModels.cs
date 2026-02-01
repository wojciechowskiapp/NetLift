namespace NetLift.Core.Models.Config;

/// <summary>
/// Represents a single application setting from web.config.
/// </summary>
public sealed record AppSetting
{
    /// <summary>
    /// Gets the setting key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the setting value.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the inferred type based on value analysis.
    /// </summary>
    public SettingType InferredType { get; init; } = SettingType.String;

    /// <summary>
    /// Gets the hierarchical key path when using colon or dot separators.
    /// For example, "Database:ConnectionString" becomes ["Database", "ConnectionString"].
    /// </summary>
    public string[]? KeyPath { get; init; }
}

/// <summary>
/// Represents the inferred type of an application setting.
/// </summary>
public enum SettingType
{
    /// <summary>
    /// Standard string value.
    /// </summary>
    String,

    /// <summary>
    /// Boolean value (true/false).
    /// </summary>
    Boolean,

    /// <summary>
    /// Integer value.
    /// </summary>
    Integer,

    /// <summary>
    /// Double/decimal value.
    /// </summary>
    Double,

    /// <summary>
    /// JSON object or array.
    /// </summary>
    Json
}

/// <summary>
/// Represents the appSettings section from web.config.
/// </summary>
public sealed record AppSettingsSection
{
    /// <summary>
    /// Gets the list of application settings.
    /// </summary>
    public IReadOnlyList<AppSetting> Settings { get; init; } = Array.Empty<AppSetting>();

    /// <summary>
    /// Gets the path to an external file if specified via file attribute.
    /// </summary>
    public string? ExternalFile { get; init; }

    /// <summary>
    /// Gets a value indicating whether the section is encrypted.
    /// </summary>
    public bool IsEncrypted { get; init; }
}
