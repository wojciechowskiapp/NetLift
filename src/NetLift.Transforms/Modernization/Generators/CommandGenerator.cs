using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;
using System.Text;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Generates CQRS Command and CommandHandler classes from controller actions.
/// </summary>
public sealed class CommandGenerator : ICommandGenerator
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";

    /// <summary>
    /// Generates a Command class for an action.
    /// </summary>
    /// <param name="commandInfo">Information about the command to generate</param>
    /// <returns>Generated C# source code for the command class</returns>
    public string Generate(CommandInfo commandInfo)
    {
        ArgumentNullException.ThrowIfNull(commandInfo);

        var sb = new StringBuilder();

        // Add namespace
        sb.AppendLine($"namespace {commandInfo.Namespace};");
        sb.AppendLine();

        // Add usings (using lightweight MediatR replacement)
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        // Add Result, IApplicationDbContext, and Models usings
        var rootNamespace = ExtractRootNamespace(commandInfo.Namespace);
        sb.AppendLine($"using {rootNamespace}.Application.Common;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine($"using {rootNamespace}.Models;");

        // Add Microsoft.AspNetCore.Mvc.Rendering if ViewBag mutations exist (for SelectListItem)
        if (commandInfo.ViewModelMutations?.Any() == true)
        {
            sb.AppendLine("using Microsoft.AspNetCore.Mvc.Rendering;");
        }

        sb.AppendLine();

        // Generate Command record
        GenerateCommandRecord(sb, commandInfo);
        sb.AppendLine();

        // Generate Response DTO if ViewBag mutations exist
        if (commandInfo.ViewModelMutations?.Any() == true)
        {
            GenerateResponseDto(sb, commandInfo);
            sb.AppendLine();
        }

        // Generate Handler in same file
        GenerateHandlerClass(sb, commandInfo);

        return sb.ToString();
    }

    /// <summary>
    /// Generates a response DTO record from ViewBag mutations.
    /// </summary>
    private void GenerateResponseDto(StringBuilder sb, CommandInfo commandInfo)
    {
        var entityName = ExtractEntityName(commandInfo.Name);
        var dtoName = $"{entityName}ResponseDto";

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Response DTO for {commandInfo.Name} containing ViewBag/ViewData properties.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public record {dtoName}");
        sb.AppendLine("{");

        if (commandInfo.ViewModelMutations != null)
        {
            foreach (var mutation in commandInfo.ViewModelMutations)
            {
                var propertyType = InferTypeFromAssignedValue(mutation.AssignedValue);
                sb.AppendLine($"{Indent}/// <summary>");
                sb.AppendLine($"{Indent}/// Gets or initializes {mutation.PropertyName}.");

                if (propertyType == "object?")
                {
                    sb.AppendLine($"{Indent}/// TODO: Review type inference - assigned value: {EscapeXmlComment(mutation.AssignedValue)}");
                }

                sb.AppendLine($"{Indent}/// </summary>");
                sb.AppendLine($"{Indent}public {propertyType} {mutation.PropertyName} {{ get; init; }}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Infers the C# type from an assigned value expression.
    /// </summary>
    private static string InferTypeFromAssignedValue(string assignedValue)
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
    private static string EscapeXmlComment(string text)
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
    /// Generates only the Command record (for backwards compatibility).
    /// </summary>
    private void GenerateCommandRecord(StringBuilder sb, CommandInfo commandInfo)
    {
        // Add XML documentation
        sb.AppendLine("/// <summary>");

        var description = GenerateDescription(commandInfo);
        sb.AppendLine($"/// {description}");

        if (commandInfo.Confidence < 100)
        {
            sb.AppendLine($"/// Generated with {commandInfo.Confidence}% confidence from {commandInfo.Source.ControllerName}.{commandInfo.Source.ActionName}.");
        }

        sb.AppendLine("/// </summary>");

        // Generate record with properties
        if (commandInfo.Properties.Count == 0)
        {
            // No properties - simple command
            sb.AppendLine($"public record {commandInfo.Name} : IRequest<{commandInfo.ReturnType}>;");
        }
        else if (commandInfo.Properties.Count == 1)
        {
            // Single property - inline record
            var prop = commandInfo.Properties[0];
            var propType = FormatPropertyType(prop);
            var propName = ToPascalCase(prop.Name);
            sb.AppendLine($"public record {commandInfo.Name}({propType} {propName}) : IRequest<{commandInfo.ReturnType}>;");
        }
        else
        {
            // Multiple properties - multi-line record
            sb.AppendLine($"public record {commandInfo.Name}(");

            for (int i = 0; i < commandInfo.Properties.Count; i++)
            {
                var prop = commandInfo.Properties[i];
                var propType = FormatPropertyType(prop);
                var propName = ToPascalCase(prop.Name);
                var comma = i < commandInfo.Properties.Count - 1 ? "," : string.Empty;
                sb.AppendLine($"{Indent}{propType} {propName}{comma}");
            }

            sb.AppendLine($") : IRequest<{commandInfo.ReturnType}>;");
        }
    }

    /// <summary>
    /// Generates a CommandHandler class (for backwards compatibility - returns full file).
    /// </summary>
    /// <param name="commandInfo">Information about the command handler to generate</param>
    /// <returns>Generated C# source code for the command handler class</returns>
    public string GenerateHandler(CommandInfo commandInfo)
    {
        ArgumentNullException.ThrowIfNull(commandInfo);
        // Return empty since handler is now included in Generate()
        return string.Empty;
    }

    /// <summary>
    /// Generates the handler class content (without namespace/usings).
    /// </summary>
    private void GenerateHandlerClass(StringBuilder sb, CommandInfo commandInfo)
    {
        // Add XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Handles the {commandInfo.Name} command.");

        if (commandInfo.Confidence < 80)
        {
            sb.AppendLine($"/// TODO: Review implementation - generated with {commandInfo.Confidence}% confidence.");
        }

        sb.AppendLine("/// </summary>");

        // Generate handler class
        var handlerName = GetHandlerName(commandInfo.Name);
        sb.AppendLine($"public sealed class {handlerName} : IRequestHandler<{commandInfo.Name}, {commandInfo.ReturnType}>");
        sb.AppendLine("{");

        // Add dependencies (context field)
        sb.AppendLine($"{Indent}private readonly IApplicationDbContext _context;");
        sb.AppendLine();

        // Add constructor
        sb.AppendLine($"{Indent}public {handlerName}(IApplicationDbContext context)");
        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_context = context;");
        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();

        // Determine if we need async - either from IsAsync flag or if business logic contains await
        var needsAsync = commandInfo.IsAsync ||
            (!string.IsNullOrWhiteSpace(commandInfo.BusinessLogic) &&
             commandInfo.BusinessLogic.Contains("await ", StringComparison.Ordinal));

        // Add Handle method
        if (needsAsync)
        {
            sb.AppendLine($"{Indent}public async Task<{commandInfo.ReturnType}> Handle({commandInfo.Name} request, CancellationToken cancellationToken)");
        }
        else
        {
            sb.AppendLine($"{Indent}public Task<{commandInfo.ReturnType}> Handle({commandInfo.Name} request, CancellationToken cancellationToken)");
        }

        sb.AppendLine($"{Indent}{{");

        // Add implementation
        if (!string.IsNullOrWhiteSpace(commandInfo.BusinessLogic))
        {
            // Use provided business logic
            sb.AppendLine($"{DoubleIndent}// Business logic from {commandInfo.Source.ControllerName}.{commandInfo.Source.ActionName}");

            // If ViewBag mutations exist, declare result variable
            if (commandInfo.ViewModelMutations?.Any() == true)
            {
                var entityName = ExtractEntityName(commandInfo.Name);
                var dtoName = $"{entityName}ResponseDto";
                sb.AppendLine($"{DoubleIndent}var result = new {dtoName}();");
                sb.AppendLine();
            }

            // Indent the business logic
            var logicLines = commandInfo.BusinessLogic.Split('\n', StringSplitOptions.TrimEntries);
            foreach (var line in logicLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine($"{DoubleIndent}{line}");
                }
            }
        }
        else
        {
            // Generate placeholder implementation
            if (commandInfo.Confidence < 60)
            {
                sb.AppendLine($"{DoubleIndent}// TODO: Implement command handler logic");
                sb.AppendLine($"{DoubleIndent}// Original action: {commandInfo.Source.ControllerName}.{commandInfo.Source.ActionName}");
                sb.AppendLine($"{DoubleIndent}// Confidence: {commandInfo.Confidence}%");
                sb.AppendLine($"{DoubleIndent}throw new NotImplementedException(\"Command handler requires manual implementation\");");
            }
            else
            {
                // Generate basic CRUD-like implementation
                sb.AppendLine($"{DoubleIndent}// TODO: Review and adjust implementation as needed");
                sb.AppendLine();

                // Extract entity name from command (e.g., "CreateStudent" -> "Student")
                var entityName = ExtractEntityName(commandInfo.Name);

                if (commandInfo.Name.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
                {
                    GenerateCreateImplementation(sb, commandInfo, entityName);
                }
                else if (commandInfo.Name.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
                {
                    GenerateUpdateImplementation(sb, commandInfo, entityName);
                }
                else if (commandInfo.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase))
                {
                    GenerateDeleteImplementation(sb, commandInfo, entityName);
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}throw new NotImplementedException(\"Command handler requires manual implementation\");");
                }
            }
        }

        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");
    }

    private static string GenerateDescription(CommandInfo commandInfo)
    {
        // Try to infer description from command name
        if (commandInfo.Name.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = ExtractEntityName(commandInfo.Name);
            return $"Command to create a new {entityName}.";
        }
        else if (commandInfo.Name.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = ExtractEntityName(commandInfo.Name);
            return $"Command to update an existing {entityName}.";
        }
        else if (commandInfo.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = ExtractEntityName(commandInfo.Name);
            return $"Command to delete a {entityName}.";
        }

        return $"Command for {commandInfo.Source.ActionName} operation.";
    }

    private static string GetHandlerName(string commandName)
    {
        // Remove "Command" suffix if present
        if (commandName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
        {
            commandName = commandName[..^7]; // Remove "Command"
        }

        return $"{commandName}Handler";
    }

    private static string ExtractEntityName(string commandName)
    {
        // Remove common prefixes and "Command" suffix
        var entityName = commandName;

        var prefixes = new[] { "Create", "Update", "Delete", "Upsert", "Add", "Remove", "Edit", "Modify" };
        foreach (var prefix in prefixes)
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

        return string.IsNullOrWhiteSpace(entityName) ? "Entity" : entityName;
    }

    private static string FormatPropertyType(CommandProperty property)
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
    /// Converts a string to PascalCase (first letter uppercase).
    /// </summary>
    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Transforms legacy ASP.NET MVC types to ASP.NET Core equivalents.
    /// </summary>
    private static string TransformLegacyType(string type)
    {
        return type switch
        {
            "FormCollection" => "Dictionary<string, string>",
            "System.Web.Mvc.FormCollection" => "Dictionary<string, string>",
            "HttpPostedFileBase" => "IFormFile",
            "System.Web.HttpPostedFileBase" => "IFormFile",
            "HttpPostedFileBase[]" => "IFormFileCollection",
            "IEnumerable<HttpPostedFileBase>" => "IFormFileCollection",
            "SelectList" => "IEnumerable<SelectListItem>",
            "System.Web.Mvc.SelectList" => "IEnumerable<SelectListItem>",
            _ => type
        };
    }

    private static void GenerateCreateImplementation(StringBuilder sb, CommandInfo commandInfo, string entityName)
    {
        sb.AppendLine($"{DoubleIndent}var entity = new {entityName}");
        sb.AppendLine($"{DoubleIndent}{{");

        foreach (var prop in commandInfo.Properties)
        {
            sb.AppendLine($"{TripleIndent}{prop.Name} = request.{prop.Name},");
        }

        sb.AppendLine($"{DoubleIndent}}};");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}_context.{entityName}s.Add(entity);");

        if (commandInfo.IsAsync)
        {
            sb.AppendLine($"{DoubleIndent}await _context.SaveChangesAsync(cancellationToken);");
        }
        else
        {
            sb.AppendLine($"{DoubleIndent}_context.SaveChanges();");
        }

        sb.AppendLine();

        // Return appropriate result based on return type
        if (commandInfo.ReturnType.Contains("Result"))
        {
            if (commandInfo.ReturnType.Contains("<int>") || commandInfo.ReturnType.Contains("<Int32>"))
            {
                if (commandInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return Result<int>.Success(entity.Id);");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(Result<int>.Success(entity.Id));");
                }
            }
            else
            {
                if (commandInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return Result.Success();");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(Result.Success());");
                }
            }
        }
        else if (commandInfo.ReturnType == "Unit")
        {
            if (commandInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}return Unit.Value;");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}return Task.FromResult(Unit.Value);");
            }
        }
        else
        {
            if (commandInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}return entity.Id;");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}return Task.FromResult(entity.Id);");
            }
        }
    }

    private static void GenerateUpdateImplementation(StringBuilder sb, CommandInfo commandInfo, string entityName)
    {
        // Assume first property is Id
        var idProperty = commandInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        if (idProperty != null)
        {
            if (commandInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}var entity = await _context.{entityName}s.FindAsync(new object[] {{ request.{idProperty.Name} }}, cancellationToken);");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}var entity = _context.{entityName}s.Find(request.{idProperty.Name});");
            }

            sb.AppendLine();
            sb.AppendLine($"{DoubleIndent}if (entity == null)");
            sb.AppendLine($"{DoubleIndent}{{");

            if (commandInfo.ReturnType.Contains("Result"))
            {
                sb.AppendLine($"{TripleIndent}return Result.Failure(\"{entityName} not found\");");
            }
            else
            {
                sb.AppendLine($"{TripleIndent}throw new KeyNotFoundException(\"{entityName} not found\");");
            }

            sb.AppendLine($"{DoubleIndent}}}");
            sb.AppendLine();

            foreach (var prop in commandInfo.Properties.Where(p => p != idProperty))
            {
                sb.AppendLine($"{DoubleIndent}entity.{prop.Name} = request.{prop.Name};");
            }

            sb.AppendLine();

            if (commandInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}await _context.SaveChangesAsync(cancellationToken);");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}_context.SaveChanges();");
            }

            sb.AppendLine();

            if (commandInfo.ReturnType.Contains("Result"))
            {
                if (commandInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return Result.Success();");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(Result.Success());");
                }
            }
            else if (commandInfo.ReturnType == "Unit")
            {
                if (commandInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return Unit.Value;");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(Unit.Value);");
                }
            }
        }
        else
        {
            sb.AppendLine($"{DoubleIndent}throw new NotImplementedException(\"Update command requires Id property\");");
        }
    }

    private static void GenerateDeleteImplementation(StringBuilder sb, CommandInfo commandInfo, string entityName)
    {
        // Assume first property is Id
        var idProperty = commandInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        if (idProperty != null)
        {
            if (commandInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}var entity = await _context.{entityName}s.FindAsync(new object[] {{ request.{idProperty.Name} }}, cancellationToken);");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}var entity = _context.{entityName}s.Find(request.{idProperty.Name});");
            }

            sb.AppendLine();
            sb.AppendLine($"{DoubleIndent}if (entity == null)");
            sb.AppendLine($"{DoubleIndent}{{");

            if (commandInfo.ReturnType.Contains("Result"))
            {
                sb.AppendLine($"{TripleIndent}return Result.Failure(\"{entityName} not found\");");
            }
            else
            {
                sb.AppendLine($"{TripleIndent}throw new KeyNotFoundException(\"{entityName} not found\");");
            }

            sb.AppendLine($"{DoubleIndent}}}");
            sb.AppendLine();
            sb.AppendLine($"{DoubleIndent}_context.{entityName}s.Remove(entity);");

            if (commandInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}await _context.SaveChangesAsync(cancellationToken);");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}_context.SaveChanges();");
            }

            sb.AppendLine();

            if (commandInfo.ReturnType.Contains("Result"))
            {
                if (commandInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return Result.Success();");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(Result.Success());");
                }
            }
            else if (commandInfo.ReturnType == "Unit")
            {
                if (commandInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return Unit.Value;");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(Unit.Value);");
                }
            }
        }
        else
        {
            sb.AppendLine($"{DoubleIndent}throw new NotImplementedException(\"Delete command requires Id property\");");
        }
    }

    /// <summary>
    /// Extracts the root namespace from a full namespace (e.g., "MyApp.Application.Store.Commands" -> "MyApp")
    /// </summary>
    private static string ExtractRootNamespace(string fullNamespace)
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
}
