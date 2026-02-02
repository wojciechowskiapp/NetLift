using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.Modernization;
using NetLift.Core.Models.Modernization;

namespace NetLift.Transforms.Modernization.Analyzers;

/// <summary>
/// Extracts business logic from method bodies for transformation into CQRS handlers using Roslyn.
/// Parses C# method bodies to identify variables, method calls, DbContext operations, and control flow.
/// </summary>
public sealed class LogicExtractor : ILogicExtractor
{
    private static readonly Dictionary<string, string> AsyncMethodMappings = new(StringComparer.Ordinal)
    {
        // EF Core query methods
        ["ToList"] = "ToListAsync",
        ["ToArray"] = "ToArrayAsync",
        ["First"] = "FirstAsync",
        ["FirstOrDefault"] = "FirstOrDefaultAsync",
        ["Single"] = "SingleAsync",
        ["SingleOrDefault"] = "SingleOrDefaultAsync",
        ["Any"] = "AnyAsync",
        ["All"] = "AllAsync",
        ["Count"] = "CountAsync",
        ["LongCount"] = "LongCountAsync",
        ["Sum"] = "SumAsync",
        ["Average"] = "AverageAsync",
        ["Min"] = "MinAsync",
        ["Max"] = "MaxAsync",
        ["ForEach"] = "ForEachAsync",

        // EF Core CUD methods
        ["SaveChanges"] = "SaveChangesAsync",
        ["Find"] = "FindAsync",
        ["Load"] = "LoadAsync",

        // Other common async patterns
        ["ExecuteSqlCommand"] = "ExecuteSqlCommandAsync",
        ["ExecuteSqlRaw"] = "ExecuteSqlRawAsync",
        ["FromSql"] = "FromSqlAsync"
    };

    private static readonly HashSet<string> DbContextMethods = new(StringComparer.Ordinal)
    {
        "SaveChanges", "SaveChangesAsync",
        "Add", "AddAsync", "AddRange", "AddRangeAsync",
        "Update", "UpdateRange",
        "Remove", "RemoveRange",
        "Attach", "AttachRange",
        "Find", "FindAsync",
        "Entry",
        "ExecuteSqlCommand", "ExecuteSqlCommandAsync",
        "ExecuteSqlRaw", "ExecuteSqlRawAsync",
        "FromSql", "FromSqlAsync"
    };

    private static readonly HashSet<string> LinqMethods = new(StringComparer.Ordinal)
    {
        "Where", "Select", "SelectMany",
        "Include", "ThenInclude",
        "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
        "Skip", "Take",
        "GroupBy", "Join", "GroupJoin",
        "Distinct", "Union", "Intersect", "Except",
        "First", "FirstOrDefault", "Single", "SingleOrDefault",
        "Any", "All", "Count", "LongCount",
        "Sum", "Average", "Min", "Max",
        "ToList", "ToArray", "AsEnumerable", "AsQueryable"
    };

    /// <inheritdoc />
    public async Task<ExtractedLogic> ExtractAsync(
        string methodBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(methodBody))
        {
            return CreateEmptyLogic();
        }

        // Wrap in a class with a method to parse as valid C# code
        var wrappedCode = $"class Temp {{ void Method() {{ {methodBody} }} }}";

        var tree = CSharpSyntaxTree.ParseText(wrappedCode, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (method?.Body == null)
        {
            return CreateEmptyLogic();
        }

        return ExtractFromMethodBody(method.Body);
    }

    /// <inheritdoc />
    public async Task<ExtractedLogic> ExtractFromMethodAsync(
        string methodSource,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(methodSource))
        {
            return CreateEmptyLogic();
        }

        // Wrap in a class to parse as valid C# code
        var wrappedCode = $"class Temp {{ {methodSource} }}";

        var tree = CSharpSyntaxTree.ParseText(wrappedCode, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (method?.Body == null)
        {
            return CreateEmptyLogic();
        }

        return ExtractFromMethodBody(method.Body);
    }

    /// <inheritdoc />
    public ExtractedLogic CombineLogic(
        ExtractedLogic actionLogic,
        IReadOnlyList<ServiceMethodLink> serviceMethods)
    {
        if (serviceMethods.Count == 0)
        {
            return actionLogic;
        }

        var combinedVariables = new List<VariableInfo>(actionLogic.Variables);
        var combinedStatements = new List<StatementInfo>();
        var combinedServiceCalls = new List<MethodCallInfo>();
        var combinedDbOperations = new List<DbContextOperation>(actionLogic.DbOperations);
        var combinedExplicitLoads = new List<ExplicitLoadOperation>(actionLogic.ExplicitLoads);
        var combinedViewModelMutations = new List<ViewModelMutation>(actionLogic.ViewModelMutations);
        var combinedConditionalBlocks = new List<ConditionalBlock>(actionLogic.ConditionalBlocks);
        var combinedDependencies = new HashSet<string>(actionLogic.UsedDependencies);
        var warnings = new List<string>(actionLogic.Warnings);

        // Process each statement in action logic
        foreach (var statement in actionLogic.Statements)
        {
            // Check if this statement is a service method call
            var serviceCall = actionLogic.ServiceCalls.FirstOrDefault(sc =>
                statement.SourceCode.Contains(sc.SourceCode, StringComparison.Ordinal));

            if (serviceCall != null)
            {
                // Find the corresponding service method link
                var serviceMethodLink = serviceMethods.FirstOrDefault(sml =>
                    sml.CallExpression.Contains(serviceCall.MethodName, StringComparison.Ordinal));

                if (serviceMethodLink?.Method.ExtractedLogic != null)
                {
                    // Inline the service method body properly
                    var inlinedLogic = serviceMethodLink.Method.ExtractedLogic;

                    // Add service method variables (with renamed variables to avoid conflicts)
                    combinedVariables.AddRange(inlinedLogic.Variables);

                    // Add only NON-RETURN statements from service method
                    // (intermediate variables, setup code, etc.)
                    var nonReturnStatements = inlinedLogic.Statements
                        .Where(s => s.Type != StatementType.Return)
                        .ToList();
                    combinedStatements.AddRange(nonReturnStatements);

                    // KEEP the original statement but replace the service call expression
                    // with the service method's return expression
                    var transformedStatement = InlineServiceCallInStatement(
                        statement,
                        serviceCall,
                        inlinedLogic.ReturnStatement);
                    combinedStatements.Add(transformedStatement);

                    // Add service method DB operations
                    combinedDbOperations.AddRange(inlinedLogic.DbOperations);

                    // Add service method explicit loads
                    combinedExplicitLoads.AddRange(inlinedLogic.ExplicitLoads);

                    // Add service method view model mutations
                    combinedViewModelMutations.AddRange(inlinedLogic.ViewModelMutations);

                    // Add service method conditional blocks
                    combinedConditionalBlocks.AddRange(inlinedLogic.ConditionalBlocks);

                    // Add service method dependencies
                    foreach (var dep in inlinedLogic.UsedDependencies)
                    {
                        combinedDependencies.Add(dep);
                    }

                    warnings.Add($"Inlined service method: {serviceMethodLink.Method.Name}");
                }
                else
                {
                    // Keep the service call as-is if we couldn't extract its logic
                    combinedStatements.Add(statement);
                    combinedServiceCalls.Add(serviceCall);
                    warnings.Add($"Could not inline service method: {serviceCall.MethodName}");
                }
            }
            else
            {
                // Regular statement, keep as-is
                combinedStatements.Add(statement);
            }
        }

        // Calculate combined confidence
        var confidence = CalculateCombinedConfidence(actionLogic, serviceMethods);

        return new ExtractedLogic
        {
            Variables = combinedVariables,
            Statements = combinedStatements,
            ServiceCalls = combinedServiceCalls,
            DbOperations = combinedDbOperations,
            ExplicitLoads = combinedExplicitLoads,
            ViewModelMutations = combinedViewModelMutations,
            ConditionalBlocks = combinedConditionalBlocks,
            ReturnStatement = actionLogic.ReturnStatement,
            UsedDependencies = combinedDependencies.ToList(),
            HasModelStateValidation = actionLogic.HasModelStateValidation,
            HasViewBagUsage = actionLogic.HasViewBagUsage,
            Confidence = confidence,
            Warnings = warnings
        };
    }

    /// <inheritdoc />
    public ExtractedLogic TransformToAsync(ExtractedLogic logic)
    {
        var transformedStatements = new List<StatementInfo>();
        var transformedDbOperations = new List<DbContextOperation>();
        var warnings = new List<string>(logic.Warnings);

        // Transform statements to async
        foreach (var statement in logic.Statements)
        {
            if (statement.NeedsAsyncTransform)
            {
                var transformed = TransformStatementToAsync(statement);
                transformedStatements.Add(transformed);
            }
            else
            {
                transformedStatements.Add(statement);
            }
        }

        // Transform DB operations to async
        foreach (var dbOp in logic.DbOperations)
        {
            transformedDbOperations.Add(dbOp);
        }

        // Transform return statement if needed
        ReturnInfo? transformedReturn = null;
        if (logic.ReturnStatement != null)
        {
            transformedReturn = TransformReturnToAsync(logic.ReturnStatement);
        }

        return new ExtractedLogic
        {
            Variables = logic.Variables,
            Statements = transformedStatements,
            ServiceCalls = logic.ServiceCalls,
            DbOperations = transformedDbOperations,
            ExplicitLoads = logic.ExplicitLoads,
            ViewModelMutations = logic.ViewModelMutations,
            ConditionalBlocks = logic.ConditionalBlocks,
            ReturnStatement = transformedReturn,
            UsedDependencies = logic.UsedDependencies,
            HasModelStateValidation = logic.HasModelStateValidation,
            HasViewBagUsage = logic.HasViewBagUsage,
            Confidence = logic.Confidence,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Extracts logic from a method body block.
    /// </summary>
    private ExtractedLogic ExtractFromMethodBody(BlockSyntax body)
    {
        var walker = new LogicExtractionWalker();
        walker.Visit(body);

        var confidence = CalculateConfidence(walker);

        return new ExtractedLogic
        {
            Variables = walker.Variables,
            Statements = walker.Statements,
            ServiceCalls = walker.ServiceCalls,
            DbOperations = walker.DbOperations,
            ExplicitLoads = walker.ExplicitLoads,
            ViewModelMutations = walker.ViewModelMutations,
            ConditionalBlocks = walker.ConditionalBlocks,
            ReturnStatement = walker.ReturnStatement,
            UsedDependencies = walker.UsedDependencies.ToList(),
            HasModelStateValidation = walker.HasModelStateValidation,
            HasViewBagUsage = walker.HasViewBagUsage,
            Confidence = confidence,
            Warnings = walker.Warnings
        };
    }

    /// <summary>
    /// Transforms a statement to its async equivalent.
    /// </summary>
    private StatementInfo TransformStatementToAsync(StatementInfo statement)
    {
        var transformedCode = statement.SourceCode;

        // Add await keyword before async calls
        foreach (var (syncMethod, asyncMethod) in AsyncMethodMappings)
        {
            if (transformedCode.Contains($".{syncMethod}(", StringComparison.Ordinal))
            {
                transformedCode = transformedCode.Replace(
                    $".{syncMethod}(",
                    $".{asyncMethod}(");

                // Add await if not already present
                if (!transformedCode.TrimStart().StartsWith("await ", StringComparison.Ordinal))
                {
                    // Find the assignment or declaration and add await after =
                    if (transformedCode.Contains(" = ", StringComparison.Ordinal))
                    {
                        transformedCode = transformedCode.Replace(" = ", " = await ");
                    }
                    else
                    {
                        transformedCode = $"await {transformedCode}";
                    }
                }
            }
        }

        return statement with
        {
            TransformedCode = transformedCode,
            NeedsAsyncTransform = false
        };
    }

    /// <summary>
    /// Transforms a return statement to its async equivalent.
    /// </summary>
    private ReturnInfo TransformReturnToAsync(ReturnInfo returnInfo)
    {
        var transformedExpression = returnInfo.Expression;

        // Add await if the expression contains async methods
        foreach (var (syncMethod, asyncMethod) in AsyncMethodMappings)
        {
            if (transformedExpression.Contains($".{syncMethod}(", StringComparison.Ordinal))
            {
                transformedExpression = transformedExpression.Replace(
                    $".{syncMethod}(",
                    $".{asyncMethod}(");

                if (!transformedExpression.TrimStart().StartsWith("await ", StringComparison.Ordinal))
                {
                    transformedExpression = $"await {transformedExpression}";
                }
            }
        }

        return returnInfo with
        {
            TransformedReturn = transformedExpression
        };
    }

    /// <summary>
    /// Calculates confidence score for extracted logic.
    /// </summary>
    private int CalculateConfidence(LogicExtractionWalker walker)
    {
        var confidence = 100;

        // Lower confidence if we found complex patterns
        if (walker.HasViewBagUsage)
        {
            confidence = Math.Min(confidence, 75);
        }

        // Explicit loads indicate manual eager loading needed
        if (walker.ExplicitLoads.Count > 0)
        {
            confidence = Math.Min(confidence, 70);
        }

        // Conditional blocks add complexity
        if (walker.ConditionalBlocks.Count > 0)
        {
            confidence = Math.Min(confidence, 80);
        }

        // Multiple view model mutations indicate complex data flow
        if (walker.ViewModelMutations.Count > 3)
        {
            confidence = Math.Min(confidence, 75);
        }

        if (walker.Warnings.Count > 0)
        {
            confidence = Math.Min(confidence, 85);
        }

        // Lower confidence if no statements found
        if (walker.Statements.Count == 0)
        {
            confidence = Math.Min(confidence, 60);
        }

        return confidence;
    }

    /// <summary>
    /// Calculates confidence for combined logic.
    /// </summary>
    private int CalculateCombinedConfidence(
        ExtractedLogic actionLogic,
        IReadOnlyList<ServiceMethodLink> serviceMethods)
    {
        var confidence = actionLogic.Confidence;

        // Lower confidence for each service method we couldn't inline
        var uninlineableCount = serviceMethods.Count(sml =>
            sml.Method.ExtractedLogic == null);

        if (uninlineableCount > 0)
        {
            confidence = Math.Min(confidence, 80 - (uninlineableCount * 10));
        }

        return Math.Max(confidence, 50); // Never go below 50
    }

    /// <summary>
    /// Inlines a service method call within a statement by replacing the call with the return expression.
    /// For example: ViewBag.X = new SelectList(service.GetItems(), ...) where service.GetItems() returns db.Items
    /// becomes: ViewBag.X = new SelectList(db.Items, ...)
    /// </summary>
    private static StatementInfo InlineServiceCallInStatement(
        StatementInfo originalStatement,
        MethodCallInfo serviceCall,
        ReturnInfo? returnInfo)
    {
        var sourceCode = originalStatement.SourceCode;

        if (returnInfo == null || string.IsNullOrEmpty(returnInfo.Expression))
        {
            // No return info - keep original
            return originalStatement;
        }

        // Get the return expression (strip "return " prefix if present)
        var returnExpression = returnInfo.Expression;
        if (returnExpression.StartsWith("return ", StringComparison.OrdinalIgnoreCase))
        {
            returnExpression = returnExpression.Substring(7).TrimEnd(';').Trim();
        }

        // Replace the service call in the source code with the return expression
        // e.g., service.GetCatalogBrands() → db.CatalogBrands.ToList()
        var transformedCode = sourceCode.Replace(serviceCall.SourceCode, returnExpression);

        return originalStatement with
        {
            SourceCode = transformedCode,
            TransformedCode = transformedCode
        };
    }

    /// <summary>
    /// Creates an empty logic result.
    /// </summary>
    private ExtractedLogic CreateEmptyLogic()
    {
        return new ExtractedLogic
        {
            Variables = [],
            Statements = [],
            ServiceCalls = [],
            DbOperations = [],
            ReturnStatement = null,
            UsedDependencies = [],
            HasModelStateValidation = false,
            HasViewBagUsage = false,
            Confidence = 0,
            Warnings = ["Empty or invalid method body"]
        };
    }

    /// <summary>
    /// Roslyn syntax walker that extracts logic from method bodies.
    /// </summary>
    private sealed class LogicExtractionWalker : CSharpSyntaxWalker
    {
        private readonly bool _isNestedContext;

        public List<VariableInfo> Variables { get; } = new();
        public List<StatementInfo> Statements { get; } = new();
        public List<MethodCallInfo> ServiceCalls { get; } = new();
        public List<DbContextOperation> DbOperations { get; } = new();
        public List<ExplicitLoadOperation> ExplicitLoads { get; } = new();
        public List<ViewModelMutation> ViewModelMutations { get; } = new();
        public List<ConditionalBlock> ConditionalBlocks { get; } = new();
        public ReturnInfo? ReturnStatement { get; private set; }
        public HashSet<string> UsedDependencies { get; } = new();
        public bool HasModelStateValidation { get; private set; }
        public bool HasViewBagUsage { get; private set; }
        public List<string> Warnings { get; } = new();

        /// <summary>
        /// Creates a new LogicExtractionWalker.
        /// </summary>
        /// <param name="isNestedContext">True if this walker is processing a nested context (like inside an if block)</param>
        public LogicExtractionWalker(bool isNestedContext = false)
        {
            _isNestedContext = isNestedContext;
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            foreach (var variable in node.Declaration.Variables)
            {
                var variableInfo = new VariableInfo
                {
                    Name = variable.Identifier.Text,
                    Type = node.Declaration.Type.ToString(),
                    Initializer = variable.Initializer?.Value.ToString(),
                    LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line
                };

                Variables.Add(variableInfo);
            }

            // Add as statement
            var needsAsync = ContainsAsyncMethod(node.ToString());

            var statementInfo = new StatementInfo
            {
                Type = StatementType.Declaration,
                SourceCode = node.ToString().Trim(),
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                NeedsAsyncTransform = needsAsync
            };

            Statements.Add(statementInfo);

            base.VisitLocalDeclarationStatement(node);
        }

        public override void VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var expression = node.Expression;
            var needsAsync = ContainsAsyncMethod(node.ToString());

            StatementType statementType = expression switch
            {
                InvocationExpressionSyntax => StatementType.MethodCall,
                AssignmentExpressionSyntax => StatementType.Assignment,
                _ => StatementType.Other
            };

            // Handle assignment expressions directly to capture view model mutations
            if (expression is AssignmentExpressionSyntax assignment)
            {
                HandleAssignmentForMutation(assignment);
            }

            var statementInfo = new StatementInfo
            {
                Type = statementType,
                SourceCode = node.ToString().Trim(),
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                NeedsAsyncTransform = needsAsync
            };

            Statements.Add(statementInfo);

            base.VisitExpressionStatement(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var methodName = ExtractMethodName(node);
            var target = ExtractTarget(node);

            if (methodName != null && target != null)
            {
                var isDbContextCall = IsDbContextMethod(methodName);
                var shouldBeAsync = ShouldBeAsync(methodName);

                var methodCallInfo = new MethodCallInfo
                {
                    Target = target,
                    MethodName = methodName,
                    Arguments = ExtractArguments(node),
                    SourceCode = node.ToString(),
                    IsDbContextCall = isDbContextCall,
                    ShouldBeAsync = shouldBeAsync,
                    AsyncEquivalent = shouldBeAsync ? GetAsyncEquivalent(methodName) : null
                };

                // Detect explicit loading patterns: db.Entry().Collection().Load() or db.Entry().Reference().Load()
                if (methodName == "Load" || methodName == "LoadAsync")
                {
                    var explicitLoad = TryParseExplicitLoad(node);
                    if (explicitLoad != null)
                    {
                        ExplicitLoads.Add(explicitLoad);
                        Warnings.Add($"Explicit loading detected: {explicitLoad.NavigationProperty} - consider using Include/ThenInclude");
                    }
                }

                // Determine if this is a service call or DbContext call
                if (isDbContextCall || IsLinqChain(node))
                {
                    var dbOperation = ExtractDbContextOperation(node);
                    if (dbOperation != null)
                    {
                        DbOperations.Add(dbOperation);
                    }
                }
                else if (IsServiceCall(target, methodName))
                {
                    ServiceCalls.Add(methodCallInfo);
                    UsedDependencies.Add(target);
                }

                // Check for ModelState usage
                if (target.Contains("ModelState", StringComparison.Ordinal))
                {
                    HasModelStateValidation = true;
                }

                // Check for ViewBag/ViewData usage
                if (target.Contains("ViewBag", StringComparison.Ordinal) ||
                    target.Contains("ViewData", StringComparison.Ordinal))
                {
                    HasViewBagUsage = true;
                }
            }

            base.VisitInvocationExpression(node);
        }

        /// <summary>
        /// Tries to parse an explicit loading pattern (db.Entry().Collection/Reference().Load()).
        /// </summary>
        private ExplicitLoadOperation? TryParseExplicitLoad(InvocationExpressionSyntax invocation)
        {
            var fullExpression = invocation.ToString();

            // Check for explicit load pattern
            if (!fullExpression.Contains(".Entry(", StringComparison.Ordinal))
            {
                return null;
            }

            var isCollection = fullExpression.Contains(".Collection(", StringComparison.Ordinal);
            var isReference = fullExpression.Contains(".Reference(", StringComparison.Ordinal);

            if (!isCollection && !isReference)
            {
                return null;
            }

            // Extract the entity variable from Entry(entity) or Entry(entity).Collection(...)
            var entityVariable = ExtractEntityFromEntry(fullExpression);
            var navigationProperty = isCollection
                ? ExtractNavigationFromCall(fullExpression, "Collection")
                : ExtractNavigationFromCall(fullExpression, "Reference");

            if (string.IsNullOrEmpty(entityVariable) || string.IsNullOrEmpty(navigationProperty))
            {
                return null;
            }

            return new ExplicitLoadOperation
            {
                EntityVariable = entityVariable,
                NavigationProperty = navigationProperty,
                IsCollection = isCollection,
                SourceCode = fullExpression,
                LineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line
            };
        }

        /// <summary>
        /// Extracts the entity variable from Entry(entity) call.
        /// </summary>
        private static string? ExtractEntityFromEntry(string expression)
        {
            // Pattern: ...Entry(entityVar)...
            var entryIdx = expression.IndexOf(".Entry(", StringComparison.Ordinal);
            if (entryIdx < 0) return null;

            var start = entryIdx + 7; // ".Entry(" length
            var parenCount = 1;
            var end = start;

            while (end < expression.Length && parenCount > 0)
            {
                if (expression[end] == '(') parenCount++;
                else if (expression[end] == ')') parenCount--;
                end++;
            }

            if (parenCount == 0 && end > start + 1)
            {
                return expression.Substring(start, end - start - 1).Trim();
            }

            return null;
        }

        /// <summary>
        /// Extracts the navigation property from Collection(x => x.Nav) or Reference(x => x.Nav) call.
        /// </summary>
        private static string? ExtractNavigationFromCall(string expression, string callType)
        {
            // Pattern: .Collection(x => x.NavigationProperty) or .Reference(x => x.NavigationProperty)
            var callIdx = expression.IndexOf($".{callType}(", StringComparison.Ordinal);
            if (callIdx < 0) return null;

            // Find the lambda: x => x.Property
            var arrowIdx = expression.IndexOf("=>", callIdx, StringComparison.Ordinal);
            if (arrowIdx < 0) return null;

            // Find the property access after the arrow
            var afterArrow = expression.Substring(arrowIdx + 2).Trim();
            var dotIdx = afterArrow.IndexOf('.');
            if (dotIdx < 0) return null;

            // Extract property name (handle closing paren)
            var propStart = dotIdx + 1;
            var propEnd = afterArrow.IndexOf(')', propStart);
            if (propEnd < 0) propEnd = afterArrow.Length;

            return afterArrow.Substring(propStart, propEnd - propStart).Trim();
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (node.Expression != null)
            {
                var expression = node.Expression.ToString();

                // Check if this return is inside an if/else statement
                // If so, it will be handled by the nested walker in VisitIfStatement
                var isInsideConditional = IsReturnInsideConditional(node);

                ReturnStatement = new ReturnInfo
                {
                    Expression = expression,
                    IsViewReturn = expression.Contains("View(", StringComparison.Ordinal),
                    IsRedirect = expression.Contains("Redirect", StringComparison.Ordinal),
                    IsErrorReturn = expression.Contains("NotFound", StringComparison.Ordinal) ||
                                   expression.Contains("BadRequest", StringComparison.Ordinal),
                    ReturnedModel = ExtractReturnedModel(node.Expression)
                };

                // Only add to top-level Statements if:
                // 1. We're in a nested context (processing inside an if/else block), OR
                // 2. We're NOT inside a conditional block
                // This prevents duplicate returns: parent walker skips conditional returns,
                // but nested walkers include them in their Statements (which become ChildStatements)
                if (_isNestedContext || !isInsideConditional)
                {
                    var statementInfo = new StatementInfo
                    {
                        Type = StatementType.Return,
                        SourceCode = node.ToString().Trim(),
                        LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                        NeedsAsyncTransform = ContainsAsyncMethod(expression)
                    };

                    Statements.Add(statementInfo);
                }
            }

            base.VisitReturnStatement(node);
        }

        /// <summary>
        /// Checks if a return statement is inside an if/else conditional block.
        /// </summary>
        private static bool IsReturnInsideConditional(ReturnStatementSyntax returnNode)
        {
            var parent = returnNode.Parent;
            while (parent != null)
            {
                // Check if parent is an if statement block or else clause
                if (parent is IfStatementSyntax)
                {
                    return true;
                }

                // Check if we're inside an else clause
                if (parent is ElseClauseSyntax)
                {
                    return true;
                }

                // Stop at method/block boundaries - don't go beyond the current method
                if (parent is MethodDeclarationSyntax)
                {
                    break;
                }

                parent = parent.Parent;
            }

            return false;
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            var condition = node.Condition.ToString();
            var lineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line;

            // Extract statements from if block using a nested walker
            var ifWalker = new LogicExtractionWalker(isNestedContext: true);
            if (node.Statement is BlockSyntax ifBlock)
            {
                ifWalker.Visit(ifBlock);
            }
            else
            {
                // Single statement without braces
                ifWalker.Visit(node.Statement);
            }

            var childStatements = ifWalker.Statements.ToList();

            // Check if this is a parameter null check (if (id != null) or if (id.HasValue))
            var parameterName = ExtractParameterFromCondition(condition);

            // Propagate ViewBag/ModelState usage from nested walker
            if (ifWalker.HasViewBagUsage)
            {
                HasViewBagUsage = true;
            }
            if (ifWalker.HasModelStateValidation)
            {
                HasModelStateValidation = true;
            }

            // Add warnings from nested walker
            Warnings.AddRange(ifWalker.Warnings);

            if (parameterName != null)
            {
                // Mark nested mutations as conditional
                var conditionalMutations = ifWalker.ViewModelMutations
                    .Select(m => m with { IsConditional = true, ConditionExpression = condition })
                    .ToList();

                // Create a ConditionalBlock for parameter-based conditions
                var conditionalBlock = new ConditionalBlock
                {
                    Condition = condition,
                    ParameterName = parameterName,
                    Statements = childStatements,
                    Mutations = conditionalMutations,
                    DbOperations = ifWalker.DbOperations.ToList(),
                    ExplicitLoads = ifWalker.ExplicitLoads.ToList(),
                    LineNumber = lineNumber
                };

                ConditionalBlocks.Add(conditionalBlock);

                // Add the conditional mutations to main list with conditional flag
                ViewModelMutations.AddRange(conditionalMutations);

                // Add nested DB operations and explicit loads
                DbOperations.AddRange(ifWalker.DbOperations);
                ExplicitLoads.AddRange(ifWalker.ExplicitLoads);
            }
            else
            {
                // Regular if statement - merge nested data
                ViewModelMutations.AddRange(ifWalker.ViewModelMutations);
                DbOperations.AddRange(ifWalker.DbOperations);
                ExplicitLoads.AddRange(ifWalker.ExplicitLoads);
            }

            var statementInfo = new StatementInfo
            {
                Type = StatementType.If,
                SourceCode = $"if ({condition})",
                LineNumber = lineNumber,
                ChildStatements = childStatements,
                NeedsAsyncTransform = childStatements.Any(s => s.NeedsAsyncTransform)
            };

            Statements.Add(statementInfo);

            // Handle else clause
            if (node.Else != null)
            {
                var elseWalker = new LogicExtractionWalker(isNestedContext: true);
                if (node.Else.Statement is BlockSyntax elseBlock)
                {
                    elseWalker.Visit(elseBlock);
                }
                else if (node.Else.Statement is IfStatementSyntax elseIfStmt)
                {
                    // else if - let it be handled recursively
                    // Don't visit here to avoid double processing
                }
                else
                {
                    elseWalker.Visit(node.Else.Statement);
                }

                var elseChildren = elseWalker.Statements.ToList();

                // Propagate ViewBag/ModelState usage from else block
                if (elseWalker.HasViewBagUsage)
                {
                    HasViewBagUsage = true;
                }
                if (elseWalker.HasModelStateValidation)
                {
                    HasModelStateValidation = true;
                }
                Warnings.AddRange(elseWalker.Warnings);

                // Merge nested data from else block
                ViewModelMutations.AddRange(elseWalker.ViewModelMutations);
                DbOperations.AddRange(elseWalker.DbOperations);
                ExplicitLoads.AddRange(elseWalker.ExplicitLoads);

                var elseStatement = new StatementInfo
                {
                    Type = StatementType.Else,
                    SourceCode = "else",
                    LineNumber = node.Else.GetLocation().GetLineSpan().StartLinePosition.Line,
                    ChildStatements = elseChildren,
                    NeedsAsyncTransform = elseChildren.Any(s => s.NeedsAsyncTransform)
                };

                Statements.Add(elseStatement);
            }

            // Don't call base to avoid double processing of nested statements
            // base.VisitIfStatement(node);
        }

        /// <summary>
        /// Extracts the parameter name from a condition expression.
        /// </summary>
        private static string? ExtractParameterFromCondition(string condition)
        {
            // Common patterns:
            // "id != null" → "id"
            // "id.HasValue" → "id"
            // "id == null" → "id"
            // "!string.IsNullOrEmpty(name)" → "name"
            // "courseID != null" → "courseID"

            // Pattern: paramName != null or paramName == null
            var nullCheckPattern = new System.Text.RegularExpressions.Regex(@"^(\w+)\s*[!=]=\s*null$");
            var match = nullCheckPattern.Match(condition.Trim());
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Pattern: paramName.HasValue
            var hasValuePattern = new System.Text.RegularExpressions.Regex(@"^(\w+)\.HasValue$");
            match = hasValuePattern.Match(condition.Trim());
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Pattern: !string.IsNullOrEmpty(paramName) or string.IsNullOrEmpty(paramName)
            var stringCheckPattern = new System.Text.RegularExpressions.Regex(@"!?string\.IsNullOrEmpty\s*\(\s*(\w+)\s*\)");
            match = stringCheckPattern.Match(condition.Trim());
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            var childStatements = new List<StatementInfo>();

            if (node.Statement is BlockSyntax block)
            {
                var walker = new LogicExtractionWalker();
                walker.Visit(block);
                childStatements.AddRange(walker.Statements);
            }

            var statementInfo = new StatementInfo
            {
                Type = StatementType.ForEach,
                SourceCode = $"foreach (var {node.Identifier} in {node.Expression})",
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                ChildStatements = childStatements,
                NeedsAsyncTransform = false
            };

            Statements.Add(statementInfo);

            base.VisitForEachStatement(node);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            var childStatements = new List<StatementInfo>();

            if (node.Statement is BlockSyntax block)
            {
                var walker = new LogicExtractionWalker();
                walker.Visit(block);
                childStatements.AddRange(walker.Statements);
            }

            var statementInfo = new StatementInfo
            {
                Type = StatementType.For,
                SourceCode = node.ToString().Trim().Split('\n')[0], // Just the for declaration
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                ChildStatements = childStatements,
                NeedsAsyncTransform = false
            };

            Statements.Add(statementInfo);

            base.VisitForStatement(node);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            var childStatements = new List<StatementInfo>();

            if (node.Statement is BlockSyntax block)
            {
                var walker = new LogicExtractionWalker();
                walker.Visit(block);
                childStatements.AddRange(walker.Statements);
            }

            var statementInfo = new StatementInfo
            {
                Type = StatementType.While,
                SourceCode = $"while ({node.Condition})",
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                ChildStatements = childStatements,
                NeedsAsyncTransform = false
            };

            Statements.Add(statementInfo);

            base.VisitWhileStatement(node);
        }

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            // Preserve the entire try-catch-finally block as-is
            var statementInfo = new StatementInfo
            {
                Type = StatementType.Try,
                SourceCode = node.ToString(),
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                ChildStatements = new List<StatementInfo>(),
                NeedsAsyncTransform = ContainsAsyncMethod(node.ToString())
            };

            Statements.Add(statementInfo);
            Warnings.Add("Try-catch block found - manual review recommended");

            // Don't call base - we've captured the entire block
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            // Preserve the entire switch statement block as-is
            var statementInfo = new StatementInfo
            {
                Type = StatementType.Switch,
                SourceCode = node.ToString(),
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                ChildStatements = new List<StatementInfo>(),
                NeedsAsyncTransform = ContainsAsyncMethod(node.ToString())
            };

            Statements.Add(statementInfo);
            Warnings.Add("Switch statement found - preserving as-is");

            // Don't call base - we've captured the entire block
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            var statementInfo = new StatementInfo
            {
                Type = StatementType.Throw,
                SourceCode = node.ToString().Trim(),
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                NeedsAsyncTransform = false
            };

            Statements.Add(statementInfo);

            base.VisitThrowStatement(node);
        }

        /// <summary>
        /// Visits LINQ query expressions (from ... select syntax).
        /// </summary>
        public override void VisitQueryExpression(QueryExpressionSyntax node)
        {
            // Extract information about the LINQ query
            var sourceCode = node.ToString();

            // Identify the query source
            var fromClause = node.FromClause;
            var sourceName = fromClause.Expression.ToString();

            // Check if this is a DbContext query
            var isDbQuery = sourceName.Contains("db.", StringComparison.OrdinalIgnoreCase) ||
                           sourceName.Contains("_context.", StringComparison.OrdinalIgnoreCase) ||
                           sourceName.Contains("context.", StringComparison.OrdinalIgnoreCase);

            if (isDbQuery)
            {
                // Extract LINQ operations from the query
                var linqOps = new List<string>();

                // Check for group by
                if (sourceCode.Contains("group ", StringComparison.OrdinalIgnoreCase))
                {
                    linqOps.Add("GroupBy");
                }

                // Check for select
                if (sourceCode.Contains("select ", StringComparison.OrdinalIgnoreCase))
                {
                    linqOps.Add("Select");
                }

                // Check for where
                if (sourceCode.Contains("where ", StringComparison.OrdinalIgnoreCase))
                {
                    linqOps.Add("Where");
                }

                // Check for join
                if (sourceCode.Contains("join ", StringComparison.OrdinalIgnoreCase))
                {
                    linqOps.Add("Join");
                }

                // Check for orderby
                if (sourceCode.Contains("orderby ", StringComparison.OrdinalIgnoreCase))
                {
                    linqOps.Add("OrderBy");
                }

                var dbOperation = new DbContextOperation
                {
                    OperationType = DbOperationType.Query,
                    EntityType = ExtractEntityTypeFromFrom(fromClause),
                    DbSetName = ExtractDbSetFromFrom(fromClause),
                    SourceCode = sourceCode,
                    LinqOperations = linqOps
                };

                DbOperations.Add(dbOperation);
            }

            base.VisitQueryExpression(node);
        }

        /// <summary>
        /// Extracts entity type from a LINQ from clause.
        /// </summary>
        private string? ExtractEntityTypeFromFrom(FromClauseSyntax fromClause)
        {
            // The identifier gives us the range variable name
            // The type (if specified) gives us the entity type
            if (fromClause.Type != null)
            {
                return fromClause.Type.ToString();
            }

            // Try to infer from expression (e.g., db.Students -> Student)
            var expression = fromClause.Expression.ToString();
            var lastDot = expression.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < expression.Length - 1)
            {
                var dbSetName = expression[(lastDot + 1)..];
                // Singularize if it looks like a collection
                if (dbSetName.EndsWith("s", StringComparison.Ordinal) &&
                    !dbSetName.EndsWith("ss", StringComparison.Ordinal))
                {
                    return dbSetName[..^1];
                }
                return dbSetName;
            }

            return null;
        }

        /// <summary>
        /// Extracts DbSet name from a LINQ from clause.
        /// </summary>
        private string? ExtractDbSetFromFrom(FromClauseSyntax fromClause)
        {
            var expression = fromClause.Expression.ToString();
            var lastDot = expression.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < expression.Length - 1)
            {
                return expression[(lastDot + 1)..];
            }
            return null;
        }

        /// <summary>
        /// Visits member access expressions to detect ViewBag/ViewData assignments and view model mutations.
        /// </summary>
        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            // HandleAssignmentForMutation is called from VisitExpressionStatement for top-level assignments
            // This method handles nested assignments and ensures we don't miss any
            base.VisitAssignmentExpression(node);
        }

        /// <summary>
        /// Handles assignment expressions to detect view model mutations.
        /// </summary>
        private void HandleAssignmentForMutation(AssignmentExpressionSyntax node)
        {
            var left = node.Left.ToString();
            var right = node.Right.ToString();
            var lineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line;

            // Detect ViewBag/ViewData assignments
            if (left.StartsWith("ViewBag.", StringComparison.Ordinal) ||
                left.StartsWith("ViewData[", StringComparison.Ordinal))
            {
                HasViewBagUsage = true;

                // Extract property name from ViewBag.PropertyName
                var propertyName = left.StartsWith("ViewBag.", StringComparison.Ordinal)
                    ? left.Substring(8)
                    : ExtractViewDataKey(left);

                ViewModelMutations.Add(new ViewModelMutation
                {
                    ViewModelVariable = "ViewBag",
                    PropertyName = propertyName,
                    AssignedValue = right,
                    LineNumber = lineNumber,
                    IsConditional = false
                });

                Warnings.Add($"ViewBag/ViewData assignment detected: {left} - consider adding to result DTO");
            }

            // Detect property assignments on view models (viewModel.Property = value)
            if (node.Left is MemberAccessExpressionSyntax memberAccess &&
                !left.StartsWith("this.", StringComparison.Ordinal))
            {
                var objectName = memberAccess.Expression.ToString();
                var propertyName = memberAccess.Name.ToString();

                // Check if this looks like a view model mutation
                var isViewModel = IsViewModelVariable(objectName);

                if (isViewModel)
                {
                    // Check if this mutation already exists (avoid duplicates)
                    var alreadyExists = ViewModelMutations.Any(m =>
                        m.ViewModelVariable == objectName &&
                        m.PropertyName == propertyName &&
                        m.LineNumber == lineNumber);

                    if (!alreadyExists)
                    {
                        ViewModelMutations.Add(new ViewModelMutation
                        {
                            ViewModelVariable = objectName,
                            PropertyName = propertyName,
                            AssignedValue = right,
                            LineNumber = lineNumber,
                            IsConditional = false
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a variable name represents a view model.
        /// </summary>
        private bool IsViewModelVariable(string objectName)
        {
            return objectName.EndsWith("viewModel", StringComparison.OrdinalIgnoreCase) ||
                   objectName.EndsWith("ViewModel", StringComparison.Ordinal) ||
                   objectName.EndsWith("model", StringComparison.OrdinalIgnoreCase) ||
                   objectName.EndsWith("result", StringComparison.OrdinalIgnoreCase) ||
                   objectName.EndsWith("Data", StringComparison.Ordinal) ||
                   objectName.Equals("viewModel", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Equals("model", StringComparison.OrdinalIgnoreCase) ||
                   Variables.Any(v => v.Name == objectName && IsViewModelType(v.Type));
        }

        /// <summary>
        /// Checks if a type name represents a view model type.
        /// </summary>
        private static bool IsViewModelType(string typeName)
        {
            return typeName.Contains("ViewModel", StringComparison.OrdinalIgnoreCase) ||
                   typeName.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
                   typeName.Contains("Data", StringComparison.Ordinal) ||
                   typeName.Contains("Result", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the key from ViewData["key"] pattern.
        /// </summary>
        private static string ExtractViewDataKey(string viewDataAccess)
        {
            // ViewData["Key"] → Key
            var start = viewDataAccess.IndexOf('[') + 1;
            var end = viewDataAccess.LastIndexOf(']');
            if (start > 0 && end > start)
            {
                var key = viewDataAccess.Substring(start, end - start).Trim('"', '\'');
                return key;
            }
            return "Unknown";
        }

        /// <summary>
        /// Extracts the method name from an invocation.
        /// </summary>
        private string? ExtractMethodName(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression switch
            {
                IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                _ => null
            };
        }

        /// <summary>
        /// Extracts the target object from an invocation.
        /// </summary>
        private string? ExtractTarget(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Expression.ToString();
            }

            return null;
        }

        /// <summary>
        /// Extracts arguments from an invocation.
        /// </summary>
        private IReadOnlyList<string> ExtractArguments(InvocationExpressionSyntax invocation)
        {
            return invocation.ArgumentList.Arguments
                .Select(arg => arg.ToString())
                .ToList();
        }

        /// <summary>
        /// Checks if a method name is a DbContext method.
        /// </summary>
        private bool IsDbContextMethod(string methodName)
        {
            return DbContextMethods.Contains(methodName);
        }

        /// <summary>
        /// Checks if an invocation is a LINQ chain.
        /// </summary>
        private bool IsLinqChain(InvocationExpressionSyntax invocation)
        {
            var methodName = ExtractMethodName(invocation);
            if (methodName == null || !LinqMethods.Contains(methodName))
            {
                return false;
            }

            // Check if the target is also a LINQ method (chained)
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is InvocationExpressionSyntax)
            {
                return true;
            }

            return true;
        }

        /// <summary>
        /// Checks if a call is to a service (private field pattern).
        /// </summary>
        private bool IsServiceCall(string target, string methodName)
        {
            // Service calls typically start with underscore or lowercase (fields)
            // And are not common framework methods
            return (target.StartsWith("_", StringComparison.Ordinal) ||
                    (target.Length > 0 && char.IsLower(target[0]))) &&
                   !IsDbContextMethod(methodName) &&
                   !target.Contains(".", StringComparison.Ordinal); // Not a static call
        }

        /// <summary>
        /// Checks if a method should be transformed to async.
        /// </summary>
        private static bool ShouldBeAsync(string methodName)
        {
            return AsyncMethodMappings.ContainsKey(methodName);
        }

        /// <summary>
        /// Gets the async equivalent of a sync method name.
        /// </summary>
        private static string? GetAsyncEquivalent(string methodName)
        {
            return AsyncMethodMappings.TryGetValue(methodName, out var asyncName)
                ? asyncName
                : null;
        }

        /// <summary>
        /// Checks if code contains async method calls.
        /// </summary>
        private static bool ContainsAsyncMethod(string code)
        {
            foreach (var syncMethod in AsyncMethodMappings.Keys)
            {
                if (code.Contains($".{syncMethod}(", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts DbContext operation from an invocation.
        /// </summary>
        private DbContextOperation? ExtractDbContextOperation(InvocationExpressionSyntax invocation)
        {
            var methodName = ExtractMethodName(invocation);
            if (methodName == null)
            {
                return null;
            }

            var operationType = DetermineOperationType(methodName);
            var linqOps = ExtractLinqOperations(invocation);
            var entityType = ExtractEntityType(invocation);

            return new DbContextOperation
            {
                OperationType = operationType,
                EntityType = entityType,
                DbSetName = ExtractDbSetName(invocation),
                SourceCode = invocation.ToString(),
                LinqOperations = linqOps
            };
        }

        /// <summary>
        /// Determines the type of DbContext operation.
        /// </summary>
        private DbOperationType DetermineOperationType(string methodName)
        {
            return methodName switch
            {
                "SaveChanges" or "SaveChangesAsync" => DbOperationType.SaveChanges,
                "Find" or "FindAsync" => DbOperationType.Find,
                "Add" or "AddAsync" or "AddRange" or "AddRangeAsync" => DbOperationType.Add,
                "Update" or "UpdateRange" => DbOperationType.Update,
                "Remove" or "RemoveRange" => DbOperationType.Remove,
                "Entry" => DbOperationType.Entry,
                _ when LinqMethods.Contains(methodName) => DbOperationType.Query,
                _ => DbOperationType.Other
            };
        }

        /// <summary>
        /// Extracts LINQ operations from a LINQ chain.
        /// </summary>
        private IReadOnlyList<string> ExtractLinqOperations(InvocationExpressionSyntax invocation)
        {
            var operations = new List<string>();
            var current = invocation;

            while (current != null)
            {
                var methodName = ExtractMethodName(current);
                if (methodName != null && LinqMethods.Contains(methodName))
                {
                    operations.Insert(0, methodName);
                }

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

            return operations;
        }

        /// <summary>
        /// Extracts entity type from a DbContext operation.
        /// </summary>
        private string? ExtractEntityType(InvocationExpressionSyntax invocation)
        {
            // Try to extract from DbSet<T> pattern
            var fullExpression = invocation.ToString();

            // Look for pattern like: db.Users or _context.Products
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var dbSetName = memberAccess.Name.Identifier.Text;
                // Assume DbSet name is plural of entity type
                return dbSetName;
            }

            return null;
        }

        /// <summary>
        /// Extracts DbSet property name from a DbContext operation.
        /// </summary>
        private string? ExtractDbSetName(InvocationExpressionSyntax invocation)
        {
            // Walk up the invocation chain to find the DbSet access
            var current = invocation.Expression;

            while (current is MemberAccessExpressionSyntax memberAccess)
            {
                // Check if this looks like a DbSet property access
                var name = memberAccess.Name.Identifier.Text;
                if (char.IsUpper(name[0])) // DbSet properties are typically PascalCase
                {
                    return name;
                }

                current = memberAccess.Expression;
            }

            return null;
        }

        /// <summary>
        /// Extracts the model being returned from a return expression.
        /// </summary>
        private string? ExtractReturnedModel(ExpressionSyntax expression)
        {
            // Try to extract from View(model), Ok(model), etc.
            if (expression is InvocationExpressionSyntax invocation &&
                invocation.ArgumentList.Arguments.Count > 0)
            {
                return invocation.ArgumentList.Arguments[0].ToString();
            }

            return null;
        }
    }
}
