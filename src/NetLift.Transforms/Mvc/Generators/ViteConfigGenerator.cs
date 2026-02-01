using System.Text;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Mvc;

namespace NetLift.Transforms.Mvc.Generators;

/// <summary>
/// Generates Vite configuration files from bundle definitions.
/// </summary>
public sealed class ViteConfigGenerator : IViteConfigGenerator
{
    /// <inheritdoc />
    public string Generate(IEnumerable<BundleDefinition> bundles)
    {
        if (bundles == null)
            throw new ArgumentNullException(nameof(bundles));

        var bundleList = bundles.ToList();
        var sb = new StringBuilder();

        sb.AppendLine("import { defineConfig } from 'vite';");
        sb.AppendLine("import { resolve } from 'path';");
        sb.AppendLine();
        sb.AppendLine("export default defineConfig({");
        sb.AppendLine("  build: {");
        sb.AppendLine("    outDir: 'wwwroot/dist',");
        sb.AppendLine("    emptyOutDir: true,");
        sb.AppendLine("    manifest: true,");
        sb.AppendLine("    rollupOptions: {");
        sb.AppendLine("      input: {");

        // Generate entry points from bundles
        var entries = new List<string>();
        foreach (var bundle in bundleList)
        {
            var entryName = GetEntryName(bundle);
            var entryPath = ConvertBundleToEntryPath(bundle);
            entries.Add($"        {entryName}: resolve(__dirname, '{entryPath}')");
        }

        sb.AppendLine(string.Join(",\n", entries));
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine("  server: {");
        sb.AppendLine("    port: 5173,");
        sb.AppendLine("    strictPort: true");
        sb.AppendLine("  },");
        sb.AppendLine("  resolve: {");
        sb.AppendLine("    alias: {");
        sb.AppendLine("      '@': resolve(__dirname, 'src'),");
        sb.AppendLine("      '~/Scripts': resolve(__dirname, 'src/js'),");
        sb.AppendLine("      '~/Content': resolve(__dirname, 'src/css')");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.Append("});");

        return sb.ToString();
    }

    /// <summary>
    /// Gets a clean entry name from the bundle's virtual path.
    /// </summary>
    private static string GetEntryName(BundleDefinition bundle)
    {
        // Extract name from virtual path (e.g., "~/bundles/jquery" -> "jquery")
        var name = bundle.VirtualPath
            .Replace("~/bundles/", "")
            .Replace("~/Bundle/", "")
            .Replace("~/", "")
            .Replace("/", "-");

        return name;
    }

    /// <summary>
    /// Converts a bundle definition to a Vite entry path.
    /// </summary>
    private static string ConvertBundleToEntryPath(BundleDefinition bundle)
    {
        // If the bundle has specific files, use the first one
        if (bundle.IncludedFiles.Count > 0)
        {
            var firstFile = bundle.IncludedFiles[0];
            return ConvertVirtualPath(firstFile, bundle.Type);
        }

        // Otherwise, create a default entry point based on the bundle name
        var entryName = GetEntryName(bundle);
        var extension = bundle.Type == BundleType.Script ? "js" : "css";
        var dir = bundle.Type == BundleType.Script ? "js" : "css";

        return $"src/{dir}/{entryName}.{extension}";
    }

    /// <summary>
    /// Converts ASP.NET virtual paths to modern directory structure.
    /// </summary>
    private static string ConvertVirtualPath(string virtualPath, BundleType type)
    {
        var path = virtualPath
            .Replace("~/Scripts/", "src/js/")
            .Replace("~/Content/", "src/css/")
            .Replace("~/", "src/");

        return path;
    }
}
