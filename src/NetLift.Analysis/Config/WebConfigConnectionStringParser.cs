using System.Xml.Linq;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Config;

namespace NetLift.Analysis.Config;

/// <summary>
/// Parser for connection strings section in web.config files.
/// </summary>
public sealed class WebConfigConnectionStringParser : IWebConfigConnectionStringParser
{
    private static readonly XNamespace XdtNamespace = "http://schemas.microsoft.com/XML-Document-Transform";

    /// <inheritdoc/>
    public ConnectionStringsSection Parse(XDocument webConfig)
    {
        var connectionStrings = new List<ConnectionStringInfo>();
        var hasEncrypted = false;

        var connectionStringsElement = webConfig
            .Descendants("connectionStrings")
            .FirstOrDefault();

        if (connectionStringsElement == null)
        {
            return new ConnectionStringsSection
            {
                ConnectionStrings = Array.Empty<ConnectionStringInfo>()
            };
        }

        // Check if section is encrypted
        var configProtectionProvider = connectionStringsElement
            .Attribute("configProtectionProvider")?.Value;

        if (!string.IsNullOrEmpty(configProtectionProvider))
        {
            hasEncrypted = true;
            // Connection strings are encrypted - cannot parse individual entries
            // The consuming code should handle this scenario appropriately
        }

        foreach (var add in connectionStringsElement.Elements("add"))
        {
            var name = add.Attribute("name")?.Value;
            var connStr = add.Attribute("connectionString")?.Value;
            var provider = add.Attribute("providerName")?.Value
                ?? "System.Data.SqlClient"; // Default for .NET Framework

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(connStr))
            {
                continue;
            }

            connectionStrings.Add(new ConnectionStringInfo
            {
                Name = name,
                ConnectionString = connStr,
                ProviderName = provider,
                IsEncrypted = hasEncrypted
            });
        }

        return new ConnectionStringsSection
        {
            ConnectionStrings = connectionStrings,
            HasEncryptedStrings = hasEncrypted
        };
    }

    /// <inheritdoc/>
    public ConnectionStringsSection ParseWithTransforms(
        XDocument webConfig,
        XDocument? transformConfig)
    {
        // First parse base config
        var baseSection = Parse(webConfig);

        if (transformConfig == null)
        {
            return baseSection;
        }

        // Apply XDT transformations
        return ApplyTransformations(baseSection, transformConfig);
    }

    private ConnectionStringsSection ApplyTransformations(
        ConnectionStringsSection baseSection,
        XDocument transformConfig)
    {
        var transformedStrings = baseSection.ConnectionStrings.ToList();

        var transformElement = transformConfig
            .Descendants("connectionStrings")
            .FirstOrDefault();

        if (transformElement == null)
        {
            return baseSection;
        }

        foreach (var add in transformElement.Elements("add"))
        {
            var name = add.Attribute("name")?.Value;
            var transform = add.Attribute(XName.Get("Transform", XdtNamespace.NamespaceName))?.Value;

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (transform == "SetAttributes")
            {
                // Replace existing connection string attributes
                var index = transformedStrings.FindIndex(x => x.Name == name);
                if (index >= 0)
                {
                    var connStr = add.Attribute("connectionString")?.Value;
                    var provider = add.Attribute("providerName")?.Value;

                    transformedStrings[index] = transformedStrings[index] with
                    {
                        ConnectionString = connStr ?? transformedStrings[index].ConnectionString,
                        ProviderName = provider ?? transformedStrings[index].ProviderName
                    };
                }
            }
            else if (transform == "Insert")
            {
                // Insert new connection string
                var connStr = add.Attribute("connectionString")?.Value;
                var provider = add.Attribute("providerName")?.Value ?? "System.Data.SqlClient";

                if (!string.IsNullOrEmpty(connStr))
                {
                    transformedStrings.Add(new ConnectionStringInfo
                    {
                        Name = name,
                        ConnectionString = connStr,
                        ProviderName = provider
                    });
                }
            }
            else if (transform == "Remove")
            {
                // Remove connection string by name
                transformedStrings.RemoveAll(x => x.Name == name);
            }
        }

        return new ConnectionStringsSection
        {
            ConnectionStrings = transformedStrings,
            HasEncryptedStrings = baseSection.HasEncryptedStrings
        };
    }
}
