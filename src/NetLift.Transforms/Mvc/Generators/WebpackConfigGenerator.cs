using System.Text;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Mvc;

namespace NetLift.Transforms.Mvc.Generators;

/// <summary>
/// Generates Webpack configuration files from bundle definitions.
/// </summary>
public sealed class WebpackConfigGenerator : IWebpackConfigGenerator
{
    /// <inheritdoc />
    public string Generate(IEnumerable<BundleDefinition> bundles)
    {
        if (bundles == null)
            throw new ArgumentNullException(nameof(bundles));

        var bundleList = bundles.ToList();
        var sb = new StringBuilder();

        sb.AppendLine("const path = require('path');");
        sb.AppendLine("const MiniCssExtractPlugin = require('mini-css-extract-plugin');");
        sb.AppendLine("const { WebpackManifestPlugin } = require('webpack-manifest-plugin');");
        sb.AppendLine();
        sb.AppendLine("module.exports = {");
        sb.AppendLine("  mode: process.env.NODE_ENV === 'production' ? 'production' : 'development',");
        sb.AppendLine("  entry: {");

        // Generate entry points from bundles
        var entries = new List<string>();
        foreach (var bundle in bundleList)
        {
            var entryName = GetEntryName(bundle);
            var entryPath = ConvertBundleToEntryPath(bundle);
            entries.Add($"    {entryName}: path.resolve(__dirname, '{entryPath}')");
        }

        sb.AppendLine(string.Join(",\n", entries));
        sb.AppendLine("  },");
        sb.AppendLine("  output: {");
        sb.AppendLine("    path: path.resolve(__dirname, 'wwwroot/dist'),");
        sb.AppendLine("    filename: 'js/[name].[contenthash].js',");
        sb.AppendLine("    clean: true");
        sb.AppendLine("  },");
        sb.AppendLine("  module: {");
        sb.AppendLine("    rules: [");
        sb.AppendLine("      {");
        sb.AppendLine("        test: /\\.css$/,");
        sb.AppendLine("        use: [MiniCssExtractPlugin.loader, 'css-loader']");
        sb.AppendLine("      },");
        sb.AppendLine("      {");
        sb.AppendLine("        test: /\\.s[ac]ss$/,");
        sb.AppendLine("        use: [MiniCssExtractPlugin.loader, 'css-loader', 'sass-loader']");
        sb.AppendLine("      },");
        sb.AppendLine("      {");
        sb.AppendLine("        test: /\\.(png|svg|jpg|jpeg|gif|woff|woff2|eot|ttf|otf)$/,");
        sb.AppendLine("        type: 'asset/resource',");
        sb.AppendLine("        generator: {");
        sb.AppendLine("          filename: 'assets/[name].[hash][ext]'");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  },");
        sb.AppendLine("  plugins: [");
        sb.AppendLine("    new MiniCssExtractPlugin({");
        sb.AppendLine("      filename: 'css/[name].[contenthash].css'");
        sb.AppendLine("    }),");
        sb.AppendLine("    new WebpackManifestPlugin({");
        sb.AppendLine("      fileName: 'manifest.json'");
        sb.AppendLine("    })");
        sb.AppendLine("  ],");
        sb.AppendLine("  resolve: {");
        sb.AppendLine("    alias: {");
        sb.AppendLine("      '@': path.resolve(__dirname, 'src'),");
        sb.AppendLine("      '~/Scripts': path.resolve(__dirname, 'src/js'),");
        sb.AppendLine("      '~/Content': path.resolve(__dirname, 'src/css')");
        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine("  devtool: process.env.NODE_ENV === 'production' ? 'source-map' : 'eval-source-map'");
        sb.Append("};");

        return sb.ToString();
    }

    /// <summary>
    /// Gets a clean entry name from the bundle's virtual path.
    /// </summary>
    private static string GetEntryName(BundleDefinition bundle)
    {
        var name = bundle.VirtualPath
            .Replace("~/bundles/", "")
            .Replace("~/Bundle/", "")
            .Replace("~/", "")
            .Replace("/", "-");

        return name;
    }

    /// <summary>
    /// Converts a bundle definition to a Webpack entry path.
    /// </summary>
    private static string ConvertBundleToEntryPath(BundleDefinition bundle)
    {
        if (bundle.IncludedFiles.Count > 0)
        {
            var firstFile = bundle.IncludedFiles[0];
            return ConvertVirtualPath(firstFile, bundle.Type);
        }

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
