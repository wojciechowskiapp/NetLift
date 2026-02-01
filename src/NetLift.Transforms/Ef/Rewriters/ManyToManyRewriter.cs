using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Ef.Configuration;

namespace NetLift.Transforms.Ef.Rewriters;

/// <summary>
/// Rewrites EF6 many-to-many relationship configurations to EF Core equivalents.
/// Transforms HasMany().WithMany() patterns, including Map() configurations, to UsingEntity patterns.
/// </summary>
public sealed class ManyToManyRewriter : CSharpSyntaxRewriter, IManyToManyRewriter
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private readonly List<ManyToManyInfo> _detectedRelationships = new();
    private int _lowestConfidence = 100;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public IReadOnlyCollection<ManyToManyInfo> DetectedRelationships => _detectedRelationships;

    /// <inheritdoc />
    public string Rewrite(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return sourceCode;
        }

        // Reset state
        _requiredUsings.Clear();
        _diagnostics.Clear();
        _detectedRelationships.Clear();
        _lowestConfidence = 100;

        // Parse the source code
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // Rewrite the tree
        var rewritten = Visit(root);

        if (rewritten == null)
        {
            return sourceCode;
        }

        // Add new using directives if needed
        rewritten = AddRequiredUsings(rewritten);

        return rewritten.ToFullString();
    }

    /// <summary>
    /// Visits expression statements to detect and transform many-to-many configurations.
    /// </summary>
    public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        // First visit children
        var visited = (ExpressionStatementSyntax?)base.VisitExpressionStatement(node);
        if (visited == null)
        {
            return null;
        }

        // Find the root invocation expression
        if (visited.Expression is not InvocationExpressionSyntax rootInvocation)
        {
            return visited;
        }

        // Check if this is a many-to-many configuration
        var analysis = AnalyzeManyToManyChain(rootInvocation);
        if (analysis == null || !analysis.IsManyToMany)
        {
            return visited;
        }

        // Record the detected relationship
        _detectedRelationships.Add(new ManyToManyInfo(
            analysis.LeftEntity,
            analysis.RightEntity,
            analysis.MapConfig?.TableName,
            analysis.MapConfig?.LeftKeyName,
            analysis.MapConfig?.RightKeyName));

        // If there's no Map(), it's a simple many-to-many (EF Core 5+ compatible)
        if (!analysis.HasMap)
        {
            _lowestConfidence = Math.Min(_lowestConfidence, ManyToManyMappings.SimpleConfidence);
            _diagnostics.Add(new RewriterDiagnostic(
                $"Detected simple many-to-many: {analysis.LeftEntity} <-> {analysis.RightEntity} (no transformation needed)",
                RewriterDiagnosticSeverity.Info));
            return visited;
        }

        // There's a Map() - determine if we should transform
        var confidence = ManyToManyMappings.GetMapConfidenceScore(
            analysis.MapConfig?.HasToTable ?? false,
            analysis.MapConfig?.HasMapLeftKey ?? false,
            analysis.MapConfig?.HasMapRightKey ?? false,
            analysis.MapConfig?.HasOtherCalls ?? false);

        _lowestConfidence = Math.Min(_lowestConfidence, confidence);

        if (analysis.MapConfig?.HasOtherCalls == true)
        {
            // Complex Map() - add TODO comment
            _diagnostics.Add(new RewriterDiagnostic(
                $"Complex Map() configuration detected for {analysis.LeftEntity} <-> {analysis.RightEntity} - manual review recommended",
                RewriterDiagnosticSeverity.Warning));

            var todoComment = SyntaxFactory.Comment(
                $"// TODO: Review complex Map() configuration for {analysis.LeftEntity} <-> {analysis.RightEntity}");
            return visited.WithLeadingTrivia(
                visited.GetLeadingTrivia().Insert(0, todoComment));
        }

        // Transform to UsingEntity
        return TransformToUsingEntity(visited, analysis);
    }

    /// <summary>
    /// Analyzes an invocation chain to determine if it's a many-to-many configuration.
    /// </summary>
    private static ManyToManyAnalysis? AnalyzeManyToManyChain(InvocationExpressionSyntax rootInvocation)
    {
        // Walk the chain to find HasMany, WithMany, and optionally Map
        InvocationExpressionSyntax? hasManyCall = null;
        InvocationExpressionSyntax? withManyCall = null;
        InvocationExpressionSyntax? mapCall = null;

        var current = rootInvocation;
        while (current != null)
        {
            var methodName = GetMethodName(current);

            if (methodName == "Map")
            {
                mapCall = current;
            }
            else if (methodName == "WithMany")
            {
                withManyCall = current;
            }
            else if (methodName == "HasMany")
            {
                hasManyCall = current;
                break; // HasMany is always before WithMany
            }

            // Move to the next call in the chain
            if (current.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is InvocationExpressionSyntax nextInvocation)
            {
                current = nextInvocation;
            }
            else
            {
                break;
            }
        }

        // Must have both HasMany and WithMany to be a many-to-many
        if (hasManyCall == null || withManyCall == null)
        {
            return null;
        }

        // Extract entity types
        var leftEntity = ExtractEntityType(hasManyCall);
        if (leftEntity == null)
        {
            return null;
        }

        // For the right entity, we'll use a placeholder since we can't easily infer from lambda
        var rightEntity = "UnknownEntity"; // Would need semantic model to properly resolve

        // Parse Map configuration if present
        MapConfiguration? mapConfig = null;
        if (mapCall != null)
        {
            mapConfig = ParseMapConfiguration(mapCall);
        }

        return new ManyToManyAnalysis
        {
            IsManyToMany = true,
            LeftEntity = leftEntity,
            RightEntity = rightEntity,
            HasManyCall = hasManyCall,
            WithManyCall = withManyCall,
            MapCall = mapCall,
            HasMap = mapCall != null,
            MapConfig = mapConfig
        };
    }

    /// <summary>
    /// Gets the method name from an invocation expression.
    /// </summary>
    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text;
        }
        return null;
    }

    /// <summary>
    /// Extracts the entity type from a HasMany() or Entity() call.
    /// </summary>
    private static string? ExtractEntityType(InvocationExpressionSyntax invocation)
    {
        // Walk backwards to find Entity<T>() call
        var current = invocation.Expression;
        while (current != null)
        {
            if (current is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Expression is InvocationExpressionSyntax inv &&
                    inv.Expression is MemberAccessExpressionSyntax ma &&
                    ma.Name is GenericNameSyntax genericName &&
                    genericName.Identifier.Text == "Entity")
                {
                    var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                    return typeArg?.ToString();
                }
                current = memberAccess.Expression;
            }
            else if (current is InvocationExpressionSyntax inv)
            {
                current = inv.Expression;
            }
            else
            {
                break;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses the Map() lambda to extract configuration.
    /// </summary>
    private static MapConfiguration ParseMapConfiguration(InvocationExpressionSyntax mapInvocation)
    {
        var config = new MapConfiguration();

        var lambda = mapInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression as LambdaExpressionSyntax;
        if (lambda?.Body is not BlockSyntax block)
        {
            // Single expression Map - treat as simple
            if (lambda?.Body is InvocationExpressionSyntax singleInvocation)
            {
                ProcessMapCall(singleInvocation, config);
            }
            return config;
        }

        foreach (var statement in block.Statements)
        {
            if (statement is not ExpressionStatementSyntax exprStmt)
                continue;

            if (exprStmt.Expression is not InvocationExpressionSyntax invocation)
                continue;

            ProcessMapCall(invocation, config);
        }

        return config;
    }

    /// <summary>
    /// Processes a single Map() method call.
    /// </summary>
    private static void ProcessMapCall(InvocationExpressionSyntax invocation, MapConfiguration config)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.Text;

        switch (methodName)
        {
            case "ToTable":
                config.HasToTable = true;
                config.TableName = ExtractStringArgument(invocation);
                break;

            case "MapLeftKey":
                config.HasMapLeftKey = true;
                config.LeftKeyName = ExtractStringArgument(invocation);
                break;

            case "MapRightKey":
                config.HasMapRightKey = true;
                config.RightKeyName = ExtractStringArgument(invocation);
                break;

            default:
                config.HasOtherCalls = true;
                break;
        }
    }

    /// <summary>
    /// Extracts a string literal argument from an invocation.
    /// </summary>
    private static string? ExtractStringArgument(InvocationExpressionSyntax invocation)
    {
        var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (arg?.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }
        return null;
    }

    /// <summary>
    /// Transforms a statement to use UsingEntity instead of Map.
    /// </summary>
    private ExpressionStatementSyntax TransformToUsingEntity(
        ExpressionStatementSyntax statement,
        ManyToManyAnalysis analysis)
    {
        _diagnostics.Add(new RewriterDiagnostic(
            $"Transforming Map() to UsingEntity() for {analysis.LeftEntity} <-> {analysis.RightEntity}",
            RewriterDiagnosticSeverity.Info));

        // Generate the UsingEntity code
        var usingEntityCode = ManyToManyMappings.GenerateUsingEntity(
            analysis.MapConfig?.TableName,
            analysis.MapConfig?.LeftKeyName,
            analysis.MapConfig?.RightKeyName,
            analysis.LeftEntity,
            analysis.RightEntity);

        // If no UsingEntity needed, just remove the Map() call
        if (string.IsNullOrWhiteSpace(usingEntityCode))
        {
            var newExpression = RemoveMapCall(statement.Expression);
            return statement.WithExpression(newExpression);
        }

        // Build the new statement with UsingEntity
        // Get the chain up to and including WithMany
        var chainUpToWithMany = GetChainUpToWithMany(statement.Expression);
        var newCode = chainUpToWithMany + usingEntityCode + ";";

        // Parse the new statement
        var tempCode = $"class Temp {{ void M() {{ {newCode} }} }}";
        var newTree = CSharpSyntaxTree.ParseText(tempCode);
        var newRoot = newTree.GetRoot();
        var newStatement = newRoot.DescendantNodes()
            .OfType<ExpressionStatementSyntax>()
            .FirstOrDefault();

        if (newStatement != null)
        {
            // Preserve trivia from original statement
            newStatement = newStatement
                .WithLeadingTrivia(statement.GetLeadingTrivia())
                .WithTrailingTrivia(statement.GetTrailingTrivia());

            // Add required using
            _requiredUsings.Add("System.Collections.Generic");

            return newStatement;
        }

        return statement;
    }

    /// <summary>
    /// Gets the chain up to and including WithMany().
    /// </summary>
    private static string GetChainUpToWithMany(ExpressionSyntax expression)
    {
        // Find the WithMany call
        var invocations = new Stack<InvocationExpressionSyntax>();
        var current = expression;

        while (current is InvocationExpressionSyntax invocation)
        {
            var methodName = GetMethodName(invocation);
            invocations.Push(invocation);

            if (methodName == "WithMany")
            {
                // Found WithMany - return everything up to and including it
                return invocation.ToString();
            }

            if (methodName == "Map")
            {
                // Skip Map and continue
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    current = memberAccess.Expression;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        // Fallback - return the full expression
        return expression.ToString();
    }

    /// <summary>
    /// Removes the Map() call from an expression.
    /// </summary>
    private static ExpressionSyntax RemoveMapCall(ExpressionSyntax expression)
    {
        // If this is a Map() invocation, return what it's called on
        if (expression is InvocationExpressionSyntax invocation &&
            GetMethodName(invocation) == "Map")
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Expression;
            }
        }

        return expression;
    }

    /// <summary>
    /// Adds required using directives.
    /// </summary>
    private SyntaxNode AddRequiredUsings(SyntaxNode root)
    {
        if (_requiredUsings.Count == 0)
        {
            return root;
        }

        if (root is CompilationUnitSyntax compilationUnit)
        {
            var existingUsings = compilationUnit.Usings
                .Select(u => u.Name?.ToString())
                .Where(n => n != null)
                .ToHashSet(StringComparer.Ordinal);

            var newUsings = _requiredUsings
                .Where(ns => !existingUsings.Contains(ns) && !string.IsNullOrWhiteSpace(ns))
                .Select(ns =>
                {
                    var usingCode = $"using {ns};";
                    var usingTree = CSharpSyntaxTree.ParseText(usingCode);
                    var parsedUsing = usingTree.GetRoot()
                        .DescendantNodes()
                        .OfType<UsingDirectiveSyntax>()
                        .First();
                    return parsedUsing.WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
                })
                .ToList();

            if (newUsings.Count > 0)
            {
                return compilationUnit.AddUsings(newUsings.ToArray());
            }
        }

        return root;
    }

    private class MapConfiguration
    {
        public bool HasToTable { get; set; }
        public bool HasMapLeftKey { get; set; }
        public bool HasMapRightKey { get; set; }
        public bool HasOtherCalls { get; set; }
        public string? TableName { get; set; }
        public string? LeftKeyName { get; set; }
        public string? RightKeyName { get; set; }
    }

    private class ManyToManyAnalysis
    {
        public bool IsManyToMany { get; set; }
        public string LeftEntity { get; set; } = string.Empty;
        public string RightEntity { get; set; } = string.Empty;
        public InvocationExpressionSyntax? HasManyCall { get; set; }
        public InvocationExpressionSyntax? WithManyCall { get; set; }
        public InvocationExpressionSyntax? MapCall { get; set; }
        public bool HasMap { get; set; }
        public MapConfiguration? MapConfig { get; set; }
    }
}
