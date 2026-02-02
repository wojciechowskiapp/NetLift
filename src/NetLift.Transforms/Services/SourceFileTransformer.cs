using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;

namespace NetLift.Transforms.Services;

/// <summary>
/// Orchestrates source file transformations by detecting file type and applying appropriate rewriter chain.
/// Implements a pipeline pattern where each rewriter's output becomes the next rewriter's input.
/// </summary>
public sealed class SourceFileTransformer : ISourceFileTransformer
{
    private readonly IMvcNamespaceRewriter _mvcNamespaceRewriter;
    private readonly IControllerBaseRewriter _controllerBaseRewriter;
    private readonly IControllerMethodBodyRewriter _controllerMethodBodyRewriter;
    private readonly IActionResultRewriter _actionResultRewriter;
    private readonly IHttpContextRewriter _httpContextRewriter;
    private readonly IAttributeRoutingTransformer _attributeRoutingTransformer;
    private readonly IActionFilterTransformer _actionFilterTransformer;
    private readonly IDbContextConstructorRewriter _dbContextConstructorRewriter;
    private readonly IFluentApiRelationshipRewriter _fluentApiRelationshipRewriter;
    private readonly IManyToManyRewriter _manyToManyRewriter;
    private readonly IIncludeThenIncludeRewriter _includeThenIncludeRewriter;
    private readonly ISqlQueryRewriter _sqlQueryRewriter;
    private readonly ILazyLoadingConfigRewriter _lazyLoadingConfigRewriter;
    private readonly IDatabaseInitializerRemover _databaseInitializerRemover;

    private ISet<string>? _knownDbContextTypes;

    public SourceFileTransformer(
        IMvcNamespaceRewriter mvcNamespaceRewriter,
        IControllerBaseRewriter controllerBaseRewriter,
        IControllerMethodBodyRewriter controllerMethodBodyRewriter,
        IActionResultRewriter actionResultRewriter,
        IHttpContextRewriter httpContextRewriter,
        IAttributeRoutingTransformer attributeRoutingTransformer,
        IActionFilterTransformer actionFilterTransformer,
        IDbContextConstructorRewriter dbContextConstructorRewriter,
        IFluentApiRelationshipRewriter fluentApiRelationshipRewriter,
        IManyToManyRewriter manyToManyRewriter,
        IIncludeThenIncludeRewriter includeThenIncludeRewriter,
        ISqlQueryRewriter sqlQueryRewriter,
        ILazyLoadingConfigRewriter lazyLoadingConfigRewriter,
        IDatabaseInitializerRemover databaseInitializerRemover)
    {
        _mvcNamespaceRewriter = mvcNamespaceRewriter;
        _controllerBaseRewriter = controllerBaseRewriter;
        _controllerMethodBodyRewriter = controllerMethodBodyRewriter;
        _actionResultRewriter = actionResultRewriter;
        _httpContextRewriter = httpContextRewriter;
        _attributeRoutingTransformer = attributeRoutingTransformer;
        _actionFilterTransformer = actionFilterTransformer;
        _dbContextConstructorRewriter = dbContextConstructorRewriter;
        _fluentApiRelationshipRewriter = fluentApiRelationshipRewriter;
        _manyToManyRewriter = manyToManyRewriter;
        _includeThenIncludeRewriter = includeThenIncludeRewriter;
        _sqlQueryRewriter = sqlQueryRewriter;
        _lazyLoadingConfigRewriter = lazyLoadingConfigRewriter;
        _databaseInitializerRemover = databaseInitializerRemover;
    }

    /// <inheritdoc />
    public void SetKnownDbContextTypes(ISet<string>? knownDbContextTypes)
    {
        _knownDbContextTypes = knownDbContextTypes;
    }

    /// <inheritdoc />
    public SourceFileType DetectFileType(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return SourceFileType.Unknown;
        }

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            // Check for class declarations
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            if (!classDeclarations.Any())
            {
                return SourceFileType.Unknown;
            }

            // Check for WCF service (highest priority - most specific)
            if (HasAttribute(root, "ServiceContract") || HasAttribute(root, "ServiceContractAttribute"))
            {
                return SourceFileType.WcfService;
            }

            // Check for DbContext
            if (classDeclarations.Any(c => HasBaseType(c, "DbContext")))
            {
                return SourceFileType.DbContext;
            }

            // Check for Controller
            if (classDeclarations.Any(c => HasBaseType(c, "Controller") || HasBaseType(c, "ApiController")))
            {
                return SourceFileType.Controller;
            }

            // Check for ActionFilter
            if (classDeclarations.Any(c =>
                HasBaseType(c, "IActionFilter") ||
                HasBaseType(c, "IAuthorizationFilter") ||
                HasBaseType(c, "IExceptionFilter") ||
                HasBaseType(c, "IResultFilter") ||
                HasBaseType(c, "ActionFilterAttribute") ||
                HasBaseType(c, "AuthorizeAttribute")))
            {
                return SourceFileType.ActionFilter;
            }

            // Check if it has any System.Web.Mvc references (generic MVC file)
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Select(u => u.Name?.ToString() ?? "")
                .ToList();

            if (usings.Any(u => u.Contains("System.Web.Mvc") || u.Contains("System.Data.Entity")))
            {
                return SourceFileType.Other;
            }

            return SourceFileType.Unknown;
        }
        catch (ArgumentException)
        {
            // Invalid source code syntax - treat as unknown
            return SourceFileType.Unknown;
        }
        catch (InvalidOperationException)
        {
            // Roslyn parsing issue - treat as unknown
            return SourceFileType.Unknown;
        }
    }

    /// <inheritdoc />
    public Task<SourceTransformResult> TransformAsync(
        string filePath,
        string sourceCode,
        SourceFileType fileType = SourceFileType.Unknown,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Task.FromResult(new SourceTransformResult
            {
                Changed = false,
                TransformedCode = sourceCode,
                Confidence = 100
            });
        }

        // Auto-detect file type if not provided
        if (fileType == SourceFileType.Unknown)
        {
            fileType = DetectFileType(sourceCode);
        }

        var result = fileType switch
        {
            SourceFileType.Controller => TransformController(sourceCode, filePath),
            SourceFileType.DbContext => TransformDbContext(sourceCode, filePath),
            SourceFileType.ActionFilter => TransformActionFilter(sourceCode, filePath),
            SourceFileType.WcfService => TransformWcfService(sourceCode, filePath),
            SourceFileType.Other => TransformOther(sourceCode, filePath),
            _ => new SourceTransformResult
            {
                Changed = false,
                TransformedCode = sourceCode,
                Confidence = 100
            }
        };

        return Task.FromResult(result);
    }

    private SourceTransformResult TransformController(string sourceCode, string filePath)
    {
        var transformers = new List<string>();
        var diagnostics = new List<MigrationDiagnostic>();
        var confidenceScores = new List<int>();
        var current = sourceCode;

        // 1. MVC Namespace rewriting
        current = ApplyRewriter(
            current,
            _mvcNamespaceRewriter,
            "MvcNamespaceRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 2. Controller base class rewriting
        current = ApplyRewriter(
            current,
            _controllerBaseRewriter,
            "ControllerBaseRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 3. ActionResult type rewriting
        current = ApplyRewriter(
            current,
            _actionResultRewriter,
            "ActionResultRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 4. Controller method body rewriting (storeDB→_context, TryUpdateModel, FormCollection, auth)
        current = ApplyRewriter(
            current,
            _controllerMethodBodyRewriter,
            "ControllerMethodBodyRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 5. HttpContext rewriting
        current = ApplyRewriter(
            current,
            _httpContextRewriter,
            "HttpContextRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 6. Attribute routing transformation
        current = ApplyRewriter(
            current,
            _attributeRoutingTransformer,
            "AttributeRoutingTransformer",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        return BuildResult(sourceCode, current, transformers, diagnostics, confidenceScores);
    }

    private SourceTransformResult TransformDbContext(string sourceCode, string filePath)
    {
        var transformers = new List<string>();
        var diagnostics = new List<MigrationDiagnostic>();
        var confidenceScores = new List<int>();
        var current = sourceCode;

        // 0. First apply namespace rewriting (System.Data.Entity → Microsoft.EntityFrameworkCore)
        current = ApplyRewriter(
            current,
            _mvcNamespaceRewriter,
            "EfNamespaceRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 1. DbContext constructor rewriting
        current = ApplyRewriter(
            current,
            _dbContextConstructorRewriter,
            "DbContextConstructorRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 2. Fluent API relationship rewriting
        current = ApplyRewriter(
            current,
            _fluentApiRelationshipRewriter,
            "FluentApiRelationshipRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 3. Many-to-many rewriting
        current = ApplyRewriter(
            current,
            _manyToManyRewriter,
            "ManyToManyRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 4. Include/ThenInclude rewriting
        current = ApplyRewriter(
            current,
            _includeThenIncludeRewriter,
            "IncludeThenIncludeRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 5. SqlQuery rewriting
        current = ApplyRewriter(
            current,
            _sqlQueryRewriter,
            "SqlQueryRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 6. Lazy loading configuration
        current = ApplyRewriter(
            current,
            _lazyLoadingConfigRewriter,
            "LazyLoadingConfigRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        // 7. Database initializer removal
        current = ApplyRewriter(
            current,
            _databaseInitializerRemover,
            "DatabaseInitializerRemover",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        return BuildResult(sourceCode, current, transformers, diagnostics, confidenceScores);
    }

    private SourceTransformResult TransformActionFilter(string sourceCode, string filePath)
    {
        var transformers = new List<string>();
        var diagnostics = new List<MigrationDiagnostic>();
        var confidenceScores = new List<int>();
        var current = sourceCode;

        // Action filter transformation
        current = ApplyRewriter(
            current,
            _actionFilterTransformer,
            "ActionFilterTransformer",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        return BuildResult(sourceCode, current, transformers, diagnostics, confidenceScores);
    }

    private SourceTransformResult TransformWcfService(string sourceCode, string filePath)
    {
        // WCF transformation is handled separately by WCF-specific generators
        // For now, just return unchanged with a diagnostic note
        return new SourceTransformResult
        {
            Changed = false,
            TransformedCode = sourceCode,
            Confidence = 100,
            Diagnostics = new[]
            {
                new MigrationDiagnostic
                {
                    Level = DiagnosticLevel.Info,
                    Message = "WCF service detected. Use WCF-specific migration pipeline for gRPC/REST transformation.",
                    FilePath = filePath,
                    Code = "WCFINFO001"
                }
            }
        };
    }

    private SourceTransformResult TransformOther(string sourceCode, string filePath)
    {
        var transformers = new List<string>();
        var diagnostics = new List<MigrationDiagnostic>();
        var confidenceScores = new List<int>();
        var current = sourceCode;

        // Apply only namespace rewriting for generic files
        current = ApplyRewriter(
            current,
            _mvcNamespaceRewriter,
            "MvcNamespaceRewriter",
            transformers,
            diagnostics,
            confidenceScores,
            filePath);

        return BuildResult(sourceCode, current, transformers, diagnostics, confidenceScores);
    }

    private string ApplyRewriter(
        string sourceCode,
        object rewriter,
        string rewriterName,
        List<string> transformers,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores,
        string filePath)
    {
        try
        {
            string transformed;

            // Special handling for ControllerMethodBodyRewriter to pass known DbContext types
            if (rewriter is IControllerMethodBodyRewriter methodBodyRewriter)
            {
                transformed = methodBodyRewriter.Rewrite(sourceCode, _knownDbContextTypes);
            }
            else
            {
                // Find the best Rewrite method - prefer single-parameter version to avoid ambiguity
                var rewriterType = rewriter.GetType();
                var rewriteMethods = rewriterType.GetMethods()
                    .Where(m => m.Name == "Rewrite" && m.GetParameters().Length >= 1)
                    .OrderBy(m => m.GetParameters().Length)
                    .ToList();

                var rewriteMethod = rewriteMethods.FirstOrDefault();
                if (rewriteMethod == null)
                {
                    return sourceCode;
                }

                // Determine parameters - some rewriters take optional parameters
                var parameters = rewriteMethod.GetParameters();
                if (parameters.Length == 1)
                {
                    transformed = (string)rewriteMethod.Invoke(rewriter, new object[] { sourceCode })!;
                }
                else
                {
                    // For rewriters with optional parameters, pass null for additional params
                    var args = new object?[parameters.Length];
                    args[0] = sourceCode;
                    for (var i = 1; i < parameters.Length; i++)
                    {
                        args[i] = null;
                    }
                    transformed = (string)rewriteMethod.Invoke(rewriter, args)!;
                }
            }

            // Only track if actually changed
            if (transformed != sourceCode)
            {
                transformers.Add(rewriterName);

                // Get confidence score
                var confidenceProp = rewriter.GetType().GetProperty("ConfidenceScore");
                if (confidenceProp != null)
                {
                    var confidence = (int)confidenceProp.GetValue(rewriter)!;
                    confidenceScores.Add(confidence);
                }

                // Get diagnostics
                var diagnosticsProp = rewriter.GetType().GetProperty("Diagnostics");
                if (diagnosticsProp != null)
                {
                    var rewriterDiagnostics = diagnosticsProp.GetValue(rewriter) as IEnumerable<RewriterDiagnostic>;
                    if (rewriterDiagnostics != null)
                    {
                        foreach (var diag in rewriterDiagnostics)
                        {
                            diagnostics.Add(new MigrationDiagnostic
                            {
                                Level = diag.Severity switch
                                {
                                    RewriterDiagnosticSeverity.Error => DiagnosticLevel.Error,
                                    RewriterDiagnosticSeverity.Warning => DiagnosticLevel.Warning,
                                    _ => DiagnosticLevel.Info
                                },
                                Message = $"[{rewriterName}] {diag.Message}",
                                FilePath = filePath
                            });
                        }
                    }
                }
            }

            return transformed;
        }
        catch (Exception ex)
        {
            diagnostics.Add(new MigrationDiagnostic
            {
                Level = DiagnosticLevel.Error,
                Message = $"[{rewriterName}] Failed to apply transformation: {ex.Message}",
                FilePath = filePath,
                Code = "REWRITER_ERROR"
            });

            return sourceCode;
        }
    }

    private SourceTransformResult BuildResult(
        string original,
        string transformed,
        List<string> transformers,
        List<MigrationDiagnostic> diagnostics,
        List<int> confidenceScores)
    {
        var changed = original != transformed;
        var overallConfidence = confidenceScores.Any() ? confidenceScores.Min() : 100;

        var todoComments = new List<string>();
        if (changed && overallConfidence < 60)
        {
            todoComments.Add($"TODO: Low confidence transformation (confidence: {overallConfidence}%). Manual review strongly recommended.");
            todoComments.Add($"TODO: Applied transformers: {string.Join(", ", transformers)}");
        }
        else if (changed && overallConfidence < 80)
        {
            todoComments.Add($"// INFO: Transformation applied with {overallConfidence}% confidence. Review recommended.");
        }

        return new SourceTransformResult
        {
            Changed = changed,
            TransformedCode = transformed,
            Confidence = overallConfidence,
            AppliedTransformers = transformers,
            Diagnostics = diagnostics,
            TodoComments = todoComments
        };
    }

    private static bool HasBaseType(ClassDeclarationSyntax classDecl, string baseTypeName)
    {
        if (classDecl.BaseList == null)
        {
            return false;
        }

        return classDecl.BaseList.Types
            .Select(t => t.Type.ToString())
            .Any(t => t.Contains(baseTypeName));
    }

    private static bool HasAttribute(CompilationUnitSyntax root, string attributeName)
    {
        var attributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Select(a => a.Name.ToString())
            .ToList();

        return attributes.Any(a =>
            a.Equals(attributeName, StringComparison.OrdinalIgnoreCase) ||
            a.Equals(attributeName.Replace("Attribute", ""), StringComparison.OrdinalIgnoreCase));
    }
}
