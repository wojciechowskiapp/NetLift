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
/// Generates production-ready CQRS Command and CommandHandler classes from controller actions.
/// Includes logging, mapping, audit trails, and proper error handling.
/// </summary>
public sealed class CommandGenerator : ICommandGenerator
{
    private const string Indent = "    ";
    private const string DoubleIndent = "        ";
    private const string TripleIndent = "            ";
    private const string QuadIndent = "                ";

    /// <summary>
    /// Options for command generation.
    /// </summary>
    public bool IncludeLogger { get; set; } = true;
    public bool IncludeAuditTrail { get; set; } = true;
    public bool IncludeConfigureAwait { get; set; } = true;

    /// <summary>
    /// Generates a Command class for an action.
    /// </summary>
    /// <param name="commandInfo">Information about the command to generate</param>
    /// <returns>Generated C# source code for the command class</returns>
    public string Generate(CommandInfo commandInfo)
    {
        ArgumentNullException.ThrowIfNull(commandInfo);

        // Determine if async based on IsAsync flag OR if business logic contains async method calls
        // This must be done BEFORE processing to ensure EnsureAsyncAwait is called
        var needsAsync = commandInfo.IsAsync || CqrsGeneratorHelpers.HasAsyncMethodCalls(commandInfo.BusinessLogic ?? string.Empty);

        // Process business logic first to detect dependencies and fix issues
        var processedLogic = BusinessLogicProcessor.Process(commandInfo.BusinessLogic, needsAsync);

        var sb = new StringBuilder();

        // Add namespace
        sb.AppendLine($"namespace {commandInfo.Namespace};");
        sb.AppendLine();

        // Add usings (production-ready handlers)
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");

        // Add core usings
        var rootNamespace = CqrsGeneratorHelpers.ExtractRootNamespace(commandInfo.Namespace);
        sb.AppendLine($"using {rootNamespace}.Application.Common;");
        sb.AppendLine($"using {rootNamespace}.Application.Common.Interfaces;");
        sb.AppendLine($"using {rootNamespace}.Models;");

        // Add production feature usings
        if (IncludeLogger)
        {
            sb.AppendLine("using Microsoft.Extensions.Logging;");
        }

        // Add Microsoft.AspNetCore.Mvc.Rendering if ViewBag mutations exist (for SelectListItem)
        if (commandInfo.ViewModelMutations?.Any() == true)
        {
            sb.AppendLine("using Microsoft.AspNetCore.Mvc.Rendering;");
        }

        // Add Microsoft.AspNetCore.Http if any property uses ASP.NET Core Http types
        if (CqrsGeneratorHelpers.RequiresAspNetCoreHttpUsing(commandInfo))
        {
            sb.AppendLine("using Microsoft.AspNetCore.Http;");
        }

        // Add usings detected from business logic
        foreach (var requiredUsing in processedLogic.RequiredUsings)
        {
            sb.AppendLine($"using {requiredUsing};");
        }

        sb.AppendLine();

        // Generate Command record
        GenerateCommandRecord(sb, commandInfo);
        sb.AppendLine();

        // Generate Response DTO if ViewBag mutations exist
        if (commandInfo.ViewModelMutations?.Any() == true)
        {
            var entityName = CqrsGeneratorHelpers.ExtractEntityName(commandInfo.Name);
            CqrsGeneratorHelpers.GenerateResponseDto(sb, entityName, commandInfo.Name, commandInfo.ViewModelMutations);
            sb.AppendLine();
        }

        // Generate Handler in same file
        GenerateHandlerClass(sb, commandInfo, processedLogic);

        return sb.ToString();
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
            var propType = CqrsGeneratorHelpers.FormatPropertyType(prop);
            var propName = CqrsGeneratorHelpers.ToPascalCase(prop.Name);
            sb.AppendLine($"public record {commandInfo.Name}({propType} {propName}) : IRequest<{commandInfo.ReturnType}>;");
        }
        else
        {
            // Multiple properties - multi-line record
            sb.AppendLine($"public record {commandInfo.Name}(");

            for (int i = 0; i < commandInfo.Properties.Count; i++)
            {
                var prop = commandInfo.Properties[i];
                var propType = CqrsGeneratorHelpers.FormatPropertyType(prop);
                var propName = CqrsGeneratorHelpers.ToPascalCase(prop.Name);
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
    private void GenerateHandlerClass(StringBuilder sb, CommandInfo commandInfo, ProcessedBusinessLogic? processedLogic)
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

        // Add dependencies (fields)
        sb.AppendLine($"{Indent}private readonly IApplicationDbContext _context;");

        if (IncludeLogger)
        {
            sb.AppendLine($"{Indent}private readonly ILogger<{handlerName}> _logger;");
        }

        if (IncludeAuditTrail)
        {
            sb.AppendLine($"{Indent}private readonly ICurrentUserService _currentUser;");
            sb.AppendLine($"{Indent}private readonly IDateTime _dateTime;");
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

        if (IncludeAuditTrail)
        {
            constructorParams.Add("ICurrentUserService currentUser");
            constructorParams.Add("IDateTime dateTime");
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

        if (IncludeAuditTrail)
        {
            sb.AppendLine($"{DoubleIndent}_currentUser = currentUser;");
            sb.AppendLine($"{DoubleIndent}_dateTime = dateTime;");
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
        var handlerNeedsAsync = commandInfo.IsAsync ||
            (!string.IsNullOrWhiteSpace(commandInfo.BusinessLogic) &&
             (commandInfo.BusinessLogic.Contains("await ", StringComparison.Ordinal) ||
              CqrsGeneratorHelpers.HasAsyncMethodCalls(commandInfo.BusinessLogic))) ||
            (processedLogic != null && processedLogic.Code.Contains("await ", StringComparison.Ordinal));

        // Add Handle method
        if (handlerNeedsAsync)
        {
            sb.AppendLine($"{Indent}public async Task<{commandInfo.ReturnType}> Handle({commandInfo.Name} request, CancellationToken cancellationToken)");
        }
        else
        {
            sb.AppendLine($"{Indent}public Task<{commandInfo.ReturnType}> Handle({commandInfo.Name} request, CancellationToken cancellationToken)");
        }

        sb.AppendLine($"{Indent}{{");

        // Add implementation
        if (processedLogic != null && !string.IsNullOrWhiteSpace(processedLogic.Code))
        {
            // Use processed business logic
            sb.AppendLine($"{DoubleIndent}// Business logic from {commandInfo.Source.ControllerName}.{commandInfo.Source.ActionName}");

            // If ViewBag mutations exist, declare result variable
            if (commandInfo.ViewModelMutations?.Any() == true)
            {
                var entityName = CqrsGeneratorHelpers.ExtractEntityName(commandInfo.Name);
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
                var entityName = CqrsGeneratorHelpers.ExtractEntityName(commandInfo.Name);

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

        // Generate private methods that are called from business logic
        if (commandInfo.PrivateMethods?.Any() == true)
        {
            foreach (var privateMethod in commandInfo.PrivateMethods)
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

    private static string GenerateDescription(CommandInfo commandInfo)
    {
        // Try to infer description from command name
        if (commandInfo.Name.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = CqrsGeneratorHelpers.ExtractEntityName(commandInfo.Name);
            return $"Command to create a new {entityName}.";
        }
        else if (commandInfo.Name.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = CqrsGeneratorHelpers.ExtractEntityName(commandInfo.Name);
            return $"Command to update an existing {entityName}.";
        }
        else if (commandInfo.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase))
        {
            var entityName = CqrsGeneratorHelpers.ExtractEntityName(commandInfo.Name);
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


    private void GenerateCreateImplementation(StringBuilder sb, CommandInfo commandInfo, string entityName)
    {
        var configureAwait = IncludeConfigureAwait ? ".ConfigureAwait(false)" : "";

        // Add logging at start
        if (IncludeLogger)
        {
            sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"Creating {entityName}\");");
            sb.AppendLine();
        }

        sb.AppendLine($"{DoubleIndent}var entity = new {entityName}");
        sb.AppendLine($"{DoubleIndent}{{");

        foreach (var prop in commandInfo.Properties)
        {
            sb.AppendLine($"{TripleIndent}{prop.Name} = request.{prop.Name},");
        }

        // Add audit trail properties
        if (IncludeAuditTrail)
        {
            sb.AppendLine($"{TripleIndent}// Audit trail");
            sb.AppendLine($"{TripleIndent}CreatedBy = _currentUser.UserId,");
            sb.AppendLine($"{TripleIndent}CreatedDate = _dateTime.UtcNow,");
        }

        sb.AppendLine($"{DoubleIndent}}};");
        sb.AppendLine();
        sb.AppendLine($"{DoubleIndent}_context.{entityName}s.Add(entity);");

        if (commandInfo.IsAsync)
        {
            sb.AppendLine($"{DoubleIndent}await _context.SaveChangesAsync(cancellationToken){configureAwait};");
        }
        else
        {
            sb.AppendLine($"{DoubleIndent}_context.SaveChanges();");
        }

        sb.AppendLine();

        // Add logging at end
        if (IncludeLogger)
        {
            sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"{entityName} created with Id {{Id}}\", entity.Id);");
            sb.AppendLine();
        }

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

    private void GenerateUpdateImplementation(StringBuilder sb, CommandInfo commandInfo, string entityName)
    {
        var configureAwait = IncludeConfigureAwait ? ".ConfigureAwait(false)" : "";

        // Assume first property is Id
        var idProperty = commandInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        if (idProperty != null)
        {
            // Add logging at start
            if (IncludeLogger)
            {
                sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"Updating {entityName} with Id {{Id}}\", request.{idProperty.Name});");
                sb.AppendLine();
            }

            if (commandInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}var entity = await _context.{entityName}s.FindAsync(new object[] {{ request.{idProperty.Name} }}, cancellationToken){configureAwait};");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}var entity = _context.{entityName}s.Find(request.{idProperty.Name});");
            }

            sb.AppendLine();
            sb.AppendLine($"{DoubleIndent}if (entity is null)");
            sb.AppendLine($"{DoubleIndent}{{");

            if (IncludeLogger)
            {
                sb.AppendLine($"{TripleIndent}_logger.LogWarning(\"{entityName} with Id {{Id}} not found\", request.{idProperty.Name});");
            }

            if (commandInfo.ReturnType.Contains("Result"))
            {
                if (commandInfo.IsAsync)
                {
                    sb.AppendLine($"{TripleIndent}return Result.Failure(Error.NotFound);");
                }
                else
                {
                    sb.AppendLine($"{TripleIndent}return Task.FromResult(Result.Failure(Error.NotFound));");
                }
            }
            else
            {
                sb.AppendLine($"{TripleIndent}throw new KeyNotFoundException(\"{entityName} not found\");");
            }

            sb.AppendLine($"{DoubleIndent}}}");
            sb.AppendLine();

            sb.AppendLine($"{DoubleIndent}// Update properties");
            foreach (var prop in commandInfo.Properties.Where(p => p != idProperty))
            {
                sb.AppendLine($"{DoubleIndent}entity.{prop.Name} = request.{prop.Name};");
            }

            // Add audit trail
            if (IncludeAuditTrail)
            {
                sb.AppendLine();
                sb.AppendLine($"{DoubleIndent}// Audit trail");
                sb.AppendLine($"{DoubleIndent}entity.ModifiedBy = _currentUser.UserId;");
                sb.AppendLine($"{DoubleIndent}entity.ModifiedDate = _dateTime.UtcNow;");
            }

            sb.AppendLine();

            if (commandInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}await _context.SaveChangesAsync(cancellationToken){configureAwait};");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}_context.SaveChanges();");
            }

            sb.AppendLine();

            // Add logging at end
            if (IncludeLogger)
            {
                sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"{entityName} with Id {{Id}} updated successfully\", request.{idProperty.Name});");
                sb.AppendLine();
            }

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

    private void GenerateDeleteImplementation(StringBuilder sb, CommandInfo commandInfo, string entityName)
    {
        var configureAwait = IncludeConfigureAwait ? ".ConfigureAwait(false)" : "";

        // Assume first property is Id
        var idProperty = commandInfo.Properties.FirstOrDefault(p =>
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        if (idProperty != null)
        {
            // Add logging at start
            if (IncludeLogger)
            {
                sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"Deleting {entityName} with Id {{Id}}\", request.{idProperty.Name});");
                sb.AppendLine();
            }

            if (commandInfo.IsAsync)
            {
                sb.AppendLine($"{DoubleIndent}var entity = await _context.{entityName}s.FindAsync(new object[] {{ request.{idProperty.Name} }}, cancellationToken){configureAwait};");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}var entity = _context.{entityName}s.Find(request.{idProperty.Name});");
            }

            sb.AppendLine();
            sb.AppendLine($"{DoubleIndent}if (entity is null)");
            sb.AppendLine($"{DoubleIndent}{{");

            if (IncludeLogger)
            {
                sb.AppendLine($"{TripleIndent}_logger.LogWarning(\"{entityName} with Id {{Id}} not found for deletion\", request.{idProperty.Name});");
            }

            if (commandInfo.ReturnType.Contains("Result"))
            {
                if (commandInfo.IsAsync)
                {
                    sb.AppendLine($"{TripleIndent}return Result.Failure(Error.NotFound);");
                }
                else
                {
                    sb.AppendLine($"{TripleIndent}return Task.FromResult(Result.Failure(Error.NotFound));");
                }
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
                sb.AppendLine($"{DoubleIndent}await _context.SaveChangesAsync(cancellationToken){configureAwait};");
            }
            else
            {
                sb.AppendLine($"{DoubleIndent}_context.SaveChanges();");
            }

            sb.AppendLine();

            // Add logging at end
            if (IncludeLogger)
            {
                sb.AppendLine($"{DoubleIndent}_logger.LogInformation(\"{entityName} with Id {{Id}} deleted successfully\", request.{idProperty.Name});");
                sb.AppendLine();
            }

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

}
