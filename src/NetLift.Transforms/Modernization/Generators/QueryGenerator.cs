using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;
using NetLift.Transforms.Modernization.Processors;
using NetLift.Transforms.Modernization.Utilities;
using System;
using System.Linq;
using System.Text;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Generates production-ready, optimized CQRS Query and QueryHandler classes.
/// Includes AsNoTracking, projection, logging, and caching support.
/// </summary>
public sealed class QueryGenerator : IQueryGenerator
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";
    private const string QuadIndent = "                ";

    /// <summary>
    /// Options for query generation.
    /// </summary>
    public bool IncludeLogger { get; set; } = true;
    public bool IncludeMapper { get; set; } = false;  // Default false - AutoMapper has commercial license
    public bool UseAsNoTracking { get; set; } = true;
    public bool UseProjectTo { get; set; } = false;   // Default false - use manual .Select() projection
    public bool IncludeConfigureAwait { get; set; } = true;
    public bool IncludeCachingSupport { get; set; } = false;

    /// <summary>
    /// Generates a Query class for an action.
    /// </summary>
    /// <param name="queryInfo">Information about the query to generate</param>
    /// <returns>Generated C# source code for the query class</returns>
    public string Generate(QueryInfo queryInfo)
    {
        ArgumentNullException.ThrowIfNull(queryInfo);

        // Determine if async based on IsAsync flag OR if business logic contains async method calls
        // This must be done BEFORE processing to ensure EnsureAsyncAwait is called
        var needsAsync = queryInfo.IsAsync || CqrsGeneratorHelpers.HasAsyncMethodCalls(queryInfo.BusinessLogic ?? string.Empty);

        // Process business logic first to detect dependencies and fix issues
        var processedLogic = BusinessLogicProcessor.Process(queryInfo.BusinessLogic, needsAsync);

        var sb = new StringBuilder();

        // Add namespace
        sb.AppendLine($"namespace {queryInfo.Namespace};");
        sb.AppendLine();

        // Add usings (production-ready optimized queries)
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");

        // Add core usings
        var rootNamespace = CqrsGeneratorHelpers.ExtractRootNamespace(queryInfo.Namespace);
        sb.AppendLine($"using {rootNamespace}.Application.Common;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Extensions;");
        sb.AppendLine($"using {rootNamespace}.Models;");

        // Add production feature usings
        if (IncludeLogger)
        {
            sb.AppendLine("using Microsoft.Extensions.Logging;");
        }

        if (IncludeMapper && UseProjectTo)
        {
            sb.AppendLine("using AutoMapper;");
            sb.AppendLine("using AutoMapper.QueryableExtensions;");
        }

        // Add Microsoft.AspNetCore.Mvc.Rendering if ViewBag mutations exist (for SelectListItem)
        if (queryInfo.ViewModelMutations?.Any() == true)
        {
            sb.AppendLine("using Microsoft.AspNetCore.Mvc.Rendering;");
        }

        // Add Microsoft.AspNetCore.Http if any property uses ASP.NET Core Http types
        if (CqrsGeneratorHelpers.RequiresAspNetCoreHttpUsing(queryInfo))
        {
            sb.AppendLine("using Microsoft.AspNetCore.Http;");
        }

        // Add usings detected from business logic
        foreach (var requiredUsing in processedLogic.RequiredUsings)
        {
            sb.AppendLine($"using {requiredUsing};");
        }

        sb.AppendLine();

        // Generate Query record
        GenerateQueryRecord(sb, queryInfo);
        sb.AppendLine();

        // Generate Response DTO if ViewBag mutations exist
        if (queryInfo.ViewModelMutations?.Any() == true)
        {
            var entityName = CqrsGeneratorHelpers.ExtractEntityName(queryInfo.Name);
            CqrsGeneratorHelpers.GenerateResponseDto(sb, entityName, queryInfo.Name, queryInfo.ViewModelMutations);
            sb.AppendLine();
        }

        // Generate Handler in same file
        GenerateHandlerClass(sb, queryInfo, processedLogic);

        return sb.ToString();
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
            var propType = CqrsGeneratorHelpers.FormatPropertyType(prop);
            var propName = CqrsGeneratorHelpers.ToPascalCase(prop.Name);
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
                var propType = CqrsGeneratorHelpers.FormatPropertyType(prop);
                var propName = CqrsGeneratorHelpers.ToPascalCase(prop.Name);
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
    private void GenerateHandlerClass(StringBuilder sb, QueryInfo queryInfo, ProcessedBusinessLogic? processedLogic)
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

        // Add caching interface if supported
        if (IncludeCachingSupport)
        {
            sb.AppendLine($"public sealed class {handlerName} : IRequestHandler<{queryInfo.Name}, {queryInfo.ReturnType}>");
        }
        else
        {
            sb.AppendLine($"public sealed class {handlerName} : IRequestHandler<{queryInfo.Name}, {queryInfo.ReturnType}>");
        }

        sb.AppendLine("{");

        // Add dependencies (fields)
        sb.AppendLine($"{Indent}private readonly IApplicationDbContext _context;");

        if (IncludeLogger)
        {
            sb.AppendLine($"{Indent}private readonly ILogger<{handlerName}> _logger;");
        }

        if (IncludeMapper && UseProjectTo)
        {
            sb.AppendLine($"{Indent}private readonly IMapper _mapper;");
        }

        // Add detected dependencies from business logic
        if (processedLogic != null)
        {
            foreach (var dep in processedLogic.RequiredDependencies)
            {
                sb.AppendLine($"{Indent}private readonly {dep.InterfaceType} {dep.FieldName};");
            }
        }

        sb.AppendLine();

        // Add constructor with all dependencies
        sb.Append($"{Indent}public {handlerName}(");
        var constructorParams = new List<string> { "IApplicationDbContext context" };

        if (IncludeLogger)
        {
            constructorParams.Add($"ILogger<{handlerName}> logger");
        }

        if (IncludeMapper && UseProjectTo)
        {
            constructorParams.Add("IMapper mapper");
        }

        // Add detected dependencies to constructor
        if (processedLogic != null)
        {
            foreach (var dep in processedLogic.RequiredDependencies)
            {
                constructorParams.Add($"{dep.InterfaceType} {dep.ConstructorParamName}");
            }
        }

        if (constructorParams.Count <= 2)
        {
            sb.Append(string.Join(", ", constructorParams));
            sb.AppendLine(")");
        }
        else
        {
            sb.AppendLine();
            for (int i = 0; i < constructorParams.Count; i++)
            {
                var separator = i < constructorParams.Count - 1 ? "," : ")";
                sb.AppendLine($"{DoubleIndent}{constructorParams[i]}{separator}");
            }
        }

        sb.AppendLine($"{Indent}{{");
        sb.AppendLine($"{DoubleIndent}_context = context;");

        if (IncludeLogger)
        {
            sb.AppendLine($"{DoubleIndent}_logger = logger;");
        }

        if (IncludeMapper && UseProjectTo)
        {
            sb.AppendLine($"{DoubleIndent}_mapper = mapper;");
        }

        // Add detected dependency assignments
        if (processedLogic != null)
        {
            foreach (var dep in processedLogic.RequiredDependencies)
            {
                sb.AppendLine($"{DoubleIndent}{dep.FieldName} = {dep.ConstructorParamName};");
            }
        }

        sb.AppendLine($"{Indent}}}");
        sb.AppendLine();

        // Determine if we need async - either from IsAsync flag, if business logic contains await, or contains *Async calls
        // Also check processed logic since EnsureAsyncAwait may have added awaits
        var handlerNeedsAsync = queryInfo.IsAsync ||
            (!string.IsNullOrWhiteSpace(queryInfo.BusinessLogic) &&
             (queryInfo.BusinessLogic.Contains("await ", StringComparison.Ordinal) ||
              CqrsGeneratorHelpers.HasAsyncMethodCalls(queryInfo.BusinessLogic))) ||
            (processedLogic != null && processedLogic.Code.Contains("await ", StringComparison.Ordinal));

        // Add Handle method
        if (handlerNeedsAsync)
        {
            sb.AppendLine($"{Indent}public async Task<{queryInfo.ReturnType}> Handle({queryInfo.Name} request, CancellationToken cancellationToken)");
        }
        else
        {
            sb.AppendLine($"{Indent}public Task<{queryInfo.ReturnType}> Handle({queryInfo.Name} request, CancellationToken cancellationToken)");
        }

        sb.AppendLine($"{Indent}{{");

        // Add implementation
        if (processedLogic != null && !string.IsNullOrWhiteSpace(processedLogic.Code))
        {
            // Use processed business logic
            sb.AppendLine($"{DoubleIndent}// Business logic from {queryInfo.Source.ControllerName}.{queryInfo.Source.ActionName}");

            // If ViewBag mutations exist, declare result variable
            if (queryInfo.ViewModelMutations?.Any() == true)
            {
                var entityName = CqrsGeneratorHelpers.ExtractEntityName(queryInfo.Name);
                var dtoName = $"{entityName}ResponseDto";
                sb.AppendLine($"{DoubleIndent}var result = new {dtoName}();");
                sb.AppendLine();
            }

            // Indent the processed business logic
            var logicLines = processedLogic.Code.Split('\n', StringSplitOptions.TrimEntries);
            foreach (var line in logicLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine($"{DoubleIndent}{line}");
                }
            }
        }
        else if (string.IsNullOrWhiteSpace(queryInfo.BusinessLogic))
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
                var entityName = CqrsGeneratorHelpers.ExtractEntityName(queryInfo.Name);

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

        // Generate private methods that are called from business logic
        if (queryInfo.PrivateMethods?.Any() == true)
        {
            foreach (var privateMethod in queryInfo.PrivateMethods)
            {
                sb.AppendLine();
                GeneratePrivateMethod(sb, privateMethod);
            }
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Generates a private method for the handler from a controller's private method.
    /// Transforms controller-specific patterns to handler patterns.
    /// </summary>
    private void GeneratePrivateMethod(StringBuilder sb, NetLift.Core.Models.Modernization.PrivateMethodInfo privateMethod)
    {
        // Extract the method body and transform it
        var methodBody = privateMethod.Body;

        // Transform controller-specific patterns
        methodBody = TransformPrivateMethodBody(methodBody);

        // Add the method to the handler
        sb.AppendLine($"{Indent}/// <summary>");
        sb.AppendLine($"{Indent}/// Private helper method migrated from controller.");
        sb.AppendLine($"{Indent}/// TODO: Review and adapt as needed for handler context.");
        sb.AppendLine($"{Indent}/// </summary>");
        sb.AppendLine($"{Indent}{methodBody}");
    }

    /// <summary>
    /// Transforms controller-specific patterns in a private method body to handler patterns.
    /// Uses Roslyn for accurate parsing and transformation.
    /// </summary>
    private static string TransformPrivateMethodBody(string methodBody)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(methodBody);
            var root = tree.GetRoot();
            var rewriter = new PrivateMethodTransformRewriter();
            var newRoot = rewriter.Visit(root);
            return newRoot.ToFullString();
        }
        catch
        {
            // Fallback to original if parsing fails
            return methodBody;
        }
    }

    private static string GenerateDescription(QueryInfo queryInfo)
    {
        // Try to infer description from query name
        if (queryInfo.Name.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = CqrsGeneratorHelpers.ExtractEntityName(queryInfo.Name);

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
            var entityName = CqrsGeneratorHelpers.ExtractEntityName(queryInfo.Name);
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


    private void GenerateGetByIdImplementation(StringBuilder sb, QueryInfo queryInfo, string entityName)
    {
        var configureAwait = IncludeConfigureAwait ? ".ConfigureAwait(false)" : "";

        // Find the ID property
        var idProperty = queryInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        if (idProperty != null)
        {
            // Add logging at start
            if (IncludeLogger)
            {
                sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"Getting {entityName} by Id {{Id}}\", request.{idProperty.Name});");
                sb.AppendLine();
            }

            // Use optimized query with AsNoTracking and ProjectTo
            if (UseProjectTo && IncludeMapper && queryInfo.ReturnType.Contains("Dto"))
            {
                sb.AppendLine($"{DoubleIndent}var dto = await _context.{entityName}s");
                if (UseAsNoTracking)
                {
                    sb.AppendLine($"{TripleIndent}.AsNoTracking()");
                }
                sb.AppendLine($"{TripleIndent}.Where(e => e.Id == request.{idProperty.Name})");
                sb.AppendLine($"{TripleIndent}.ProjectTo<{entityName}Dto>(_mapper.ConfigurationProvider)");
                sb.AppendLine($"{TripleIndent}.FirstOrDefaultAsync(cancellationToken){configureAwait};");
                sb.AppendLine();
                sb.AppendLine($"{DoubleIndent}if (dto is null)");
                sb.AppendLine($"{DoubleIndent}{{");

                if (IncludeLogger)
                {
                    sb.AppendLine($"{TripleIndent}_logger.LogWarning(\"{entityName} with Id {{Id}} not found\", request.{idProperty.Name});");
                }

                if (queryInfo.ReturnType.Contains("Result"))
                {
                    sb.AppendLine($"{TripleIndent}return Result<{entityName}Dto>.Failure(Error.NotFound);");
                }
                else
                {
                    sb.AppendLine($"{TripleIndent}throw new KeyNotFoundException(\"{entityName} not found\");");
                }

                sb.AppendLine($"{DoubleIndent}}}");
                sb.AppendLine();

                if (queryInfo.ReturnType.Contains("Result"))
                {
                    sb.AppendLine($"{DoubleIndent}return Result<{entityName}Dto>.Success(dto);");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}return dto;");
                }
            }
            else
            {
                // Fallback to standard query
                sb.Append($"{DoubleIndent}var query = _context.{entityName}s");
                if (UseAsNoTracking)
                {
                    sb.AppendLine();
                    sb.AppendLine($"{TripleIndent}.AsNoTracking()");
                    sb.Append($"{TripleIndent}");
                }
                sb.AppendLine(".AsQueryable();");
                sb.AppendLine();

                if (queryInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}var entity = await query");
                    sb.AppendLine($"{TripleIndent}.FirstOrDefaultAsync(e => e.Id == request.{idProperty.Name}, cancellationToken){configureAwait};");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}var entity = query");
                    sb.AppendLine($"{TripleIndent}.FirstOrDefault(e => e.Id == request.{idProperty.Name});");
                }

                sb.AppendLine();
                sb.AppendLine($"{DoubleIndent}if (entity is null)");
                sb.AppendLine($"{DoubleIndent}{{");

                if (IncludeLogger)
                {
                    sb.AppendLine($"{TripleIndent}_logger.LogWarning(\"{entityName} with Id {{Id}} not found\", request.{idProperty.Name});");
                }

                if (queryInfo.ReturnType.Contains("Result"))
                {
                    if (queryInfo.IsAsync)
                    {
                        sb.AppendLine($"{TripleIndent}return Result<{entityName}Dto>.Failure(Error.NotFound);");
                    }
                    else
                    {
                        sb.AppendLine($"{TripleIndent}return Task.FromResult(Result<{entityName}Dto>.Failure(Error.NotFound));");
                    }
                }
                else
                {
                    sb.AppendLine($"{TripleIndent}throw new KeyNotFoundException(\"{entityName} not found\");");
                }

                sb.AppendLine($"{DoubleIndent}}}");
                sb.AppendLine();

                // Map to DTO using mapper if available
                if (queryInfo.ReturnType.Contains("Dto"))
                {
                    if (IncludeMapper)
                    {
                        sb.AppendLine($"{DoubleIndent}var dto = _mapper.Map<{entityName}Dto>(entity);");
                    }
                    else
                    {
                        sb.AppendLine($"{DoubleIndent}var dto = new {entityName}Dto");
                        sb.AppendLine($"{DoubleIndent}{{");
                        sb.AppendLine($"{TripleIndent}// TODO: Map entity properties to DTO");
                        sb.AppendLine($"{TripleIndent}// Id = entity.Id,");
                        sb.AppendLine($"{DoubleIndent}}};");
                    }

                    sb.AppendLine();

                    if (queryInfo.ReturnType.Contains("Result"))
                    {
                        if (queryInfo.IsAsync)
                        {
                            sb.AppendLine($"{DoubleIndent}return Result<{entityName}Dto>.Success(dto);");
                        }
                        else
                        {
                            sb.AppendLine($"{DoubleIndent}return Task.FromResult(Result<{entityName}Dto>.Success(dto));");
                        }
                    }
                    else
                    {
                        if (queryInfo.IsAsync)
                        {
                            sb.AppendLine($"{DoubleIndent}return dto;");
                        }
                        else
                        {
                            sb.AppendLine($"{DoubleIndent}return Task.FromResult(dto);");
                        }
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
        }
        else
        {
            sb.AppendLine($"{DoubleIndent}throw new NotImplementedException(\"GetById query requires Id property\");");
        }
    }

    private void GenerateGetListImplementation(StringBuilder sb, QueryInfo queryInfo, string entityName)
    {
        var configureAwait = IncludeConfigureAwait ? ".ConfigureAwait(false)" : "";

        // Add logging at start
        if (IncludeLogger)
        {
            sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"Getting {entityName} list\");");
            sb.AppendLine();
        }

        // Start with optimized query
        sb.Append($"{DoubleIndent}var query = _context.{entityName}s");
        if (UseAsNoTracking)
        {
            sb.AppendLine();
            sb.AppendLine($"{TripleIndent}.AsNoTracking()");
            sb.Append($"{TripleIndent}");
        }
        sb.AppendLine(".AsQueryable();");
        sb.AppendLine();

        // Add filtering logic if supported
        if (queryInfo.SupportsFiltering)
        {
            GenerateFilteringLogic(sb, queryInfo);
        }

        // Map to DTO with optimized projection
        if (queryInfo.ReturnType.Contains("Dto"))
        {
            if (UseProjectTo && IncludeMapper)
            {
                // Use ProjectTo for efficient SQL projection
                if (queryInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}var items = await query");
                    sb.AppendLine($"{TripleIndent}.ProjectTo<{entityName}Dto>(_mapper.ConfigurationProvider)");
                    sb.AppendLine($"{TripleIndent}.ToListAsync(cancellationToken){configureAwait};");
                    sb.AppendLine();
                    sb.AppendLine($"{DoubleIndent}return Result<IReadOnlyList<{entityName}Dto>>.Success(items);");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}var items = query");
                    sb.AppendLine($"{TripleIndent}.ProjectTo<{entityName}Dto>(_mapper.ConfigurationProvider)");
                    sb.AppendLine($"{TripleIndent}.ToList();");
                    sb.AppendLine();
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(Result<IReadOnlyList<{entityName}Dto>>.Success(items));");
                }
            }
            else
            {
                // Fallback to manual projection
                if (queryInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}var items = await query");
                    sb.AppendLine($"{TripleIndent}.Select(e => new {entityName}Dto");
                    sb.AppendLine($"{TripleIndent}{{");
                    sb.AppendLine($"{TripleIndent}{Indent}// TODO: Map entity properties to DTO");
                    sb.AppendLine($"{TripleIndent}{Indent}// Id = e.Id,");
                    sb.AppendLine($"{TripleIndent}}})");
                    sb.AppendLine($"{TripleIndent}.ToListAsync(cancellationToken){configureAwait};");
                    sb.AppendLine();
                    sb.AppendLine($"{DoubleIndent}return Result<IReadOnlyList<{entityName}Dto>>.Success(items);");
                }
                else
                {
                    sb.AppendLine($"{DoubleIndent}var items = query");
                    sb.AppendLine($"{TripleIndent}.Select(e => new {entityName}Dto");
                    sb.AppendLine($"{TripleIndent}{{");
                    sb.AppendLine($"{TripleIndent}{Indent}// TODO: Map entity properties to DTO");
                    sb.AppendLine($"{TripleIndent}{Indent}// Id = e.Id,");
                    sb.AppendLine($"{TripleIndent}}})");
                    sb.AppendLine($"{TripleIndent}.ToList();");
                    sb.AppendLine();
                    sb.AppendLine($"{DoubleIndent}return Task.FromResult(Result<IReadOnlyList<{entityName}Dto>>.Success(items));");
                }
            }
        }
        else
        {
            if (queryInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}return await query.ToListAsync(cancellationToken){configureAwait};");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}return Task.FromResult(query.ToList());");
            }
        }
    }

    private void GenerateGetListWithPaginationImplementation(StringBuilder sb, QueryInfo queryInfo, string entityName)
    {
        var configureAwait = IncludeConfigureAwait ? ".ConfigureAwait(false)" : "";

        // Add logging at start
        if (IncludeLogger)
        {
            sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"Getting paginated {entityName} list\");");
            sb.AppendLine();
        }

        // Start with optimized query
        sb.Append($"{DoubleIndent}var query = _context.{entityName}s");
        if (UseAsNoTracking)
        {
            sb.AppendLine();
            sb.AppendLine($"{TripleIndent}.AsNoTracking()");
            sb.Append($"{TripleIndent}");
        }
        sb.AppendLine(".AsQueryable();");
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
            // Map to DTO with optimized projection
            if (queryInfo.ReturnType.Contains("Dto"))
            {
                if (UseProjectTo && IncludeMapper)
                {
                    // Use ProjectTo for efficient SQL projection with pagination
                    if (queryInfo.IsAsync)
                    {
                        sb.AppendLine($"{DoubleIndent}var pagedList = await query");
                        sb.AppendLine($"{TripleIndent}.ProjectTo<{entityName}Dto>(_mapper.ConfigurationProvider)");
                        sb.AppendLine($"{TripleIndent}.ToPagedListAsync(request.{pageNumberProperty.Name}, request.{pageSizeProperty.Name}, cancellationToken){configureAwait};");
                        sb.AppendLine();
                        sb.AppendLine($"{DoubleIndent}return Result<PagedList<{entityName}Dto>>.Success(pagedList);");
                    }
                    else
                    {
                        sb.AppendLine($"{DoubleIndent}var pagedList = query");
                        sb.AppendLine($"{TripleIndent}.ProjectTo<{entityName}Dto>(_mapper.ConfigurationProvider)");
                        sb.AppendLine($"{TripleIndent}.ToPagedList(request.{pageNumberProperty.Name}, request.{pageSizeProperty.Name});");
                        sb.AppendLine();
                        sb.AppendLine($"{DoubleIndent}return Task.FromResult(Result<PagedList<{entityName}Dto>>.Success(pagedList));");
                    }
                }
                else
                {
                    // Fallback to manual projection
                    if (queryInfo.IsAsync)
                    {
                        sb.AppendLine($"{DoubleIndent}var pagedList = await query");
                        sb.AppendLine($"{TripleIndent}.Select(e => new {entityName}Dto");
                        sb.AppendLine($"{TripleIndent}{{");
                        sb.AppendLine($"{TripleIndent}{Indent}// TODO: Map entity properties to DTO");
                        sb.AppendLine($"{TripleIndent}{Indent}// Id = e.Id,");
                        sb.AppendLine($"{TripleIndent}}})");
                        sb.AppendLine($"{TripleIndent}.ToPagedListAsync(request.{pageNumberProperty.Name}, request.{pageSizeProperty.Name}, cancellationToken){configureAwait};");
                        sb.AppendLine();
                        sb.AppendLine($"{DoubleIndent}return Result<PagedList<{entityName}Dto>>.Success(pagedList);");
                    }
                    else
                    {
                        sb.AppendLine($"{DoubleIndent}var pagedList = query");
                        sb.AppendLine($"{TripleIndent}.Select(e => new {entityName}Dto");
                        sb.AppendLine($"{TripleIndent}{{");
                        sb.AppendLine($"{TripleIndent}{Indent}// TODO: Map entity properties to DTO");
                        sb.AppendLine($"{TripleIndent}{Indent}// Id = e.Id,");
                        sb.AppendLine($"{TripleIndent}}})");
                        sb.AppendLine($"{TripleIndent}.ToPagedList(request.{pageNumberProperty.Name}, request.{pageSizeProperty.Name});");
                        sb.AppendLine();
                        sb.AppendLine($"{DoubleIndent}return Task.FromResult(Result<PagedList<{entityName}Dto>>.Success(pagedList));");
                    }
                }
            }
            else
            {
                if (queryInfo.IsAsync)
                {
                    sb.AppendLine($"{DoubleIndent}return await query.ToPagedListAsync(request.{pageNumberProperty.Name}, request.{pageSizeProperty.Name}, cancellationToken){configureAwait};");
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

}
