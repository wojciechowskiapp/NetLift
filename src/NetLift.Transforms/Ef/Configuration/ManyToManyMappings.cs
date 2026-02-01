namespace NetLift.Transforms.Ef.Configuration;

/// <summary>
/// Configuration for many-to-many relationship transformation patterns.
/// Defines rules for detecting and transforming EF6 Map() configurations.
/// </summary>
public static class ManyToManyMappings
{
    /// <summary>
    /// Gets the default confidence score for simple many-to-many without Map().
    /// EF Core 5+ supports this pattern natively.
    /// </summary>
    public const int SimpleConfidence = 95;

    /// <summary>
    /// Gets the confidence score for standard Map() patterns with table and key names.
    /// </summary>
    public const int StandardMapConfidence = 80;

    /// <summary>
    /// Gets the confidence score for complex Map() scenarios with additional configuration.
    /// </summary>
    public const int ComplexMapConfidence = 65;

    /// <summary>
    /// Checks if a Map() configuration is simple (only table name and key names).
    /// </summary>
    /// <param name="hasToTable">Whether ToTable() was called.</param>
    /// <param name="hasMapLeftKey">Whether MapLeftKey() was called.</param>
    /// <param name="hasMapRightKey">Whether MapRightKey() was called.</param>
    /// <param name="hasOtherCalls">Whether other Map() methods were called.</param>
    /// <returns>True if the configuration is simple and can be auto-transformed.</returns>
    public static bool IsSimpleMapConfiguration(
        bool hasToTable,
        bool hasMapLeftKey,
        bool hasMapRightKey,
        bool hasOtherCalls)
    {
        // Simple if it only uses ToTable, MapLeftKey, and MapRightKey
        // No other Map() methods like HasColumnAnnotation, HasForeignKey, etc.
        return !hasOtherCalls;
    }

    /// <summary>
    /// Gets the confidence score for a Map() configuration.
    /// </summary>
    public static int GetMapConfidenceScore(
        bool hasToTable,
        bool hasMapLeftKey,
        bool hasMapRightKey,
        bool hasOtherCalls)
    {
        if (hasOtherCalls)
        {
            // Complex configuration - lower confidence
            return ComplexMapConfidence;
        }

        if (hasToTable || hasMapLeftKey || hasMapRightKey)
        {
            // Standard Map() with table/key configuration
            return StandardMapConfidence;
        }

        // Empty Map() - treat as simple
        return SimpleConfidence;
    }

    /// <summary>
    /// Generates a UsingEntity configuration string.
    /// </summary>
    public static string GenerateUsingEntity(
        string? joinTableName,
        string? leftKeyName,
        string? rightKeyName,
        string leftEntityType,
        string rightEntityType)
    {
        // If no configuration, no UsingEntity needed (EF Core 5+ implicit)
        if (string.IsNullOrWhiteSpace(joinTableName) &&
            string.IsNullOrWhiteSpace(leftKeyName) &&
            string.IsNullOrWhiteSpace(rightKeyName))
        {
            return string.Empty;
        }

        // Build UsingEntity with configuration
        var tableName = joinTableName ?? $"{leftEntityType}{rightEntityType}";
        var leftKey = leftKeyName ?? $"{leftEntityType}Id";
        var rightKey = rightKeyName ?? $"{rightEntityType}Id";

        return $@"
        .UsingEntity<Dictionary<string, object>>(
            ""{tableName}"",
            j => j.HasOne<{rightEntityType}>().WithMany().HasForeignKey(""{rightKey}""),
            j => j.HasOne<{leftEntityType}>().WithMany().HasForeignKey(""{leftKey}""))";
    }
}
