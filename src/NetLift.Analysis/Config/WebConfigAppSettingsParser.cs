using System.Globalization;
using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Analysis.Config;

/// <summary>
/// Parses appSettings section from web.config files with support for type inference and hierarchical keys.
/// </summary>
public class WebConfigAppSettingsParser : IWebConfigAppSettingsParser
{
    private static readonly char[] HierarchySeparators = { ':', '.' };

    /// <inheritdoc />
    public AppSettingsSection Parse(XDocument webConfig)
    {
        if (webConfig == null)
        {
            return new AppSettingsSection();
        }

        var appSettingsElement = webConfig.Root?.Element("appSettings");
        if (appSettingsElement == null)
        {
            return new AppSettingsSection();
        }

        var externalFile = appSettingsElement.Attribute("file")?.Value;
        var isEncrypted = IsEncryptedSection(appSettingsElement);

        var settings = appSettingsElement
            .Elements("add")
            .Select(ParseAddElement)
            .Where(s => !string.IsNullOrWhiteSpace(s.Key))
            .ToList();

        return new AppSettingsSection
        {
            Settings = settings.AsReadOnly(),
            ExternalFile = externalFile,
            IsEncrypted = isEncrypted
        };
    }

    /// <inheritdoc />
    public AppSettingsSection ParseWithTransforms(XDocument webConfig, XDocument? transformConfig)
    {
        if (webConfig == null)
        {
            return new AppSettingsSection();
        }

        if (transformConfig == null)
        {
            return Parse(webConfig);
        }

        // Apply XDT transforms
        var mergedConfig = ApplyTransforms(webConfig, transformConfig);
        return Parse(mergedConfig);
    }

    /// <inheritdoc />
    public Dictionary<string, object> BuildHierarchy(AppSettingsSection section)
    {
        var hierarchy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var setting in section.Settings)
        {
            if (setting.KeyPath != null && setting.KeyPath.Length > 1)
            {
                // Nested key - build hierarchy
                AddNestedKey(hierarchy, setting.KeyPath, setting.Value);
            }
            else
            {
                // Flat key - add directly
                hierarchy[setting.Key] = ConvertValueToTypedObject(setting.Value, setting.InferredType);
            }
        }

        return hierarchy;
    }

    private static AppSetting ParseAddElement(XElement element)
    {
        var key = element.Attribute("key")?.Value ?? string.Empty;
        var value = element.Attribute("value")?.Value ?? string.Empty;

        var inferredType = InferType(value);
        var keyPath = ExtractKeyPath(key);

        return new AppSetting
        {
            Key = key,
            Value = value,
            InferredType = inferredType,
            KeyPath = keyPath
        };
    }

    private static SettingType InferType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SettingType.String;
        }

        var trimmedValue = value.Trim();

        // JSON (starts with { or [) - check this first as it's most specific
        if ((trimmedValue.StartsWith("{") && trimmedValue.EndsWith("}")) ||
            (trimmedValue.StartsWith("[") && trimmedValue.EndsWith("]")))
        {
            return SettingType.Json;
        }

        // Boolean
        if (bool.TryParse(trimmedValue, out _))
        {
            return SettingType.Boolean;
        }

        // Double (check for decimal point first, before integer)
        // Use invariant culture to ensure consistent parsing regardless of system locale
        if (trimmedValue.Contains('.') && double.TryParse(trimmedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return SettingType.Double;
        }

        // Integer
        if (long.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return SettingType.Integer;
        }

        return SettingType.String;
    }

    private static string[]? ExtractKeyPath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        // Check if key contains hierarchy separators
        var separatorIndex = key.IndexOfAny(HierarchySeparators);
        if (separatorIndex == -1)
        {
            return null; // No hierarchy
        }

        // Determine which separator is used (prefer the first one found)
        var separator = key[separatorIndex];

        // Split by the separator
        var parts = key.Split(separator)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        return parts.Length > 1 ? parts : null;
    }

    private static bool IsEncryptedSection(XElement element)
    {
        // Check for configProtectionProvider attribute
        var provider = element.Attribute("configProtectionProvider")?.Value;
        if (!string.IsNullOrWhiteSpace(provider))
        {
            return true;
        }

        // Check for EncryptedData child element
        return element.Element("EncryptedData") != null;
    }

    private static XDocument ApplyTransforms(XDocument baseConfig, XDocument transformConfig)
    {
        // Create a copy of base config
        var mergedConfig = new XDocument(baseConfig);

        var baseAppSettings = mergedConfig.Root?.Element("appSettings");
        var transformAppSettings = transformConfig.Root?.Element("appSettings");

        if (baseAppSettings == null || transformAppSettings == null)
        {
            return mergedConfig;
        }

        // Apply transforms
        foreach (var transformAdd in transformAppSettings.Elements("add"))
        {
            var key = transformAdd.Attribute("key")?.Value;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var transformType = transformAdd.Attribute(XName.Get("Transform", "http://schemas.microsoft.com/XML-Document-Transform"))?.Value;
            var baseAdd = baseAppSettings.Elements("add").FirstOrDefault(e => e.Attribute("key")?.Value == key);

            if (transformType == "Remove" && baseAdd != null)
            {
                baseAdd.Remove();
            }
            else if (transformType == "Insert" || transformType == "InsertIfMissing")
            {
                if (baseAdd == null || transformType == "Insert")
                {
                    baseAppSettings.Add(new XElement(transformAdd));
                }
            }
            else if (transformType == "Replace" && baseAdd != null)
            {
                baseAdd.ReplaceWith(new XElement(transformAdd));
            }
            else if (transformType == "SetAttributes" && baseAdd != null)
            {
                // Update value attribute
                var newValue = transformAdd.Attribute("value")?.Value;
                if (newValue != null)
                {
                    baseAdd.SetAttributeValue("value", newValue);
                }
            }
            else if (baseAdd != null)
            {
                // Default behavior: update existing
                baseAdd.SetAttributeValue("value", transformAdd.Attribute("value")?.Value);
            }
            else
            {
                // Default behavior: add new
                baseAppSettings.Add(new XElement(transformAdd));
            }
        }

        return mergedConfig;
    }

    private static void AddNestedKey(Dictionary<string, object> hierarchy, string[] keyPath, string value)
    {
        var current = hierarchy;

        for (int i = 0; i < keyPath.Length - 1; i++)
        {
            var segment = keyPath[i];

            if (!current.ContainsKey(segment))
            {
                current[segment] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            if (current[segment] is Dictionary<string, object> dict)
            {
                current = dict;
            }
            else
            {
                // Conflict: key path already has a value
                // Create new dict and replace
                var newDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                current[segment] = newDict;
                current = newDict;
            }
        }

        var finalKey = keyPath[^1];
        var inferredType = InferType(value);
        current[finalKey] = ConvertValueToTypedObject(value, inferredType);
    }

    private static object ConvertValueToTypedObject(string value, SettingType type)
    {
        return type switch
        {
            SettingType.Boolean => bool.TryParse(value, out var b) ? b : value,
            SettingType.Integer => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : value,
            SettingType.Double => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : value,
            SettingType.Json => value, // Keep as string for JSON
            _ => value
        };
    }
}
