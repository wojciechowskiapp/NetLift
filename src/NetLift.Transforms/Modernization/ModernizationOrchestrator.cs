using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models;
using NetLift.Core.Models.Modernization;
using NetLift.Transforms.Modernization.Generators;

namespace NetLift.Transforms.Modernization;

/// <summary>
/// Orchestrates the complete modernization process.
/// </summary>
public class ModernizationOrchestrator : IModernizationOrchestrator
{
    private readonly IControllerAnalyzer _controllerAnalyzer;
    private readonly IServiceAnalyzer _serviceAnalyzer;
    private readonly ILogicExtractor _logicExtractor;
    private readonly IControllerTransformer _controllerTransformer;
    private readonly ICommandGenerator _commandGenerator;
    private readonly IQueryGenerator _queryGenerator;
    private readonly IHandlerGenerator _handlerGenerator;
    private readonly IValidatorGenerator _validatorGenerator;
    private readonly BusinessLogicBuilder _businessLogicBuilder;

    public ModernizationOrchestrator(
        IControllerAnalyzer controllerAnalyzer,
        IServiceAnalyzer serviceAnalyzer,
        ILogicExtractor logicExtractor,
        IControllerTransformer controllerTransformer,
        ICommandGenerator commandGenerator,
        IQueryGenerator queryGenerator,
        IHandlerGenerator handlerGenerator,
        IValidatorGenerator validatorGenerator)
    {
        _controllerAnalyzer = controllerAnalyzer;
        _serviceAnalyzer = serviceAnalyzer;
        _logicExtractor = logicExtractor;
        _controllerTransformer = controllerTransformer;
        _commandGenerator = commandGenerator;
        _queryGenerator = queryGenerator;
        _handlerGenerator = handlerGenerator;
        _validatorGenerator = validatorGenerator;
        _businessLogicBuilder = new BusinessLogicBuilder();
    }

    public async Task<ModernizationAnalysis> AnalyzeAsync(
        ProjectInfo projectInfo,
        ModernizationOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var diagnostics = new List<ModernizationDiagnostic>();

        // Analyze all controllers in project
        var controllers = await _controllerAnalyzer.AnalyzeProjectAsync(
            projectInfo, cancellationToken);

        // Analyze services to extract business logic
        var projectDir = Path.GetDirectoryName(projectInfo.FilePath) ?? string.Empty;
        var services = await _serviceAnalyzer.AnalyzeServicesAsync(projectDir, cancellationToken);

        if (services.Any())
        {
            diagnostics.Add(new ModernizationDiagnostic
            {
                Severity = Core.Models.Modernization.DiagnosticSeverity.Info,
                Message = $"Found {services.Count} service(s) with business logic that can be migrated to handlers",
                Code = "MOD010"
            });
        }

        // Build potential commands and queries from actions
        var potentialCommands = new List<CommandInfo>();
        var potentialQueries = new List<QueryInfo>();
        var actionContexts = new List<ActionLogicContext>();
        var rootNamespace = projectInfo.RootNamespace ?? projectInfo.AssemblyName ?? "Application";

        foreach (var controller in controllers)
        {
            foreach (var action in controller.Actions)
            {
                // Skip trivial actions that don't need CQRS handlers
                if (action.IsTrivial)
                {
                    diagnostics.Add(new ModernizationDiagnostic
                    {
                        Severity = Core.Models.Modernization.DiagnosticSeverity.Info,
                        Message = $"Skipping trivial action {controller.ClassName}.{action.Name} (no CQRS handler needed)",
                        Code = "MOD011"
                    });
                    continue;
                }

                // Build action logic context with extracted logic
                var context = await BuildActionLogicContextAsync(
                    controller, action, services, rootNamespace, cancellationToken);

                actionContexts.Add(context);

                if (action.IsCommand)
                {
                    potentialCommands.Add(CreateCommandInfoWithLogic(context, rootNamespace));
                }
                else if (action.IsQuery)
                {
                    potentialQueries.Add(CreateQueryInfoWithLogic(context, rootNamespace));
                }
            }
        }

        stopwatch.Stop();

        return new ModernizationAnalysis
        {
            Controllers = controllers.ToList(),
            PatternCounts = new Dictionary<string, int>
            {
                ["Controllers"] = controllers.Count,
                ["Commands"] = potentialCommands.Count,
                ["Queries"] = potentialQueries.Count,
                ["Actions"] = controllers.Sum(c => c.Actions.Count),
                ["Services"] = services.Count,
                ["ExtractedLogicActions"] = actionContexts.Count(c => c.CombinedLogic != null)
            },
            PotentialCommands = potentialCommands,
            PotentialQueries = potentialQueries,
            Recommendations = GenerateRecommendations(controllers, services),
            EstimatedConfidence = CalculateConfidence(controllers),
            Duration = stopwatch.Elapsed,
            Diagnostics = diagnostics
        };
    }

    public async Task<ModernizationResult> ModernizeAsync(
        ProjectInfo projectInfo,
        ModernizationOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var generatedFiles = new List<GeneratedFileInfo>();
        var diagnostics = new List<ModernizationDiagnostic>();
        var appliedPatterns = new Dictionary<ModernizationPattern, int>();

        try
        {
            // Step 1: Analyze the project
            var analysis = await AnalyzeAsync(projectInfo, options, cancellationToken);
            diagnostics.AddRange(analysis.Diagnostics);

            // Determine project root namespace (fallback to assembly name)
            var rootNamespace = projectInfo.RootNamespace ?? projectInfo.AssemblyName ?? "Application";

            // Determine output directory
            var outputDirectory = options.OutputPath ?? Path.GetDirectoryName(projectInfo.FilePath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                diagnostics.Add(new ModernizationDiagnostic
                {
                    Severity = Core.Models.Modernization.DiagnosticSeverity.Error,
                    Message = "Could not determine output directory for generated files",
                    Code = "MOD001"
                });

                return new ModernizationResult
                {
                    Success = false,
                    Confidence = 0,
                    Duration = stopwatch.Elapsed,
                    Diagnostics = diagnostics
                };
            }

            // Step 2: Analyze services for logic extraction
            var projectDir = Path.GetDirectoryName(projectInfo.FilePath) ?? string.Empty;
            var services = await _serviceAnalyzer.AnalyzeServicesAsync(projectDir, cancellationToken);

            // Step 3: Generate CQRS artifacts for each controller
            foreach (var controller in analysis.Controllers)
            {
                var controllerBaseName = controller.ClassName.Replace("Controller", string.Empty);

                // Process each action
                foreach (var action in controller.Actions)
                {
                    // Skip trivial actions that don't need CQRS handlers
                    if (action.IsTrivial)
                    {
                        continue;
                    }

                    // Build action logic context to extract business logic
                    var actionContext = await BuildActionLogicContextAsync(
                        controller, action, services, rootNamespace, cancellationToken);

                    if (action.IsCommand)
                    {
                        // Generate Command + Handler in single file WITH extracted logic
                        var commandInfo = CreateCommandInfoWithLogic(actionContext, rootNamespace);
                        var commandCode = _commandGenerator.Generate(commandInfo);

                        // Write Command+Handler file (both classes in one file)
                        var commandFilePath = Path.Combine(
                            outputDirectory,
                            "Application",
                            controllerBaseName,
                            "Commands",
                            $"{commandInfo.Name}.cs");

                        await WriteGeneratedFileAsync(commandFilePath, commandCode, cancellationToken);

                        generatedFiles.Add(new GeneratedFileInfo
                        {
                            FilePath = commandFilePath,
                            FileType = "Command+Handler",
                            Confidence = commandInfo.Confidence,
                            SourceReference = $"{controller.ClassName}.{action.Name}"
                        });

                        // Generate Validator if needed
                        if (commandInfo.RequiresValidation && options.Patterns.Contains(ModernizationPattern.FluentValidation))
                        {
                            var validatorCode = _validatorGenerator.GenerateForCommand(commandInfo);

                            if (!string.IsNullOrEmpty(validatorCode))
                            {
                                var validatorName = commandInfo.Name.Replace("Command", "Validator");
                                var validatorFilePath = Path.Combine(
                                    outputDirectory,
                                    "Application",
                                    controllerBaseName,
                                    "Commands",
                                    $"{validatorName}.cs");

                                await WriteGeneratedFileAsync(validatorFilePath, validatorCode, cancellationToken);

                                generatedFiles.Add(new GeneratedFileInfo
                                {
                                    FilePath = validatorFilePath,
                                    FileType = "Validator",
                                    Confidence = commandInfo.Confidence,
                                    SourceReference = $"{controller.ClassName}.{action.Name}"
                                });
                            }
                        }

                        // Track pattern usage
                        if (!appliedPatterns.ContainsKey(ModernizationPattern.Cqrs))
                        {
                            appliedPatterns[ModernizationPattern.Cqrs] = 0;
                        }
                        appliedPatterns[ModernizationPattern.Cqrs]++;
                    }
                    else if (action.IsQuery)
                    {
                        // Generate Query + Handler in single file WITH extracted logic
                        var queryInfo = CreateQueryInfoWithLogic(actionContext, rootNamespace);
                        var queryCode = _queryGenerator.Generate(queryInfo);

                        // Write Query+Handler file (both classes in one file)
                        var queryFilePath = Path.Combine(
                            outputDirectory,
                            "Application",
                            controllerBaseName,
                            "Queries",
                            $"{queryInfo.Name}.cs");

                        await WriteGeneratedFileAsync(queryFilePath, queryCode, cancellationToken);

                        generatedFiles.Add(new GeneratedFileInfo
                        {
                            FilePath = queryFilePath,
                            FileType = "Query+Handler",
                            Confidence = queryInfo.Confidence,
                            SourceReference = $"{controller.ClassName}.{action.Name}"
                        });

                        // Generate Validator if needed
                        if (queryInfo.RequiresValidation && options.Patterns.Contains(ModernizationPattern.FluentValidation))
                        {
                            var validatorCode = _validatorGenerator.GenerateForQuery(queryInfo);

                            if (!string.IsNullOrEmpty(validatorCode))
                            {
                                var validatorName = queryInfo.Name.Replace("Query", "Validator");
                                var validatorFilePath = Path.Combine(
                                    outputDirectory,
                                    "Application",
                                    controllerBaseName,
                                    "Queries",
                                    $"{validatorName}.cs");

                                await WriteGeneratedFileAsync(validatorFilePath, validatorCode, cancellationToken);

                                generatedFiles.Add(new GeneratedFileInfo
                                {
                                    FilePath = validatorFilePath,
                                    FileType = "Validator",
                                    Confidence = queryInfo.Confidence,
                                    SourceReference = $"{controller.ClassName}.{action.Name}"
                                });
                            }
                        }

                        // Track pattern usage
                        if (!appliedPatterns.ContainsKey(ModernizationPattern.Cqrs))
                        {
                            appliedPatterns[ModernizationPattern.Cqrs] = 0;
                        }
                        appliedPatterns[ModernizationPattern.Cqrs]++;
                    }
                }
            }

            // Step 4: Generate common infrastructure files
            if (generatedFiles.Any())
            {
                // Generate Result<T> class
                var resultCode = _handlerGenerator.GenerateResultClass($"{rootNamespace}.Application.Common");
                var resultFilePath = Path.Combine(
                    outputDirectory,
                    "Application",
                    "Common",
                    "Result.cs");

                await WriteGeneratedFileAsync(resultFilePath, resultCode, cancellationToken);

                generatedFiles.Add(new GeneratedFileInfo
                {
                    FilePath = resultFilePath,
                    FileType = "Common",
                    Confidence = 100,
                    SourceReference = "Generated infrastructure"
                });

                // Generate lightweight MediatR replacement interfaces (IRequest, IRequestHandler, Unit)
                var mediatorInterfacesCode = _handlerGenerator.GenerateMediatorInterfaces($"{rootNamespace}.Application.Common");
                var mediatorInterfacesFilePath = Path.Combine(
                    outputDirectory,
                    "Application",
                    "Common",
                    "MediatorInterfaces.cs");

                await WriteGeneratedFileAsync(mediatorInterfacesFilePath, mediatorInterfacesCode, cancellationToken);

                generatedFiles.Add(new GeneratedFileInfo
                {
                    FilePath = mediatorInterfacesFilePath,
                    FileType = "Common",
                    Confidence = 100,
                    SourceReference = "Generated infrastructure"
                });

                // Generate IMediator interface
                var mediatorInterfaceCode = _handlerGenerator.GenerateMediatorInterface($"{rootNamespace}.Application.Common.Interfaces");
                var mediatorInterfaceFilePath = Path.Combine(
                    outputDirectory,
                    "Application",
                    "Common",
                    "Interfaces",
                    "IMediator.cs");

                await WriteGeneratedFileAsync(mediatorInterfaceFilePath, mediatorInterfaceCode, cancellationToken);

                generatedFiles.Add(new GeneratedFileInfo
                {
                    FilePath = mediatorInterfaceFilePath,
                    FileType = "Interface",
                    Confidence = 100,
                    SourceReference = "Generated infrastructure"
                });

                // Generate Mediator implementation
                var mediatorImplementationCode = _handlerGenerator.GenerateMediatorImplementation($"{rootNamespace}.Infrastructure", rootNamespace);
                var mediatorImplementationFilePath = Path.Combine(
                    outputDirectory,
                    "Infrastructure",
                    "Mediator.cs");

                await WriteGeneratedFileAsync(mediatorImplementationFilePath, mediatorImplementationCode, cancellationToken);

                generatedFiles.Add(new GeneratedFileInfo
                {
                    FilePath = mediatorImplementationFilePath,
                    FileType = "Infrastructure",
                    Confidence = 100,
                    SourceReference = "Generated infrastructure"
                });

                // Discover entities from the project Models folder
                var entityNames = DiscoverEntities(outputDirectory);
                if (entityNames.Any())
                {
                    // Generate IApplicationDbContext interface
                    var dbContextCode = _handlerGenerator.GenerateDbContextInterface(
                        entityNames,
                        $"{rootNamespace}.Application.Common.Interfaces");

                    var dbContextFilePath = Path.Combine(
                        outputDirectory,
                        "Application",
                        "Common",
                        "Interfaces",
                        "IApplicationDbContext.cs");

                    await WriteGeneratedFileAsync(dbContextFilePath, dbContextCode, cancellationToken);

                    generatedFiles.Add(new GeneratedFileInfo
                    {
                        FilePath = dbContextFilePath,
                        FileType = "Interface",
                        Confidence = 100,
                        SourceReference = "Generated infrastructure"
                    });

                    diagnostics.Add(new ModernizationDiagnostic
                    {
                        Severity = Core.Models.Modernization.DiagnosticSeverity.Info,
                        Message = $"Generated IApplicationDbContext with {entityNames.Count} entities: {string.Join(", ", entityNames.Take(5))}{(entityNames.Count > 5 ? "..." : "")}",
                        Code = "MOD002"
                    });
                }
                else
                {
                    // Add diagnostic if no entities found
                    diagnostics.Add(new ModernizationDiagnostic
                    {
                        Severity = Core.Models.Modernization.DiagnosticSeverity.Warning,
                        Message = "Could not discover entities - create IApplicationDbContext interface manually in Application/Common/Interfaces",
                        Code = "MOD002"
                    });
                }

                // Add diagnostic message about using lightweight MediatR replacement
                diagnostics.Add(new ModernizationDiagnostic
                {
                    Severity = Core.Models.Modernization.DiagnosticSeverity.Info,
                    Message = "Generated lightweight MediatR replacement (no external packages required)",
                    Code = "MOD009"
                });
            }

            // Step 5: Transform controllers to use MediatR
            var modifiedFiles = new List<ModifiedFileInfo>();

            foreach (var controller in analysis.Controllers)
            {
                try
                {
                    // Skip if controller file doesn't exist
                    if (!File.Exists(controller.FilePath))
                    {
                        diagnostics.Add(new ModernizationDiagnostic
                        {
                            Severity = Core.Models.Modernization.DiagnosticSeverity.Warning,
                            Message = $"Controller file not found: {controller.FilePath}",
                            Code = "MOD003"
                        });
                        continue;
                    }

                    // Read original controller source
                    var controllerSource = await File.ReadAllTextAsync(controller.FilePath, cancellationToken);

                    // Build action contexts for this controller (skip trivial actions)
                    var controllerActionContexts = new List<ActionLogicContext>();
                    foreach (var action in controller.Actions)
                    {
                        // Skip trivial actions - they remain unchanged in the controller
                        if (action.IsTrivial)
                        {
                            continue;
                        }

                        var context = await BuildActionLogicContextAsync(
                            controller, action, services, rootNamespace, cancellationToken);
                        controllerActionContexts.Add(context);
                    }

                    // Transform the controller
                    var transformResult = await _controllerTransformer.TransformAsync(
                        controllerSource, controllerActionContexts, cancellationToken);

                    // Only write if transformation was successful and source changed
                    if (!string.IsNullOrEmpty(transformResult.TransformedSource) &&
                        transformResult.TransformedSource != controllerSource)
                    {
                        // Write transformed controller back to file
                        await File.WriteAllTextAsync(controller.FilePath, transformResult.TransformedSource, cancellationToken);

                        modifiedFiles.Add(new ModifiedFileInfo
                        {
                            FilePath = controller.FilePath,
                            Confidence = transformResult.Confidence,
                            Changes = transformResult.TransformedActions
                                .Select(a => $"Transformed action {a} to use MediatR")
                                .ToList()
                        });

                        diagnostics.Add(new ModernizationDiagnostic
                        {
                            Severity = Core.Models.Modernization.DiagnosticSeverity.Info,
                            Message = $"Transformed {controller.ClassName} to use MediatR ({transformResult.TransformedActions.Count} actions)",
                            Code = "MOD004"
                        });

                        // Add warnings from transformation
                        foreach (var warning in transformResult.Warnings)
                        {
                            diagnostics.Add(new ModernizationDiagnostic
                            {
                                Severity = warning.Severity == "Error"
                                    ? Core.Models.Modernization.DiagnosticSeverity.Error
                                    : Core.Models.Modernization.DiagnosticSeverity.Warning,
                                Message = $"{warning.ActionName}: {warning.Message}",
                                Code = "MOD005"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.Add(new ModernizationDiagnostic
                    {
                        Severity = Core.Models.Modernization.DiagnosticSeverity.Error,
                        Message = $"Failed to transform {controller.ClassName}: {ex.Message}",
                        Code = "MOD006"
                    });
                }
            }

            stopwatch.Stop();

            // Format generated code
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                await RunDotnetFormatAsync(outputDirectory);
            }

            return new ModernizationResult
            {
                Success = true,
                GeneratedFiles = generatedFiles,
                ModifiedFiles = modifiedFiles,
                Diagnostics = diagnostics,
                Confidence = analysis.EstimatedConfidence,
                Duration = stopwatch.Elapsed,
                AppliedPatterns = appliedPatterns
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            diagnostics.Add(new ModernizationDiagnostic
            {
                Severity = Core.Models.Modernization.DiagnosticSeverity.Error,
                Message = $"Modernization failed: {ex.Message}",
                Code = "MOD999"
            });

            return new ModernizationResult
            {
                Success = false,
                Confidence = 0,
                Duration = stopwatch.Elapsed,
                Diagnostics = diagnostics
            };
        }
    }

    private static async Task WriteGeneratedFileAsync(string filePath, string content, CancellationToken cancellationToken)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write file
        await File.WriteAllTextAsync(filePath, content, cancellationToken);
    }

    private static async Task RunDotnetFormatAsync(string directory)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("dotnet", "format")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch { }
    }

    private List<string> GenerateRecommendations(
        IReadOnlyList<ControllerInfo> controllers,
        IReadOnlyList<ServiceInfo>? services = null)
    {
        var recommendations = new List<string>();

        if (controllers.Any())
            recommendations.Add($"Found {controllers.Count} controller(s) that can be modernized with CQRS pattern");

        var commandCount = controllers.Sum(c => c.Actions.Count(a => a.IsCommand));
        if (commandCount > 0)
            recommendations.Add($"{commandCount} action(s) can be converted to MediatR Commands");

        var queryCount = controllers.Sum(c => c.Actions.Count(a => a.IsQuery));
        if (queryCount > 0)
            recommendations.Add($"{queryCount} action(s) can be converted to MediatR Queries");

        if (services != null && services.Any())
        {
            var methodCount = services.Sum(s => s.Methods.Count);
            recommendations.Add($"Found {services.Count} service(s) with {methodCount} method(s) - logic will be migrated to handlers");

            var dbContextServices = services.Count(s => s.UsesDbContext);
            if (dbContextServices > 0)
                recommendations.Add($"{dbContextServices} service(s) use DbContext directly - perfect for CQRS migration");
        }

        return recommendations;
    }

    private async Task<ActionLogicContext> BuildActionLogicContextAsync(
        ControllerInfo controller,
        ActionInfo action,
        IReadOnlyList<ServiceInfo> services,
        string rootNamespace,
        CancellationToken cancellationToken)
    {
        var controllerBaseName = controller.ClassName.Replace("Controller", string.Empty);
        var targetNamespace = action.IsCommand
            ? $"{rootNamespace}.Application.{controllerBaseName}.Commands"
            : $"{rootNamespace}.Application.{controllerBaseName}.Queries";

        // Try to read the controller file and extract action body
        ExtractedLogic? actionLogic = null;
        var serviceMethodLinks = new List<ServiceMethodLink>();

        try
        {
            if (File.Exists(controller.FilePath))
            {
                var controllerSource = await File.ReadAllTextAsync(controller.FilePath, cancellationToken);

                // Find and extract the action method body (match by parameter count for overloads)
                var actionBody = ExtractActionMethodBody(controllerSource, action.Name, action.Parameters.Count);
                if (!string.IsNullOrWhiteSpace(actionBody))
                {
                    actionLogic = await _logicExtractor.ExtractFromMethodAsync(actionBody, cancellationToken);

                    // Find service methods called by this action
                    foreach (var serviceCall in actionLogic.ServiceCalls)
                    {
                        var serviceMethod = _serviceAnalyzer.FindServiceMethod(services, serviceCall.SourceCode);
                        if (serviceMethod != null)
                        {
                            var service = services.FirstOrDefault(s =>
                                s.Methods.Contains(serviceMethod));

                            if (service != null)
                            {
                                serviceMethodLinks.Add(new ServiceMethodLink
                                {
                                    Service = service,
                                    Method = serviceMethod,
                                    CallExpression = serviceCall.SourceCode,
                                    ResultVariable = null,
                                    ArgumentMappings = new Dictionary<string, string>()
                                });
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // If extraction fails, we'll use stub generation
        }

        // Combine logic from action and service methods
        ExtractedLogic? combinedLogic = null;
        if (actionLogic != null && serviceMethodLinks.Any())
        {
            combinedLogic = _logicExtractor.CombineLogic(actionLogic, serviceMethodLinks);
        }
        else if (actionLogic != null)
        {
            combinedLogic = actionLogic;
        }

        return new ActionLogicContext
        {
            Controller = controller,
            Action = action,
            ActionLogic = actionLogic,
            ServiceMethods = serviceMethodLinks,
            CombinedLogic = combinedLogic,
            TargetNamespace = targetNamespace,
            GenerateCommand = action.IsCommand,
            GenerateQuery = action.IsQuery,
            Confidence = combinedLogic?.Confidence ?? action.Confidence,
            Warnings = combinedLogic?.Warnings.ToList() ?? []
        };
    }

    /// <summary>
    /// Extracts the action method body using Roslyn for proper C# parsing.
    /// Returns the full method source (including signature) for the LogicExtractor.
    /// </summary>
    private static string ExtractActionMethodBody(string controllerSource, string actionName, int parameterCount)
    {
        // Use Roslyn to properly parse and extract the method
        var syntaxTree = CSharpSyntaxTree.ParseText(controllerSource);
        var root = syntaxTree.GetCompilationUnitRoot();

        // Find all methods with the given name
        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == actionName)
            .ToList();

        if (!methods.Any())
            return string.Empty;

        // If there's only one method, return it
        if (methods.Count == 1)
            return methods[0].ToFullString();

        // Multiple overloads - match by parameter count to get the exact overload
        var matchingMethod = methods.FirstOrDefault(m =>
            m.ParameterList.Parameters.Count == parameterCount);

        return matchingMethod?.ToFullString() ?? string.Empty;
    }

    private CommandInfo CreateCommandInfoWithLogic(ActionLogicContext context, string projectNamespace)
    {
        var commandInfo = CreateCommandInfo(context.Controller, context.Action, projectNamespace);

        // If we have extracted logic, generate the business logic code
        if (context.CombinedLogic != null)
        {
            var businessLogic = _businessLogicBuilder.BuildFromActionContext(context);
            if (!string.IsNullOrWhiteSpace(businessLogic))
            {
                return commandInfo with
                {
                    BusinessLogic = businessLogic,
                    Confidence = context.Confidence,
                    ViewModelMutations = context.CombinedLogic.ViewModelMutations
                };
            }
        }

        return commandInfo;
    }

    private QueryInfo CreateQueryInfoWithLogic(ActionLogicContext context, string projectNamespace)
    {
        var queryInfo = CreateQueryInfo(context.Controller, context.Action, projectNamespace);

        // If we have extracted logic, generate the business logic code
        if (context.CombinedLogic != null)
        {
            var businessLogic = _businessLogicBuilder.BuildFromActionContext(context);
            if (!string.IsNullOrWhiteSpace(businessLogic))
            {
                return queryInfo with
                {
                    BusinessLogic = businessLogic,
                    Confidence = context.Confidence,
                    ViewModelMutations = context.CombinedLogic.ViewModelMutations
                };
            }
        }

        return queryInfo;
    }

    private int CalculateConfidence(IReadOnlyList<ControllerInfo> controllers)
    {
        if (!controllers.Any()) return 0;
        return (int)controllers.Average(c => c.Confidence);
    }

    private CommandInfo CreateCommandInfo(ControllerInfo controller, ActionInfo action, string projectNamespace)
    {
        // Create namespace (e.g., "ProjectName.Application.ControllerName.Commands")
        var controllerBaseName = controller.ClassName.Replace("Controller", string.Empty);
        var commandNamespace = $"{projectNamespace}.Application.{controllerBaseName}.Commands";

        // Determine command name from controller and action name (with overload handling)
        var commandName = DetermineCommandName(controllerBaseName, action.Name, action.HasOverload, action.HttpMethods);

        // Map parameters to properties
        var properties = action.Parameters.Select(p => new CommandProperty
        {
            Name = p.Name,
            Type = p.Type,
            IsNullable = p.IsNullable,
            IsRequired = !p.HasDefaultValue && !p.IsNullable,
            ValidationRules = []
        }).ToList();

        // Determine return type (wrap in Result<T> for CQRS pattern)
        var returnType = DetermineReturnType(action.ReturnType, isCommand: true);

        return new CommandInfo
        {
            Name = commandName,
            Namespace = commandNamespace,
            Properties = properties,
            ReturnType = returnType,
            IsAsync = action.IsAsync,
            Source = new SourceReference
            {
                FilePath = controller.FilePath,
                ControllerName = controller.ClassName,
                ActionName = action.Name,
                LineNumber = null
            },
            Confidence = action.Confidence,
            RequiresValidation = properties.Any(p => p.IsRequired),
            BusinessLogic = null // TODO: Extract from action body using Roslyn
        };
    }

    private QueryInfo CreateQueryInfo(ControllerInfo controller, ActionInfo action, string projectNamespace)
    {
        // Create namespace (e.g., "ProjectName.Application.ControllerName.Queries")
        var controllerBaseName = controller.ClassName.Replace("Controller", string.Empty);
        var queryNamespace = $"{projectNamespace}.Application.{controllerBaseName}.Queries";

        // Determine query name from controller and action name (pass HasOverload flag)
        var queryName = DetermineQueryName(controllerBaseName, action.Name, action.HasOverload);

        // Map parameters to properties
        var properties = action.Parameters.Select(p => new CommandProperty
        {
            Name = p.Name,
            Type = p.Type,
            IsNullable = p.IsNullable,
            IsRequired = !p.HasDefaultValue && !p.IsNullable,
            ValidationRules = []
        }).ToList();

        // Determine return type
        var returnType = DetermineReturnType(action.ReturnType, isCommand: false);

        // Detect pagination support
        var supportsPagination = properties.Any(p =>
            p.Name.Equals("PageNumber", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("PageSize", StringComparison.OrdinalIgnoreCase));

        // Detect filtering support
        var supportsFiltering = properties.Any(p =>
            p.Name.Equals("Search", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("SearchString", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("Filter", StringComparison.OrdinalIgnoreCase));

        return new QueryInfo
        {
            Name = queryName,
            Namespace = queryNamespace,
            Properties = properties,
            ReturnType = returnType,
            IsAsync = action.IsAsync,
            Source = new SourceReference
            {
                FilePath = controller.FilePath,
                ControllerName = controller.ClassName,
                ActionName = action.Name,
                LineNumber = null
            },
            Confidence = action.Confidence,
            RequiresValidation = properties.Any(p => p.IsRequired),
            BusinessLogic = null, // TODO: Extract from action body using Roslyn
            SupportsPagination = supportsPagination,
            SupportsFiltering = supportsFiltering
        };
    }

    private static string DetermineCommandName(string controllerBaseName, string actionName, bool hasOverload = false, IReadOnlyList<string>? httpMethods = null)
    {
        // Action names like "Create", "Edit", "Update", "Delete" -> "{Controller}CreateCommand", etc.
        if (actionName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
        {
            return actionName;
        }

        var commandName = $"{controllerBaseName}{actionName}Command";

        // If there's an overload, disambiguate based on HTTP method
        // POST is the standard for commands, so it keeps the standard name
        // Other HTTP methods (PUT, PATCH, DELETE) get a suffix
        if (hasOverload && httpMethods?.Any() == true)
        {
            var primaryMethod = httpMethods.FirstOrDefault()?.ToUpperInvariant();

            // Only add suffix for non-POST methods to avoid collision
            if (primaryMethod != null && primaryMethod != "POST")
            {
                // Capitalize first letter of HTTP method for suffix (PUT -> Put, PATCH -> Patch)
                var methodSuffix = char.ToUpperInvariant(primaryMethod[0]) + primaryMethod.Substring(1).ToLowerInvariant();
                commandName = $"{controllerBaseName}{actionName}{methodSuffix}Command";
            }
        }

        return commandName;
    }

    private static string DetermineQueryName(string controllerBaseName, string actionName, bool hasOverload = false)
    {
        // Action names like "Index", "Details", "Get" -> "{Controller}GetListQuery", "{Controller}GetByIdQuery", etc.
        if (actionName.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            return actionName;
        }

        // Convert common action names to query names with controller prefix
        if (actionName.Equals("Index", StringComparison.OrdinalIgnoreCase))
        {
            return $"{controllerBaseName}GetListQuery";
        }

        if (actionName.Equals("Details", StringComparison.OrdinalIgnoreCase))
        {
            return $"{controllerBaseName}GetByIdQuery";
        }

        // If this is a GET that has a POST overload (like GET Create with POST Create), add "Form" suffix
        if (hasOverload)
        {
            return $"{controllerBaseName}{actionName}FormQuery";
        }

        return $"{controllerBaseName}{actionName}Query";
    }

    /// <summary>
    /// Discovers entity classes from the Models folder.
    /// </summary>
    private static List<string> DiscoverEntities(string projectDirectory)
    {
        var entityNames = new List<string>();
        var modelsPath = Path.Combine(projectDirectory, "Models");

        if (!Directory.Exists(modelsPath))
        {
            return entityNames;
        }

        // Find all .cs files in Models folder
        var modelFiles = Directory.GetFiles(modelsPath, "*.cs", SearchOption.TopDirectoryOnly);

        foreach (var file in modelFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                var syntaxTree = CSharpSyntaxTree.ParseText(content);
                var root = syntaxTree.GetCompilationUnitRoot();

                // Find class declarations that look like entities (not ViewModels, not DbContext, not static)
                var classes = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(c => !c.Modifiers.Any(m => m.Text == "static"))
                    .Where(c => !c.Identifier.Text.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase))
                    .Where(c => !c.Identifier.Text.EndsWith("Model", StringComparison.OrdinalIgnoreCase) ||
                               c.Identifier.Text.Length <= 5) // Allow "Model" but not "LoginModel"
                    .Where(c => !c.Identifier.Text.Contains("DbContext", StringComparison.OrdinalIgnoreCase))
                    .Where(c => !c.Identifier.Text.Contains("Context", StringComparison.OrdinalIgnoreCase))
                    .Where(c => !c.Identifier.Text.StartsWith("I") ||
                               char.IsLower(c.Identifier.Text[1])); // Exclude interfaces

                foreach (var classDecl in classes)
                {
                    // Check if it has properties (entities typically have properties)
                    var hasProperties = classDecl.Members.OfType<PropertyDeclarationSyntax>().Any();
                    if (hasProperties)
                    {
                        entityNames.Add(classDecl.Identifier.Text);
                    }
                }
            }
            catch
            {
                // Skip files that can't be parsed
            }
        }

        return entityNames.Distinct().ToList();
    }

    private static string DetermineReturnType(string actionReturnType, bool isCommand)
    {
        // Strip Task<> wrapper
        var innerType = actionReturnType;
        if (innerType.StartsWith("Task<", StringComparison.Ordinal) && innerType.EndsWith(">", StringComparison.Ordinal))
        {
            innerType = innerType.Substring(5, innerType.Length - 6);
        }

        // Strip ActionResult<> wrapper
        if (innerType.StartsWith("ActionResult<", StringComparison.Ordinal) && innerType.EndsWith(">", StringComparison.Ordinal))
        {
            innerType = innerType.Substring(13, innerType.Length - 14);
        }

        // For void/empty returns and MVC view results (which don't have typed returns)
        // These should return Result (non-generic) because they typically render views
        if (string.IsNullOrWhiteSpace(innerType) ||
            innerType.Equals("void", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("IActionResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("ActionResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("ViewResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("PartialViewResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("RedirectResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("RedirectToActionResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("RedirectToRouteResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("ContentResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("JsonResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("EmptyResult", StringComparison.OrdinalIgnoreCase) ||
            innerType.Equals("FileResult", StringComparison.OrdinalIgnoreCase))
        {
            // Commands return Result, Queries return Result (we'll use Result for consistency)
            // The actual return value will come from the business logic
            return "Result";
        }

        // Wrap in Result<T> for CQRS pattern
        return $"Result<{innerType}>";
    }
}
