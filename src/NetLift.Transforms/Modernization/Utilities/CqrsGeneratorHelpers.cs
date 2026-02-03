using NetLift.Core.Models.Modernization;
using System;
using System.Linq;
using System.Text;

namespace NetLift.Transforms.Modernization.Utilities;

/// <summary>
/// Shared helper methods for CQRS Command and Query generators.
/// Extracted to eliminate code duplication between CommandGenerator and QueryGenerator.
/// </summary>
internal static class CqrsGeneratorHelpers
{
    private const string Indent = "    ";

    /// <summary>
    /// Infers the C# type from an assigned value expression.
    /// </summary>
    internal static string InferTypeFromAssignedValue(string assignedValue)
    {
        if (string.IsNullOrWhiteSpace(assignedValue))
            return "object?";

        var trimmed = assignedValue.Trim();

        // SelectList patterns
        if (trimmed.Contains("new SelectList", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains(".Select(", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("SelectListItem", StringComparison.OrdinalIgnoreCase))
        {
            return "IEnumerable<SelectListItem>";
        }

        // String literal
        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
        {
            return "string";
        }

        // Numeric literals
        if (int.TryParse(trimmed, out _))
        {
            return "int";
        }

        if (decimal.TryParse(trimmed, out _) || trimmed.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            return "decimal";
        }

        // Boolean
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return "bool";
        }

        // LINQ query (IEnumerable<T> or List<T>)
        if (trimmed.Contains(".ToList()", StringComparison.OrdinalIgnoreCase))
        {
            return "List<object>"; // TODO: Could be improved with type inference
        }

        if (trimmed.Contains(".AsEnumerable()", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains(".Where(", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains(".Select(", StringComparison.OrdinalIgnoreCase))
        {
            return "IEnumerable<object>"; // TODO: Could be improved with type inference
        }

        // Default fallback with nullable
        return "object?";
    }

    /// <summary>
    /// Escapes XML comment content to prevent XML parsing issues.
    /// </summary>
    internal static string EscapeXmlComment(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    /// <summary>
    /// Generates a response DTO record from ViewBag mutations.
    /// </summary>
    internal static void GenerateResponseDto(
        StringBuilder sb,
        string entityName,
        string requestName,
        System.Collections.Generic.IEnumerable<ViewModelMutation>? viewModelMutations)
    {
        var dtoName = $"{entityName}ResponseDto";

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Response DTO for {requestName} containing ViewBag/ViewData properties.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public record {dtoName}");
        sb.AppendLine("{");

        if (viewModelMutations != null)
        {
            foreach (var mutation in viewModelMutations)
            {
                var propertyType = InferTypeFromAssignedValue(mutation.AssignedValue);
                sb.AppendLine($"{Indent}/// <summary>");
                sb.AppendLine($"{Indent}/// Gets or initializes {mutation.PropertyName}.");

                if (propertyType == "object?")
                {
                    sb.AppendLine($"{Indent}/// TODO: Review type inference - assigned value: {EscapeXmlComment(mutation.AssignedValue)}");
                }

                sb.AppendLine($"{Indent}/// </summary>");
                sb.AppendLine($"{Indent}public {propertyType} {mutation.PropertyName} {{ get; set; }}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Extracts the entity name from a command or query name.
    /// Uses the more sophisticated logic from QueryGenerator that handles pluralization.
    /// </summary>
    internal static string ExtractEntityName(string requestName)
    {
        // Remove common prefixes and "Command"/"Query" suffix
        var entityName = requestName;

        // Command prefixes
        var commandPrefixes = new[] { "Create", "Update", "Delete", "Upsert", "Add", "Remove", "Edit", "Modify" };
        // Query prefixes
        var queryPrefixes = new[] { "Get", "List", "Find", "Search", "Fetch", "Load", "Retrieve" };

        var allPrefixes = commandPrefixes.Concat(queryPrefixes).ToArray();

        foreach (var prefix in allPrefixes)
        {
            if (entityName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                entityName = entityName[prefix.Length..];
                break;
            }
        }

        if (entityName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
        {
            entityName = entityName[..^7];
        }

        if (entityName.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            entityName = entityName[..^5];
        }

        // Remove "ById" suffix
        if (entityName.EndsWith("ById", StringComparison.OrdinalIgnoreCase))
        {
            entityName = entityName[..^4];
        }

        // Remove pluralization for single entity queries (but keep for list queries)
        if (entityName.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !entityName.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
        {
            // Keep plural for list queries - this is intentional
        }

        return string.IsNullOrWhiteSpace(entityName) ? "Entity" : entityName;
    }

    /// <summary>
    /// Transforms legacy ASP.NET MVC types to ASP.NET Core equivalents.
    /// </summary>
    internal static string TransformLegacyType(string type)
    {
        return type switch
        {
            "FormCollection" => "IFormCollection",
            "System.Web.Mvc.FormCollection" => "IFormCollection",
            "HttpPostedFileBase" => "IFormFile",
            "System.Web.HttpPostedFileBase" => "IFormFile",
            "HttpPostedFileBase[]" => "IFormFileCollection",
            "IEnumerable<HttpPostedFileBase>" => "IFormFileCollection",
            "SelectList" => "IEnumerable<SelectListItem>",
            "System.Web.Mvc.SelectList" => "IEnumerable<SelectListItem>",
            _ => type
        };
    }

    /// <summary>
    /// Formats a property type with nullability.
    /// </summary>
    internal static string FormatPropertyType(CommandProperty property)
    {
        var type = property.Type;

        // Transform legacy MVC types to modern equivalents
        type = TransformLegacyType(type);

        if (property.IsNullable && !type.EndsWith("?"))
        {
            type += "?";
        }

        return type;
    }

    /// <summary>
    /// Checks if the command uses types that require Microsoft.AspNetCore.Http namespace.
    /// Overload for CommandInfo.
    /// </summary>
    internal static bool RequiresAspNetCoreHttpUsing(CommandInfo commandInfo)
    {
        var httpTypes = new[] { "IFormFile", "IFormCollection", "IFormFileCollection", "HttpContext" };

        // Check properties
        foreach (var prop in commandInfo.Properties)
        {
            var transformedType = TransformLegacyType(prop.Type);
            if (httpTypes.Any(t => transformedType.Contains(t, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        // Check business logic for common HttpContext patterns
        if (!string.IsNullOrWhiteSpace(commandInfo.BusinessLogic))
        {
            if (commandInfo.BusinessLogic.Contains("HttpContext", StringComparison.Ordinal) ||
                commandInfo.BusinessLogic.Contains("IFormCollection", StringComparison.Ordinal) ||
                commandInfo.BusinessLogic.Contains("IFormFile", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the query uses types that require Microsoft.AspNetCore.Http namespace.
    /// Overload for QueryInfo.
    /// </summary>
    internal static bool RequiresAspNetCoreHttpUsing(QueryInfo queryInfo)
    {
        var httpTypes = new[] { "IFormFile", "IFormCollection", "IFormFileCollection", "HttpContext" };

        // Check properties
        foreach (var prop in queryInfo.Properties)
        {
            var transformedType = TransformLegacyType(prop.Type);
            if (httpTypes.Any(t => transformedType.Contains(t, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        // Check business logic for common HttpContext patterns
        if (!string.IsNullOrWhiteSpace(queryInfo.BusinessLogic))
        {
            if (queryInfo.BusinessLogic.Contains("HttpContext", StringComparison.Ordinal) ||
                queryInfo.BusinessLogic.Contains("IFormCollection", StringComparison.Ordinal) ||
                queryInfo.BusinessLogic.Contains("IFormFile", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts a string to PascalCase (first letter uppercase).
    /// </summary>
    internal static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Extracts the root namespace from a full namespace.
    /// Example: "MyApp.Application.Store.Commands" -> "MyApp"
    /// </summary>
    internal static string ExtractRootNamespace(string fullNamespace)
    {
        if (string.IsNullOrWhiteSpace(fullNamespace))
            return "Application";

        var parts = fullNamespace.Split('.');
        // Find where "Application" starts and return everything before it
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("Application", StringComparison.OrdinalIgnoreCase))
            {
                return i > 0 ? string.Join(".", parts.Take(i)) : parts[0];
            }
        }

        // If no Application found, return first part
        return parts[0];
    }

    /// <summary>
    /// Checks if code contains async method calls (methods ending with Async).
    /// </summary>
    internal static bool HasAsyncMethodCalls(string code)
    {
        // Look for method calls ending with Async(
        return System.Text.RegularExpressions.Regex.IsMatch(code, @"\w+Async\s*\(");
    }
}
