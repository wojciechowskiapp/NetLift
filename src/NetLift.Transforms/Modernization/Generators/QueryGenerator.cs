using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;
using System.Text;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Generates CQRS Query and QueryHandler classes from controller actions.
/// </summary>
public sealed class QueryGenerator : IQueryGenerator
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";

    /// <summary>
    /// Generates a Query class for an action.
    /// </summary>
    /// <param name="queryInfo">Information about the query to generate</param>
    /// <returns>Generated C# source code for the query class</returns>
    public string Generate(QueryInfo queryInfo)
    {
        ArgumentNullException.ThrowIfNull(queryInfo);

        var sb = new StringBuilder();

        // Add namespace
        sb.AppendLine($"namespace {queryInfo.Namespace};");
        sb.AppendLine();

        // Add usings (using lightweight MediatR replacement)
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        // Add Result and IApplicationDbContext usings
        var rootNamespace = ExtractRootNamespace(queryInfo.Namespace);
        sb.AppendLine($"using {rootNamespace}.Application.Common;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine($"using {rootNamespace}.Models;");

        // Add Microsoft.AspNetCore.Mvc.Rendering if ViewBag mutations exist (for SelectListItem)
        if (queryInfo.ViewModelMutations?.Any() == true)
        {
            sb.AppendLine("using Microsoft.AspNetCore.Mvc.Rendering;");
        }

        sb.AppendLine();

        // Generate Query record
        GenerateQueryRecord(sb, queryInfo);
        sb.AppendLine();

        // Generate Response DTO if ViewBag mutations exist
        if (queryInfo.ViewModelMutations?.Any() == true)
        {
            GenerateResponseDto(sb, queryInfo);
            sb.AppendLine();
        }

        // Generate Handler in same file
        GenerateHandlerClass(sb, queryInfo);

        return sb.ToString();
    }

    /// <summary>
    /// Generates a response DTO record from ViewBag mutations.
    /// </summary>
    private void GenerateResponseDto(StringBuilder sb, QueryInfo queryInfo)
    {
        var entityName = ExtractEntityName(queryInfo.Name);
        var dtoName = $"{entityName}ResponseDto";

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Response DTO for {queryInfo.Name} containing ViewBag/ViewData properties.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public record {dtoName}");
        sb.AppendLine("{");

        if (queryInfo.ViewModelMutations != null)
        {
            foreach (var mutation in queryInfo.ViewModelMutations)
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
    /// Generates only the Query record (for backwards compatibility).
    /// </summary>
    private void GenerateQueryRecord(StringBuilder sb, QueryInfo queryInfo)
    {
        // Add XML documentation
        sb.AppendLine("/// <summary>");

        var description = GenerateDescription(queryInfo);
        sb.AppendLine($"/// {description}");

        if (queryInfo.Confidence < 100)
        {
            sb.AppendLine($"/// Generated with {queryInfo.Confidence}% confidence from {queryInfo.Source.ControllerName}.{queryInfo.Source.ActionName}.");
        }

        sb.AppendLine("/// </summary>");

        // Generate record with properties
        if (queryInfo.Properties.Count == 0)
        {
            // No properties - simple query
            sb.AppendLine($"public record {queryInfo.Name} : IRequest<{queryInfo.ReturnType}>;");
        }
        else if (queryInfo.Properties.Count == 1)
        {
            // Single property - inline record
            var prop = queryInfo.Properties[0];
            var propType = FormatPropertyType(prop);
            var propName = ToPascalCase(prop.Name);
            sb.AppendLine($"public record {queryInfo.Name}({propType} {propName}) : IRequest<{queryInfo.ReturnType}>;");
        }
        else
        {
            // Multiple properties - multi-line record
            // Sort properties so parameters with defaults come AFTER parameters without (C# requirement)
            var sortedProperties = queryInfo.Properties
                .OrderBy(p => !string.IsNullOrEmpty(GetDefaultValue(p, queryInfo)) ? 1 : 0)
                .ToList();

            sb.AppendLine($"public record {queryInfo.Name}(");

            for (int i = 0; i < sortedProperties.Count; i++)
            {
                var prop = sortedProperties[i];
                var propType = FormatPropertyType(prop);
                var propName = ToPascalCase(prop.Name);
                var comma = i < sortedProperties.Count - 1 ? "," : string.Empty;

                // Add default value for pagination parameters
                var defaultValue = GetDefaultValue(prop, queryInfo);
                var defaultSuffix = !string.IsNullOrEmpty(defaultValue) ? $" = {defaultValue}" : string.Empty;

                sb.AppendLine($"{Indent}{propType} {propName}{defaultSuffix}{comma}");
            }

            sb.AppendLine($") : IRequest<{queryInfo.ReturnType}>;");
        }
    }

    /// <summary>
    /// Generates a QueryHandler class (for backwards compatibility - returns full file).
    /// </summary>
    /// <param name="queryInfo">Information about the query handler to generate</param>
    /// <returns>Generated C# source code for the query handler class</returns>
    public string GenerateHandler(QueryInfo queryInfo)
    {
        ArgumentNullException.ThrowIfNull(queryInfo);
        // Return empty since handler is now included in Generate()
        return string.Empty;
    }

    /// <summary>
    /// Generates the handler class content (without namespace/usings).
    /// </summary>
    private void GenerateHandlerClass(StringBuilder sb, QueryInfo queryInfo)
    {
        // Add XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Handles the {queryInfo.Name} query.");

        if (queryInfo.Confidence < 80)
        {
            sb.AppendLine($"/// TODO: Review implementation - generated with {queryInfo.Confidence}% confidence.");
        }

        sb.AppendLine("/// </summary>");

        // Generate handler class
        var handlerName = GetHandlerName(queryInfo.Name);
        sb.AppendLine($"public sealed class {handlerName} : IRequestHandler<{queryInfo.Name}, {queryInfo.ReturnType}>");
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
        var needsAsync = queryInfo.IsAsync ||
            (!string.IsNullOrWhiteSpace(queryInfo.BusinessLogic) &&
             queryInfo.BusinessLogic.Contains("await ", StringComparison.Ordinal));

        // Add Handle method
        if (needsAsync)
        {
            sb.AppendLine($"{Indent}public async Task<{queryInfo.ReturnType}> Handle({queryInfo.Name} request, CancellationToken cancellationToken)");
        }
        else
        {
            sb.AppendLine($"{Indent}public Task<{queryInfo.ReturnType}> Handle({queryInfo.Name} request, CancellationToken cancellationToken)");
        }

        sb.AppendLine($"{Indent}{{");

        // Add implementation
        if (!string.IsNullOrWhiteSpace(queryInfo.BusinessLogic))
        {
            // Use provided business logic
            sb.AppendLine($"{DoubleIndent}// Business logic from {queryInfo.Source.ControllerName}.{queryInfo.Source.ActionName}");

            // If ViewBag mutations exist, declare result variable
            if (queryInfo.ViewModelMutations?.Any() == true)
            {
                var entityName = ExtractEntityName(queryInfo.Name);
                var dtoName = $"{entityName}ResponseDto";
                sb.AppendLine($"{DoubleIndent}var result = new {dtoName}();");
                sb.AppendLine();
            }

            // Indent the business logic
            var logicLines = queryInfo.BusinessLogic.Split('\n', StringSplitOptions.TrimEntries);
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
            if (queryInfo.Confidence < 60)
            {
                sb.AppendLine($"{DoubleIndent}// TODO: Implement query handler logic");
                sb.AppendLine($"{DoubleIndent}// Original action: {queryInfo.Source.ControllerName}.{queryInfo.Source.ActionName}");
                sb.AppendLine($"{DoubleIndent}// Confidence: {queryInfo.Confidence}%");
                sb.AppendLine($"{DoubleIndent}throw new NotImplementedException(\"Query handler requires manual implementation\");");
            }
            else
            {
                // Generate basic query implementation
                sb.AppendLine($"{DoubleIndent}// TODO: Review and adjust implementation as needed");
                sb.AppendLine();

                // Extract entity name from query (e.g., "GetStudent" -> "Student")
                var entityName = ExtractEntityName(queryInfo.Name);

                if (queryInfo.Name.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
                {
                    if (queryInfo.Name.Contains("ById", StringComparison.OrdinalIgnoreCase) ||
                        queryInfo.Properties.Any(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)))
                    {
                        GenerateGetByIdImplementation(sb, queryInfo, entityName);
                    }
                    else if (queryInfo.SupportsPagination)
                    {
                        GenerateGetListWithPaginationImplementation(sb, queryInfo, entityName);
                    }
                    else
                    {
                        GenerateGetListImplementation(sb, queryInfo, entityName);
                    }
                }
                else if (queryInfo.Name.StartsWith("List", StringComparison.OrdinalIgnoreCase))
                {
                    if (queryInfo.SupportsPagination)
                    {
                        GenerateGetListWithPaginationImplementation(sb, queryInfo, entityName);
                    }
                    else
                    {
                        GenerateGetListImplementation(sb, queryInfo, entityName);
                    }
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}throw new NotImplementedException(\"Query handler requires manual implementation\");");
                }
            }
        }

        sb.AppendLine($"{Indent}}}");
        sb.AppendLine("}");
    }

    private static string GenerateDescription(QueryInfo queryInfo)
    {
        // Try to infer description from query name
        if (queryInfo.Name.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = ExtractEntityName(queryInfo.Name);

            if (queryInfo.Name.Contains("ById", StringComparison.OrdinalIgnoreCase))
            {
                return $"Query to get a {entityName} by ID.";
            }
            else if (queryInfo.SupportsPagination)
            {
                return $"Query to get a paginated list of {entityName}s.";
            }
            else
            {
                return $"Query to get {entityName}s.";
            }
        }
        else if (queryInfo.Name.StartsWith("List", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = ExtractEntityName(queryInfo.Name);
            return $"Query to list {entityName}s.";
        }

        return $"Query for {queryInfo.Source.ActionName} operation.";
    }

    private static string GetHandlerName(string queryName)
    {
        // Remove "Query" suffix if present
        if (queryName.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            queryName = queryName[..^5]; // Remove "Query"
        }

        return $"{queryName}Handler";
    }

    private static string ExtractEntityName(string queryName)
    {
        // Remove common prefixes and "Query" suffix
        var entityName = queryName;

        var prefixes = new[] { "Get", "List", "Find", "Search", "Fetch", "Load", "Retrieve" };
        foreach (var prefix in prefixes)
        {
            if (entityName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                entityName = entityName[prefix.Length..];
                break;
            }
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

        // Remove pluralization for single entity queries
        if (entityName.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !entityName.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
        {
            // Keep plural for list queries
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

    private static string GetDefaultValue(CommandProperty property, QueryInfo queryInfo)
    {
        // Add default values for common pagination parameters
        if (queryInfo.SupportsPagination)
        {
            if (property.Name.Equals("PageNumber", StringComparison.OrdinalIgnoreCase))
            {
                return "1";
            }
            else if (property.Name.Equals("PageSize", StringComparison.OrdinalIgnoreCase))
            {
                return "10";
            }
        }

        return string.Empty;
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

    private static void GenerateGetByIdImplementation(StringBuilder sb, QueryInfo queryInfo, string entityName)
    {
        // Find the ID property
        var idProperty = queryInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        if (idProperty != null)
        {
            sb.AppendLine($"{DoubleIndent}var query = _context.{entityName}s.AsQueryable();");
            sb.AppendLine();

            if (queryInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}var entity = await query");
                sb.AppendLine($"{TripleIndent}.FirstOrDefaultAsync(e => e.Id == request.{idProperty.Name}, cancellationToken);");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}var entity = query");
                sb.AppendLine($"{TripleIndent}.FirstOrDefault(e => e.Id == request.{idProperty.Name});");
            }

            sb.AppendLine();
            sb.AppendLine($"{DoubleIndent}if (entity == null)");
            sb.AppendLine($"{DoubleIndent}{{");

            if (queryInfo.ReturnType.Contains("Result"))
            {
                sb.AppendLine($"{TripleIndent}return Result<{entityName}Dto>.Failure(\"{entityName} not found\");");
            }
            else
            {
                sb.AppendLine($"{TripleIndent}throw new KeyNotFoundException(\"{entityName} not found\");");
            }

            sb.AppendLine($"{DoubleIndent}}}");
            sb.AppendLine();

            // Map to DTO
            if (queryInfo.ReturnType.Contains("Dto"))
            {
                sb.AppendLine($"{DoubleIndent}var dto = new {entityName}Dto");
                sb.AppendLine($"{DoubleIndent}{{");
                sb.AppendLine($"{TripleIndent}// TODO: Map entity properties to DTO");
                sb.AppendLine($"{TripleIndent}// Id = entity.Id,");
                sb.AppendLine($"{DoubleIndent}}};");
                sb.AppendLine();

                if (queryInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return dto;");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(dto);");
                }
            }
            else
            {
                if (queryInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return entity;");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(entity);");
                }
            }
        }
        else
        {
            sb.AppendLine($"{DoubleIndent}throw new NotImplementedException(\"GetById query requires Id property\");");
        }
    }

    private static void GenerateGetListImplementation(StringBuilder sb, QueryInfo queryInfo, string entityName)
    {
        sb.AppendLine($"{DoubleIndent}var query = _context.{entityName}s.AsQueryable();");
        sb.AppendLine();

        // Add filtering logic if supported
        if (queryInfo.SupportsFiltering)
        {
            GenerateFilteringLogic(sb, queryInfo);
        }

        // Map to DTO if needed
        if (queryInfo.ReturnType.Contains("Dto"))
        {
            if (queryInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}return await query");
                sb.AppendLine($"{TripleIndent}.Select(e => new {entityName}Dto");
                sb.AppendLine($"{TripleIndent}{{");
                sb.AppendLine($"{TripleIndent}{Indent}// TODO: Map entity properties to DTO");
                sb.AppendLine($"{TripleIndent}{Indent}// Id = e.Id,");
                sb.AppendLine($"{TripleIndent}}})");
                sb.AppendLine($"{TripleIndent}.ToListAsync(cancellationToken);");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}var result = query");
                sb.AppendLine($"{TripleIndent}.Select(e => new {entityName}Dto");
                sb.AppendLine($"{TripleIndent}{{");
                sb.AppendLine($"{TripleIndent}{Indent}// TODO: Map entity properties to DTO");
                sb.AppendLine($"{TripleIndent}{Indent}// Id = e.Id,");
                sb.AppendLine($"{TripleIndent}}})");
                sb.AppendLine($"{TripleIndent}.ToList();");
                sb.AppendLine();
                sb.AppendLine($"{DoubleIndent}return Task.FromResult(result);");
            }
        }
        else
        {
            if (queryInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}return await query.ToListAsync(cancellationToken);");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}return Task.FromResult(query.ToList());");
            }
        }
    }

    private static void GenerateGetListWithPaginationImplementation(StringBuilder sb, QueryInfo queryInfo, string entityName)
    {
        sb.AppendLine($"{DoubleIndent}var query = _context.{entityName}s.AsQueryable();");
        sb.AppendLine();

        // Add filtering logic if supported
        if (queryInfo.SupportsFiltering)
        {
            GenerateFilteringLogic(sb, queryInfo);
        }

        // Add sorting logic
        var sortOrderProperty = queryInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("SortOrder", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("OrderBy", StringComparison.OrdinalIgnoreCase));

        if (sortOrderProperty != null)
        {
            sb.AppendLine($"{DoubleIndent}// Apply sorting");
            sb.AppendLine($"{DoubleIndent}query = request.{sortOrderProperty.Name}?.ToLower() switch");
            sb.AppendLine($"{DoubleIndent}{{");
            sb.AppendLine($"{TripleIndent}// TODO: Add sorting options");
            sb.AppendLine($"{TripleIndent}// \"name_desc\" => query.OrderByDescending(e => e.Name),");
            sb.AppendLine($"{TripleIndent}// \"name\" => query.OrderBy(e => e.Name),");
            sb.AppendLine($"{TripleIndent}_ => query.OrderBy(e => e.Id)");
            sb.AppendLine($"{DoubleIndent}}};");
            sb.AppendLine();
        }

        // Find pagination parameters
        var pageNumberProperty = queryInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("PageNumber", StringComparison.OrdinalIgnoreCase));
        var pageSizeProperty = queryInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("PageSize", StringComparison.OrdinalIgnoreCase));

        if (pageNumberProperty != null && pageSizeProperty != null)
        {
            // Map to DTO if needed
            if (queryInfo.ReturnType.Contains("Dto"))
            {
                if (queryInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return await query");
                    sb.AppendLine($"{TripleIndent}.Select(e => new {entityName}Dto");
                    sb.AppendLine($"{TripleIndent}{{");
                    sb.AppendLine($"{TripleIndent}{Indent}// TODO: Map entity properties to DTO");
                    sb.AppendLine($"{TripleIndent}{Indent}// Id = e.Id,");
                    sb.AppendLine($"{TripleIndent}}})");
                    sb.AppendLine($"{TripleIndent}.ToPagedListAsync(request.{pageNumberProperty.Name}, request.{pageSizeProperty.Name}, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}var items = query");
                    sb.AppendLine($"{TripleIndent}.Select(e => new {entityName}Dto");
                    sb.AppendLine($"{TripleIndent}{{");
                    sb.AppendLine($"{TripleIndent}{Indent}// TODO: Map entity properties to DTO");
                    sb.AppendLine($"{TripleIndent}{Indent}// Id = e.Id,");
                    sb.AppendLine($"{TripleIndent}}})");
                    sb.AppendLine($"{TripleIndent}.ToPagedList(request.{pageNumberProperty.Name}, request.{pageSizeProperty.Name});");
                    sb.AppendLine();
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(items);");
                }
            }
            else
            {
                if (queryInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return await query.ToPagedListAsync(request.{pageNumberProperty.Name}, request.{pageSizeProperty.Name}, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(query.ToPagedList(request.{pageNumberProperty.Name}, request.{pageSizeProperty.Name}));");
                }
            }
        }
        else
        {
            // Fallback to non-paginated list
            GenerateGetListImplementation(sb, queryInfo, entityName);
        }
    }

    private static void GenerateFilteringLogic(StringBuilder sb, QueryInfo queryInfo)
    {
        // Find common filter properties
        var searchProperty = queryInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("SearchString", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("Search", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("Query", StringComparison.OrdinalIgnoreCase));

        if (searchProperty != null)
        {
            sb.AppendLine($"{DoubleIndent}// Apply search filter");
            sb.AppendLine($"{DoubleIndent}if (!string.IsNullOrEmpty(request.{searchProperty.Name}))");
            sb.AppendLine($"{DoubleIndent}{{");
            sb.AppendLine($"{TripleIndent}query = query.Where(e =>");
            sb.AppendLine($"{TripleIndent}{Indent}// TODO: Add search logic for relevant properties");
            sb.AppendLine($"{TripleIndent}{Indent}// e.Name.Contains(request.{searchProperty.Name}) ||");
            sb.AppendLine($"{TripleIndent}{Indent}// e.Description.Contains(request.{searchProperty.Name})");
            sb.AppendLine($"{TripleIndent}{Indent}true");
            sb.AppendLine($"{TripleIndent});");
            sb.AppendLine($"{DoubleIndent}}}");
            sb.AppendLine();
        }

        // Add other filter properties
        var otherFilterProperties = queryInfo.Properties.Where(p =>
            !p.Name.Equals("SearchString", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Equals("Search", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Equals("Query", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Equals("PageNumber", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Equals("PageSize", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Equals("SortOrder", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Equals("OrderBy", StringComparison.OrdinalIgnoreCase) &&
            p.IsNullable);

        foreach (var filterProp in otherFilterProperties)
        {
            sb.AppendLine($"{DoubleIndent}// Apply {filterProp.Name} filter");
            sb.AppendLine($"{DoubleIndent}if (request.{filterProp.Name}.HasValue)");
            sb.AppendLine($"{DoubleIndent}{{");
            sb.AppendLine($"{TripleIndent}query = query.Where(e => e.{filterProp.Name} == request.{filterProp.Name}.Value);");
            sb.AppendLine($"{DoubleIndent}}}");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Extracts the root namespace from a full namespace (e.g., "MyApp.Application.Store.Queries" -> "MyApp")
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
