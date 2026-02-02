using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;
using ProjectInfo = NetLift.Core.Models.ProjectInfo;

namespace NetLift.Transforms.Modernization.Analyzers;

/// <summary>
/// Analyzes .NET Framework projects to detect legacy logging patterns and configurations.
/// Detects log4net, NLog, Enterprise Library, Console output, and custom logger wrappers.
/// </summary>
public sealed class LoggingAnalyzer : ILoggingAnalyzer
{
    private static readonly HashSet<string> Log4NetPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "log4net"
    };

    private static readonly HashSet<string> NLogPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "NLog",
        "NLog.Config",
        "NLog.Schema"
    };

    private static readonly HashSet<string> EnterpriseLibraryPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "EnterpriseLibrary.Logging",
        "Microsoft.Practices.EnterpriseLibrary.Logging",
        "EnterpriseLibrary.Common"
    };

    private static readonly HashSet<string> LoggerFieldTypes = new(StringComparer.Ordinal)
    {
        "ILog", "ILogger", "Logger", "LogManager"
    };

    private static readonly HashSet<string> ConsoleOutputMethods = new(StringComparer.Ordinal)
    {
        "WriteLine", "Write"
    };

    private static readonly HashSet<string> LoggerMethodNames = new(StringComparer.Ordinal)
    {
        "Log", "Debug", "Info", "Information", "Warn", "Warning", "Error", "Fatal", "Trace"
    };

    /// <inheritdoc />
    public async Task<LoggingInfo?> AnalyzeProjectAsync(
        ProjectInfo projectInfo,
        CancellationToken cancellationToken = default)
    {
        var framework = await DetectFrameworkAsync(projectInfo, cancellationToken);

        if (framework == LoggingFramework.None)
        {
            return null;
        }

        var projectDir = Path.GetDirectoryName(projectInfo.FilePath);
        if (string.IsNullOrEmpty(projectDir))
        {
            return null;
        }

        // Find configuration file
        var configFilePath = FindLoggingConfigFile(projectDir, framework);
        string? configXml = null;

        if (configFilePath != null)
        {
            configXml = await ParseLoggingConfigAsync(configFilePath, framework, cancellationToken);
        }

        // Find logger usages in all source files
        var allUsages = new List<LoggerUsage>();
        var sourceFiles = projectInfo.CompileItems
            .Where(item => item.Include.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(item => Path.IsPathRooted(item.Include)
                ? item.Include
                : Path.Combine(projectDir, item.Include))
            .Where(File.Exists);

        foreach (var sourceFile in sourceFiles)
        {
            try
            {
                var sourceCode = await File.ReadAllTextAsync(sourceFile, cancellationToken);
                var usages = await FindLoggerUsagesAsync(sourceFile, sourceCode, cancellationToken);
                allUsages.AddRange(usages);
            }
            catch
            {
                // Skip files that cannot be read
                continue;
            }
        }

        // Detect custom logger type if framework is Custom
        string? customLoggerType = null;
        if (framework == LoggingFramework.Custom)
        {
            customLoggerType = DetectCustomLoggerType(allUsages);
        }

        // Get package references
        var packageRefs = GetLoggingPackages(projectInfo, framework);

        // Calculate confidence and warnings
        var confidence = CalculateConfidence(framework, configFilePath, allUsages);
        var warnings = GenerateWarnings(framework, configFilePath, allUsages);

        return new LoggingInfo
        {
            Framework = framework,
            ConfigurationFilePath = configFilePath,
            ConfigurationXml = configXml,
            CustomLoggerType = customLoggerType,
            LoggerUsages = allUsages,
            Confidence = confidence,
            RequiresMigration = true,
            Warnings = warnings,
            PackageReferences = packageRefs
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LoggerUsage>> FindLoggerUsagesAsync(
        string filePath,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Array.Empty<LoggerUsage>();
        }

        var usages = new List<LoggerUsage>();

        var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        // Find field declarations with logger types
        var fields = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>();

        foreach (var field in fields)
        {
            var typeName = field.Declaration.Type.ToString();
            if (IsLoggerType(typeName))
            {
                var lineNumber = tree.GetLineSpan(field.Span).StartLinePosition.Line + 1;
                var memberName = GetContainingMemberName(field);

                usages.Add(new LoggerUsage
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    MemberName = memberName,
                    UsageType = LoggerUsageType.FieldDeclaration,
                    LoggerType = typeName,
                    SourceSnippet = field.ToString().Trim()
                });
            }
        }

        // Find property declarations with logger types
        var properties = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>();

        foreach (var property in properties)
        {
            var typeName = property.Type.ToString();
            if (IsLoggerType(typeName))
            {
                var lineNumber = tree.GetLineSpan(property.Span).StartLinePosition.Line + 1;
                var memberName = GetContainingMemberName(property);

                usages.Add(new LoggerUsage
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    MemberName = memberName,
                    UsageType = LoggerUsageType.PropertyDeclaration,
                    LoggerType = typeName,
                    SourceSnippet = property.ToString().Trim()
                });
            }
        }

        // Find logger initialization calls
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var expressionText = invocation.Expression.ToString();
            var lineNumber = tree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
            var memberName = GetContainingMemberName(invocation);

            // Check for logger factory methods
            if (IsLoggerInitialization(expressionText))
            {
                usages.Add(new LoggerUsage
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    MemberName = memberName,
                    UsageType = LoggerUsageType.Initialization,
                    LoggerType = ExtractLoggerTypeFromInit(expressionText),
                    SourceSnippet = invocation.ToString().Trim()
                });
            }
            // Check for Console.WriteLine
            else if (IsConsoleOutput(invocation))
            {
                usages.Add(new LoggerUsage
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    MemberName = memberName,
                    UsageType = LoggerUsageType.ConsoleOutput,
                    LoggerType = "Console",
                    SourceSnippet = invocation.ToString().Trim()
                });
            }
            // Check for Debug/Trace.WriteLine
            else if (IsDebugOutput(invocation))
            {
                usages.Add(new LoggerUsage
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    MemberName = memberName,
                    UsageType = LoggerUsageType.DebugOutput,
                    LoggerType = GetDebugOutputType(invocation),
                    SourceSnippet = invocation.ToString().Trim()
                });
            }
            // Check for logger method calls
            else if (IsLoggerMethodCall(invocation))
            {
                usages.Add(new LoggerUsage
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    MemberName = memberName,
                    UsageType = LoggerUsageType.MethodCall,
                    LoggerType = ExtractLoggerTypeFromMethodCall(invocation),
                    SourceSnippet = invocation.ToString().Trim()
                });
            }
        }

        return usages;
    }

    /// <inheritdoc />
    public async Task<LoggingFramework> DetectFrameworkAsync(
        ProjectInfo projectInfo,
        CancellationToken cancellationToken = default)
    {
        var frameworks = new List<LoggingFramework>();

        // Check package references
        var packages = projectInfo.PackageReferences
            .Select(p => p.Id)
            .ToList();

        if (packages.Any(p => Log4NetPackages.Contains(p)))
        {
            frameworks.Add(LoggingFramework.Log4Net);
        }

        if (packages.Any(p => NLogPackages.Contains(p)))
        {
            frameworks.Add(LoggingFramework.NLog);
        }

        if (packages.Any(p => EnterpriseLibraryPackages.Contains(p)))
        {
            frameworks.Add(LoggingFramework.EnterpriseLibrary);
        }

        // Check for configuration files
        var projectDir = Path.GetDirectoryName(projectInfo.FilePath);
        if (!string.IsNullOrEmpty(projectDir))
        {
            if (File.Exists(Path.Combine(projectDir, "log4net.config")))
            {
                if (!frameworks.Contains(LoggingFramework.Log4Net))
                {
                    frameworks.Add(LoggingFramework.Log4Net);
                }
            }

            if (File.Exists(Path.Combine(projectDir, "nlog.config")) ||
                File.Exists(Path.Combine(projectDir, "NLog.config")))
            {
                if (!frameworks.Contains(LoggingFramework.NLog))
                {
                    frameworks.Add(LoggingFramework.NLog);
                }
            }
        }

        // Check source code for console/debug output
        var hasConsoleOutput = await HasConsoleOutputAsync(projectInfo, cancellationToken);
        if (hasConsoleOutput)
        {
            frameworks.Add(LoggingFramework.Console);
        }

        // Check for custom loggers
        var hasCustomLogger = await HasCustomLoggerAsync(projectInfo, cancellationToken);
        if (hasCustomLogger)
        {
            frameworks.Add(LoggingFramework.Custom);
        }

        // Return the detected framework
        if (frameworks.Count == 0)
        {
            return LoggingFramework.None;
        }

        if (frameworks.Count == 1)
        {
            return frameworks[0];
        }

        // Multiple frameworks detected
        return LoggingFramework.Mixed;
    }

    /// <inheritdoc />
    public async Task<string?> ParseLoggingConfigAsync(
        string configFilePath,
        LoggingFramework framework,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configFilePath))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(configFilePath, cancellationToken);

            // Validate it's XML
            var doc = XDocument.Parse(content);

            // For log4net, extract the log4net section
            if (framework == LoggingFramework.Log4Net)
            {
                var log4netSection = doc.Root?.Element("log4net");
                return log4netSection?.ToString() ?? content;
            }

            // For NLog, return the entire config
            if (framework == LoggingFramework.NLog)
            {
                return content;
            }

            return content;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindLoggingConfigFile(string projectDir, LoggingFramework framework)
    {
        return framework switch
        {
            LoggingFramework.Log4Net => FindConfigFile(projectDir, "log4net.config"),
            LoggingFramework.NLog => FindConfigFile(projectDir, "nlog.config", "NLog.config"),
            _ => null
        };
    }

    private static string? FindConfigFile(string projectDir, params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var filePath = Path.Combine(projectDir, fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }
        }

        return null;
    }

    private static bool IsLoggerType(string typeName)
    {
        return LoggerFieldTypes.Any(t => typeName.Contains(t, StringComparison.Ordinal));
    }

    private static bool IsLoggerInitialization(string expressionText)
    {
        return expressionText.Contains("LogManager.GetLogger", StringComparison.Ordinal) ||
               expressionText.Contains("LogManager.GetCurrentClassLogger", StringComparison.Ordinal) ||
               expressionText.Contains("LoggerFactory.Create", StringComparison.Ordinal);
    }

    private static string ExtractLoggerTypeFromInit(string expressionText)
    {
        if (expressionText.Contains("log4net", StringComparison.OrdinalIgnoreCase))
        {
            return "log4net.ILog";
        }

        if (expressionText.Contains("NLog", StringComparison.Ordinal))
        {
            return "NLog.ILogger";
        }

        return "Logger";
    }

    private static bool IsConsoleOutput(InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression;

        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var objectName = memberAccess.Expression.ToString();
            var methodName = memberAccess.Name.Identifier.Text;

            return objectName.Equals("Console", StringComparison.Ordinal) &&
                   ConsoleOutputMethods.Contains(methodName);
        }

        return false;
    }

    private static bool IsDebugOutput(InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression;

        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var objectName = memberAccess.Expression.ToString();
            var methodName = memberAccess.Name.Identifier.Text;

            return (objectName.Equals("Debug", StringComparison.Ordinal) ||
                    objectName.Equals("Trace", StringComparison.Ordinal)) &&
                   ConsoleOutputMethods.Contains(methodName);
        }

        return false;
    }

    private static string GetDebugOutputType(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Expression.ToString();
        }

        return "Debug";
    }

    private static bool IsLoggerMethodCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            return LoggerMethodNames.Contains(methodName);
        }

        return false;
    }

    private static string ExtractLoggerTypeFromMethodCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Expression.ToString();
        }

        return "Logger";
    }

    private static string GetContainingMemberName(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method != null)
        {
            return method.Identifier.Text;
        }

        var constructor = node.Ancestors().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
        if (constructor != null)
        {
            return constructor.Identifier.Text;
        }

        var property = node.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        if (property != null)
        {
            return property.Identifier.Text;
        }

        var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl != null)
        {
            return classDecl.Identifier.Text;
        }

        return "Unknown";
    }

    private static string? DetectCustomLoggerType(IReadOnlyList<LoggerUsage> usages)
    {
        // Find the most common logger type that's not a standard framework
        var customTypes = usages
            .Where(u => u.UsageType == LoggerUsageType.FieldDeclaration ||
                       u.UsageType == LoggerUsageType.PropertyDeclaration)
            .Select(u => u.LoggerType)
            .Where(t => !t.Contains("log4net", StringComparison.OrdinalIgnoreCase) &&
                       !t.Contains("NLog", StringComparison.OrdinalIgnoreCase) &&
                       !t.Contains("EnterpriseLibrary", StringComparison.OrdinalIgnoreCase))
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return customTypes?.Key;
    }

    private static List<string> GetLoggingPackages(ProjectInfo projectInfo, LoggingFramework framework)
    {
        var packages = new List<string>();

        var allPackages = projectInfo.PackageReferences.Select(p => p.Id).ToList();

        packages.AddRange(allPackages.Where(p =>
            Log4NetPackages.Contains(p) ||
            NLogPackages.Contains(p) ||
            EnterpriseLibraryPackages.Contains(p)));

        return packages;
    }

    private static int CalculateConfidence(
        LoggingFramework framework,
        string? configFilePath,
        IReadOnlyList<LoggerUsage> usages)
    {
        var confidence = 100;

        // Lower confidence for mixed frameworks
        if (framework == LoggingFramework.Mixed)
        {
            confidence = Math.Min(confidence, 75);
        }

        // Lower confidence if no config file found for frameworks that typically have one
        if ((framework == LoggingFramework.Log4Net || framework == LoggingFramework.NLog) &&
            string.IsNullOrEmpty(configFilePath))
        {
            confidence = Math.Min(confidence, 85);
        }

        // Lower confidence for custom loggers
        if (framework == LoggingFramework.Custom)
        {
            confidence = Math.Min(confidence, 70);
        }

        // Lower confidence if no usages found
        if (usages.Count == 0)
        {
            confidence = Math.Min(confidence, 60);
        }

        return confidence;
    }

    private static List<string> GenerateWarnings(
        LoggingFramework framework,
        string? configFilePath,
        IReadOnlyList<LoggerUsage> usages)
    {
        var warnings = new List<string>();

        if (framework == LoggingFramework.Mixed)
        {
            warnings.Add("Multiple logging frameworks detected. Consider consolidating to a single framework.");
        }

        if ((framework == LoggingFramework.Log4Net || framework == LoggingFramework.NLog) &&
            string.IsNullOrEmpty(configFilePath))
        {
            warnings.Add($"No {framework} configuration file found. Configuration may be embedded in code or web.config.");
        }

        if (framework == LoggingFramework.Custom)
        {
            warnings.Add("Custom logger detected. Manual review required for migration.");
        }

        if (framework == LoggingFramework.Console && usages.Count > 50)
        {
            warnings.Add("Large number of Console.WriteLine calls detected. Consider using structured logging.");
        }

        return warnings;
    }

    private static async Task<bool> HasConsoleOutputAsync(
        ProjectInfo projectInfo,
        CancellationToken cancellationToken)
    {
        var projectDir = Path.GetDirectoryName(projectInfo.FilePath);
        if (string.IsNullOrEmpty(projectDir))
        {
            return false;
        }

        var sourceFiles = projectInfo.CompileItems
            .Where(item => item.Include.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(item => Path.IsPathRooted(item.Include)
                ? item.Include
                : Path.Combine(projectDir, item.Include))
            .Where(File.Exists)
            .Take(10); // Sample first 10 files for performance

        foreach (var sourceFile in sourceFiles)
        {
            try
            {
                var sourceCode = await File.ReadAllTextAsync(sourceFile, cancellationToken);
                if (sourceCode.Contains("Console.WriteLine", StringComparison.Ordinal) ||
                    sourceCode.Contains("Debug.WriteLine", StringComparison.Ordinal) ||
                    sourceCode.Contains("Trace.WriteLine", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch
            {
                continue;
            }
        }

        return false;
    }

    private static async Task<bool> HasCustomLoggerAsync(
        ProjectInfo projectInfo,
        CancellationToken cancellationToken)
    {
        var projectDir = Path.GetDirectoryName(projectInfo.FilePath);
        if (string.IsNullOrEmpty(projectDir))
        {
            return false;
        }

        var sourceFiles = projectInfo.CompileItems
            .Where(item => item.Include.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Include.Contains("Logger", StringComparison.OrdinalIgnoreCase) ||
                          item.Include.Contains("Logging", StringComparison.OrdinalIgnoreCase))
            .Select(item => Path.IsPathRooted(item.Include)
                ? item.Include
                : Path.Combine(projectDir, item.Include))
            .Where(File.Exists);

        foreach (var sourceFile in sourceFiles)
        {
            try
            {
                var sourceCode = await File.ReadAllTextAsync(sourceFile, cancellationToken);
                var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
                var root = await tree.GetRootAsync(cancellationToken);

                // Look for classes with "Logger" in the name
                var loggerClasses = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(c => c.Identifier.Text.Contains("Logger", StringComparison.Ordinal));

                if (loggerClasses.Any())
                {
                    return true;
                }
            }
            catch
            {
                continue;
            }
        }

        return false;
    }
}
