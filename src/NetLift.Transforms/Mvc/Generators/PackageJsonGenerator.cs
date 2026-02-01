using System.Text;
using NetLift.Core.Interfaces;

namespace NetLift.Transforms.Mvc.Generators;

/// <summary>
/// Generates package.json files for modern JavaScript build tools.
/// </summary>
public sealed class PackageJsonGenerator : IPackageJsonGenerator
{
    /// <inheritdoc />
    public string Generate(bool useVite = true)
    {
        var sb = new StringBuilder();

        sb.AppendLine("{");
        sb.AppendLine("  \"name\": \"netlift-migrated-app\",");
        sb.AppendLine("  \"version\": \"1.0.0\",");
        sb.AppendLine("  \"private\": true,");
        sb.AppendLine("  \"type\": \"module\",");
        sb.AppendLine("  \"scripts\": {");

        if (useVite)
        {
            sb.AppendLine("    \"dev\": \"vite\",");
            sb.AppendLine("    \"build\": \"vite build\",");
            sb.AppendLine("    \"preview\": \"vite preview\",");
            sb.AppendLine("    \"watch\": \"vite build --watch\"");
        }
        else
        {
            sb.AppendLine("    \"dev\": \"webpack serve --mode development\",");
            sb.AppendLine("    \"build\": \"webpack --mode production\",");
            sb.AppendLine("    \"watch\": \"webpack --watch --mode development\"");
        }

        sb.AppendLine("  },");
        sb.AppendLine("  \"devDependencies\": {");

        if (useVite)
        {
            sb.AppendLine("    \"vite\": \"^5.0.0\",");
            sb.AppendLine("    \"@vitejs/plugin-legacy\": \"^5.0.0\"");
        }
        else
        {
            sb.AppendLine("    \"webpack\": \"^5.89.0\",");
            sb.AppendLine("    \"webpack-cli\": \"^5.1.4\",");
            sb.AppendLine("    \"webpack-dev-server\": \"^4.15.1\",");
            sb.AppendLine("    \"webpack-manifest-plugin\": \"^5.0.0\",");
            sb.AppendLine("    \"mini-css-extract-plugin\": \"^2.7.6\",");
            sb.AppendLine("    \"css-loader\": \"^6.8.1\",");
            sb.AppendLine("    \"sass-loader\": \"^13.3.2\",");
            sb.AppendLine("    \"sass\": \"^1.69.5\"");
        }

        sb.AppendLine("  },");
        sb.AppendLine("  \"dependencies\": {");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
