using System.Text;
using NetLift.Core.Models.Modernization;

namespace NetLift.Transforms.Modernization.Generators;

/// <summary>
/// Converts ExtractedLogic into handler-ready business logic code.
/// Transforms service calls to DbContext operations and handles async conversion.
/// </summary>
public sealed class BusinessLogicBuilder
{
    private static readonly Dictionary<string, string> SyncToAsyncMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ToList", "ToListAsync" },
        { "ToArray", "ToArrayAsync" },
        { "First", "FirstAsync" },
        { "FirstOrDefault", "FirstOrDefaultAsync" },
        { "Single", "SingleAsync" },
        { "SingleOrDefault", "SingleOrDefaultAsync" },
        { "Count", "CountAsync" },
        { "LongCount", "LongCountAsync" },
        { "Any", "AnyAsync" },
        { "All", "AllAsync" },
        { "Find", "FindAsync" },
        { "SaveChanges", "SaveChangesAsync" },
        { "Add", "AddAsync" },
        { "AddRange", "AddRangeAsync" },
    };

    /// <summary>
    /// Builds handler business logic from extracted logic.
    /// Preserves the original logic structure, just adapts syntax for CQRS handlers.
    /// </summary>
    /// <param name="logic">The extracted logic.</param>
    /// <param name="isCommand">True for commands, false for queries.</param>
    /// <param name="entityName">The main entity name for context operations.</param>
    /// <returns>Generated business logic code.</returns>
    public string Build(ExtractedLogic logic, bool isCommand, string? entityName = null)
    {
        ArgumentNullException.ThrowIfNull(logic);

        if (logic.Statements.Count == 0 && logic.DbOperations.Count == 0 && logic.ServiceCalls.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        // Add TODO comments for explicit loads that should use Include
        if (logic.ExplicitLoads.Count > 0)
        {
            sb.AppendLine("// TODO: Consider converting explicit loads to Include/ThenInclude for better performance:");
            foreach (var load in logic.ExplicitLoads)
            {
                sb.AppendLine($"// - {load.EntityVariable}.{load.NavigationProperty} ({(load.IsCollection ? "Collection" : "Reference")})");
            }
            sb.AppendLine();
        }

        // Filter statements to avoid multiple sequential returns
        // Keep only the LAST return statement at the top level, skip any code after it
        var filteredStatements = FilterSequentialReturns(logic.Statements);

        // Track if we've output any return statements (to avoid duplicates)
        var hasReturnStatement = false;
        // Track if we've processed a ModelState.IsValid block - statements after it are the "invalid" path
        var processedModelStateBlock = false;

        // Process all statements in order - preserving the original logic structure
        foreach (var statement in filteredStatements)
        {
            // Skip statements AFTER a ModelState.IsValid block that ended with a return
            // These are the "validation failed" fallback path - not needed with pipeline validation
            if (processedModelStateBlock && hasReturnStatement)
            {
                // Add comment explaining why remaining code is skipped
                if (!sb.ToString().Contains("// Validation failure path removed"))
                {
                    sb.AppendLine("// Validation failure path removed - handled by pipeline validation");
                }
                break;
            }

            if (ShouldSkipStatement(statement))
            {
                continue;
            }

            // Check if this is a ModelState.IsValid if-block
            var isModelStateBlock = statement.Type == StatementType.If &&
                statement.SourceCode.Contains("ModelState.IsValid");

            // Track return statements to avoid duplicate handling
            if (statement.Type == StatementType.Return)
            {
                hasReturnStatement = true;
            }

            var transformed = TransformStatementPreservingStructure(statement, isCommand);
            if (!string.IsNullOrWhiteSpace(transformed))
            {
                sb.AppendLine(transformed);

                // If we just processed ModelState block and it contains a return, mark it
                if (isModelStateBlock && transformed.Contains("return "))
                {
                    processedModelStateBlock = true;
                    hasReturnStatement = true;
                }
            }
        }

        // Only handle ReturnStatement if no returns were found in Statements
        // This handles cases where the extractor stored the return separately
        if (!hasReturnStatement && logic.ReturnStatement != null)
        {
            var returnCode = BuildReturnStatement(logic.ReturnStatement, isCommand);
            if (!string.IsNullOrWhiteSpace(returnCode))
            {
                sb.AppendLine();
                sb.AppendLine(returnCode);
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Filters statements to remove sequential returns and unreachable code.
    /// Keeps the last meaningful return and removes any returns that would cause unreachable code.
    /// </summary>
    private static List<StatementInfo> FilterSequentialReturns(IReadOnlyList<StatementInfo> statements)
    {
        var result = new List<StatementInfo>();
        var foundTopLevelReturn = false;

        for (int i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];

            // If we already found a top-level return, skip remaining statements (unreachable)
            if (foundTopLevelReturn)
            {
                continue;
            }

            // If this is a return statement at top level
            if (statement.Type == StatementType.Return)
            {
                // Check if there are more non-return statements after this
                var hasMoreStatements = statements.Skip(i + 1).Any(s =>
                    s.Type != StatementType.Return &&
                    s.Type != StatementType.If &&
                    s.Type != StatementType.Else);

                if (hasMoreStatements)
                {
                    // This return would cause unreachable code - skip it
                    // The logic was probably extracted incorrectly from conditionals
                    continue;
                }

                foundTopLevelReturn = true;
            }

            result.Add(statement);
        }

        return result;
    }

    /// <summary>
    /// Transforms a statement while preserving its structure.
    /// Applies syntax adaptations for CQRS handlers without restructuring logic.
    /// </summary>
    private string TransformStatementPreservingStructure(StatementInfo statement, bool isCommand)
    {
        var code = statement.SourceCode;

        // SPECIAL CASE: If statement with ModelState.IsValid condition
        // We want to SKIP the if wrapper but KEEP the child statements (the actual business logic)
        if (statement.Type == StatementType.If && code.Contains("ModelState.IsValid") && statement.ChildStatements.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// TODO: Add validation (e.g., FluentValidation in request pipeline)");

            // Process all child statements (this is the actual business logic)
            foreach (var child in statement.ChildStatements)
            {
                var transformed = TransformStatementPreservingStructure(child, isCommand);
                if (!string.IsNullOrWhiteSpace(transformed))
                {
                    sb.AppendLine(transformed);
                }
            }

            return sb.ToString().TrimEnd();
        }

        // SPECIAL CASE: Try statement containing if (ModelState.IsValid)
        // Parse the try block to unwrap the ModelState.IsValid check while preserving try-catch
        if (statement.Type == StatementType.Try && code.Contains("ModelState.IsValid"))
        {
            return UnwrapModelStateFromTryBlock(code, isCommand);
        }

        // Skip standalone ModelState checks (shouldn't happen after above cases, but safety check)
        if (code.Contains("ModelState.IsValid"))
        {
            return "// TODO: Add validation (e.g., FluentValidation in request pipeline)";
        }

        // Handle return statements - transform MVC returns to Result returns
        if (statement.Type == StatementType.Return)
        {
            return TransformReturnStatement(code, isCommand);
        }

        // Apply syntax transformations
        code = TransformLegacyMvcPatterns(code);   // TryUpdateModelAsync, ModelState, StatusCode
        code = TransformServiceCall(code);          // service.FindX() → _context.Xs.FirstOrDefaultAsync()
        code = TransformDbContextField(code);      // db. → _context.
        code = TransformViewModelName(code);        // viewModel. → result.
        code = TransformViewBagToResult(code);      // ViewBag.X = → result.X =
        code = TransformToAsync(code);              // ToList() → ToListAsync()
        code = AddCancellationToken(code);          // Add cancellationToken to async calls

        // Add await if async call not already awaited
        if (code.Contains("Async") && !code.TrimStart().StartsWith("await") && !code.Contains("return"))
        {
            var eqIndex = code.IndexOf('=');
            if (eqIndex > 0 && !code.Contains("==") && !code.Contains("!="))
            {
                var beforeEq = code.Substring(0, eqIndex + 1);
                var afterEq = code.Substring(eqIndex + 1).Trim();
                code = $"{beforeEq} await {afterEq}";
            }
            else if (!code.Contains("="))
            {
                code = $"await {code}";
            }
        }

        // Handle if statements with children
        if (statement.Type == StatementType.If && statement.ChildStatements.Count > 0)
        {
            return BuildIfBlockPreservingStructure(statement, isCommand);
        }

        // Handle else statements with children
        if (statement.Type == StatementType.Else && statement.ChildStatements.Count > 0)
        {
            return BuildElseBlockPreservingStructure(statement, isCommand);
        }

        // Handle foreach statements
        if (statement.Type == StatementType.ForEach && statement.ChildStatements.Count > 0)
        {
            return BuildForEachBlockPreservingStructure(statement, isCommand);
        }

        return code;
    }

    /// <summary>
    /// Transforms MVC return statements to proper Result returns.
    /// Handles View(), RedirectToAction(), NotFound(), BadRequest(), etc.
    /// </summary>
    private string TransformReturnStatement(string code, bool isCommand)
    {
        // Extract the expression from "return X;"
        var returnMatch = System.Text.RegularExpressions.Regex.Match(code, @"return\s+(.+?);?\s*$");
        if (!returnMatch.Success)
        {
            return code;
        }

        var expression = returnMatch.Groups[1].Value.Trim().TrimEnd(';');

        // View() or View(model) → return Result<T>.Success(model)
        if (expression.StartsWith("View(", StringComparison.Ordinal))
        {
            // Extract the content inside View() handling nested parentheses
            var model = ExtractParenthesesContent(expression, 5); // 5 = "View(".Length
            if (string.IsNullOrEmpty(model))
            {
                return isCommand ? "return Result.Success();" : "return Result.Success();";
            }
            // Remove view name if it's a string literal (first argument is view name)
            if (model.StartsWith("\"", StringComparison.Ordinal))
            {
                var commaIdx = FindTopLevelComma(model);
                if (commaIdx > 0)
                {
                    model = model.Substring(commaIdx + 1).Trim();
                }
                else
                {
                    return isCommand ? "return Result.Success();" : "return Result.Success();";
                }
            }
            // Transform viewModel. to result.
            model = TransformViewModelName(model);
            return $"return Result.Success({model});";
        }

        // RedirectToAction() → return Result.Success()
        if (expression.StartsWith("RedirectToAction(", StringComparison.Ordinal) ||
            expression.StartsWith("Redirect(", StringComparison.Ordinal) ||
            expression.StartsWith("RedirectToRoute(", StringComparison.Ordinal))
        {
            return "return Result.Success();";
        }

        // NotFound() → return Result.Failure("Not found")
        if (expression.StartsWith("NotFound(", StringComparison.Ordinal) || expression == "NotFound()")
        {
            return "return Result.Failure(\"Not found\");";
        }

        // HttpNotFound() → return Result.Failure("Not found")
        if (expression.StartsWith("HttpNotFound(", StringComparison.Ordinal) || expression == "HttpNotFound()")
        {
            return "return Result.Failure(\"Not found\");";
        }

        // BadRequest() → return Result.Failure("Invalid request")
        if (expression.StartsWith("BadRequest(", StringComparison.Ordinal) || expression == "BadRequest()")
        {
            var badRequestMatch = System.Text.RegularExpressions.Regex.Match(expression, @"BadRequest\(([^)]*)\)");
            if (badRequestMatch.Success && !string.IsNullOrEmpty(badRequestMatch.Groups[1].Value))
            {
                return $"return Result.Failure({badRequestMatch.Groups[1].Value});";
            }
            return "return Result.Failure(\"Invalid request\");";
        }

        // Json(data) → return Result.Success(data)
        if (expression.StartsWith("Json(", StringComparison.Ordinal))
        {
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(expression, @"Json\(([^,)]+)");
            if (jsonMatch.Success)
            {
                var data = TransformViewModelName(jsonMatch.Groups[1].Value.Trim());
                return $"return Result.Success({data});";
            }
        }

        // Ok(data) or Ok() → return Result.Success(data) or Result.Success()
        if (expression.StartsWith("Ok(", StringComparison.Ordinal) || expression == "Ok()")
        {
            var okMatch = System.Text.RegularExpressions.Regex.Match(expression, @"Ok\(([^)]*)\)");
            if (okMatch.Success && !string.IsNullOrEmpty(okMatch.Groups[1].Value))
            {
                var data = TransformViewModelName(okMatch.Groups[1].Value.Trim());
                return $"return Result.Success({data});";
            }
            return "return Result.Success();";
        }

        // Content() → return Result.Success()
        if (expression.StartsWith("Content(", StringComparison.Ordinal))
        {
            return "return Result.Success();";
        }

        // PartialView() → return Result.Success()
        if (expression.StartsWith("PartialView(", StringComparison.Ordinal))
        {
            var partialMatch = System.Text.RegularExpressions.Regex.Match(expression, @"PartialView\([^,]*,\s*([^)]+)\)");
            if (partialMatch.Success)
            {
                var model = TransformViewModelName(partialMatch.Groups[1].Value.Trim());
                return $"return Result.Success({model});";
            }
            return "return Result.Success();";
        }

        // If it's already a Result return, keep it
        if (expression.Contains("Result.Success") || expression.Contains("Result.Failure") ||
            expression.Contains("Result<"))
        {
            return code;
        }

        // Default: wrap in Result.Success if it looks like returning data
        // Transform field references
        expression = TransformDbContextField(expression);
        expression = TransformViewModelName(expression);

        return $"return Result.Success({expression});";
    }

    /// <summary>
    /// Unwraps ModelState.IsValid check from inside a try-catch block.
    /// Preserves the try-catch structure but removes the if (ModelState.IsValid) wrapper.
    /// </summary>
    private string UnwrapModelStateFromTryBlock(string tryBlockCode, bool isCommand)
    {
        // Parse the try-catch block using Roslyn to properly extract and transform it
        var wrappedCode = $"class Temp {{ void Method() {{ {tryBlockCode} }} }}";

        try
        {
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(wrappedCode);
            var root = tree.GetRoot();
            var tryStatement = root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TryStatementSyntax>()
                .FirstOrDefault();

            if (tryStatement == null)
            {
                // Couldn't parse - return with TODO
                return $"// TODO: Manual review needed - ModelState.IsValid inside try-catch\n{tryBlockCode}";
            }

            var sb = new StringBuilder();
            sb.AppendLine("// TODO: Add validation (e.g., FluentValidation in request pipeline)");
            sb.AppendLine("try");
            sb.AppendLine("{");

            // Extract statements from try block, skipping if (ModelState.IsValid) wrapper
            var tryBlock = tryStatement.Block;
            if (tryBlock != null)
            {
                foreach (var statement in tryBlock.Statements)
                {
                    // Check if this is the if (ModelState.IsValid) statement
                    if (statement is Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax ifStmt)
                    {
                        var condition = ifStmt.Condition.ToString();
                        if (condition.Contains("ModelState.IsValid", StringComparison.Ordinal))
                        {
                            // Unwrap: extract statements from inside the if block
                            if (ifStmt.Statement is Microsoft.CodeAnalysis.CSharp.Syntax.BlockSyntax ifBlock)
                            {
                                foreach (var innerStatement in ifBlock.Statements)
                                {
                                    var transformed = TransformCodeSnippet(innerStatement.ToString(), isCommand);
                                    sb.AppendLine($"    {transformed}");
                                }
                            }
                            else
                            {
                                // Single statement without braces
                                var transformed = TransformCodeSnippet(ifStmt.Statement.ToString(), isCommand);
                                sb.AppendLine($"    {transformed}");
                            }
                            continue;
                        }
                    }

                    // Not a ModelState.IsValid check - keep as-is
                    var transformedStmt = TransformCodeSnippet(statement.ToString(), isCommand);
                    sb.AppendLine($"    {transformedStmt}");
                }
            }

            sb.AppendLine("}");

            // Preserve catch blocks
            foreach (var catchClause in tryStatement.Catches)
            {
                var catchDecl = catchClause.Declaration;
                if (catchDecl != null)
                {
                    // Extract exception type and optional variable name
                    var exceptionType = catchDecl.Type.ToString();
                    exceptionType = exceptionType.Replace("RetryLimitExceededException", "DbUpdateException");

                    // Include variable name if present (e.g., "ex" in "catch (Exception ex)")
                    var identifier = catchDecl.Identifier.ToString();
                    if (!string.IsNullOrEmpty(identifier))
                    {
                        sb.AppendLine($"catch ({exceptionType} {identifier})");
                    }
                    else
                    {
                        sb.AppendLine($"catch ({exceptionType})");
                    }
                }
                else
                {
                    sb.AppendLine("catch");
                }

                sb.AppendLine("{");
                if (catchClause.Block != null)
                {
                    foreach (var catchStatement in catchClause.Block.Statements)
                    {
                        var transformed = TransformCodeSnippet(catchStatement.ToString(), isCommand);
                        sb.AppendLine($"    {transformed}");
                    }
                }
                sb.AppendLine("}");
            }

            // Preserve finally block if exists
            if (tryStatement.Finally != null)
            {
                sb.AppendLine("finally");
                sb.AppendLine("{");
                if (tryStatement.Finally.Block != null)
                {
                    foreach (var finallyStatement in tryStatement.Finally.Block.Statements)
                    {
                        var transformed = TransformCodeSnippet(finallyStatement.ToString(), isCommand);
                        sb.AppendLine($"    {transformed}");
                    }
                }
                sb.AppendLine("}");
            }

            return sb.ToString().TrimEnd();
        }
        catch
        {
            // If parsing fails, return with TODO
            return $"// TODO: Manual review needed - complex try-catch with ModelState.IsValid\n{tryBlockCode}";
        }
    }

    /// <summary>
    /// Transforms a code snippet (applies all standard transformations).
    /// </summary>
    private string TransformCodeSnippet(string code, bool isCommand)
    {
        code = TransformLegacyMvcPatterns(code);
        code = TransformServiceCall(code);
        code = TransformDbContextField(code);
        code = TransformViewModelName(code);
        code = TransformViewBagToResult(code);
        code = TransformToAsync(code);
        code = AddCancellationToken(code);

        // Add await if async call not already awaited
        if (code.Contains("Async") && !code.TrimStart().StartsWith("await") && !code.Contains("return"))
        {
            var eqIndex = code.IndexOf('=');
            if (eqIndex > 0 && !code.Contains("==") && !code.Contains("!="))
            {
                var beforeEq = code.Substring(0, eqIndex + 1);
                var afterEq = code.Substring(eqIndex + 1).Trim();
                code = $"{beforeEq} await {afterEq}";
            }
            else if (!code.Contains("="))
            {
                code = $"await {code}";
            }
        }

        return code;
    }

    /// <summary>
    /// Builds an if block preserving its structure.
    /// </summary>
    private string BuildIfBlockPreservingStructure(StatementInfo ifStatement, bool isCommand)
    {
        var sb = new StringBuilder();

        // Transform the condition (parameter names)
        var condition = TransformConditionForHandler(ifStatement.SourceCode);
        sb.AppendLine(condition);
        sb.AppendLine("{");

        foreach (var child in ifStatement.ChildStatements)
        {
            var transformed = TransformStatementPreservingStructure(child, isCommand);
            if (!string.IsNullOrWhiteSpace(transformed))
            {
                sb.AppendLine($"    {transformed}");
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    /// <summary>
    /// Builds an else block preserving its structure.
    /// </summary>
    private string BuildElseBlockPreservingStructure(StatementInfo elseStatement, bool isCommand)
    {
        var sb = new StringBuilder();
        sb.AppendLine("else");
        sb.AppendLine("{");

        foreach (var child in elseStatement.ChildStatements)
        {
            var transformed = TransformStatementPreservingStructure(child, isCommand);
            if (!string.IsNullOrWhiteSpace(transformed))
            {
                sb.AppendLine($"    {transformed}");
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a foreach block preserving its structure.
    /// </summary>
    private string BuildForEachBlockPreservingStructure(StatementInfo foreachStatement, bool isCommand)
    {
        var sb = new StringBuilder();

        // Transform the foreach header
        var header = TransformDbContextField(foreachStatement.SourceCode);
        header = TransformViewModelName(header);
        sb.AppendLine(header);
        sb.AppendLine("{");

        foreach (var child in foreachStatement.ChildStatements)
        {
            var transformed = TransformStatementPreservingStructure(child, isCommand);
            if (!string.IsNullOrWhiteSpace(transformed))
            {
                sb.AppendLine($"    {transformed}");
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    /// <summary>
    /// Transforms condition expressions for handler.
    /// Note: Parameter to request.Property transformation is handled by TransformParameterReferences
    /// which has proper local variable detection to avoid transforming local vars.
    /// </summary>
    private static string TransformConditionForHandler(string condition)
    {
        // Just return the condition as-is. Parameter references will be transformed later
        // by TransformParameterReferences which properly excludes local variables.
        return condition;
    }

    /// <summary>
    /// Transforms viewModel. to result.
    /// </summary>
    private string TransformViewModelName(string code)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            code,
            @"\bviewModel\.",
            "result.",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Transforms ViewBag.Property = value to result.Property = value.
    /// </summary>
    private string TransformViewBagToResult(string code)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            code,
            @"\bViewBag\.(\w+)\s*=",
            "result.$1 =",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Transforms legacy MVC controller patterns to CQRS handler patterns.
    /// </summary>
    private static string TransformLegacyMvcPatterns(string code)
    {
        // TryUpdateModelAsync - cannot be automated, add TODO
        if (code.Contains("TryUpdateModelAsync"))
        {
            code = System.Text.RegularExpressions.Regex.Replace(
                code,
                @"if\s*\(\s*await\s+TryUpdateModelAsync\s*\([^)]+\)\s*\)",
                "// TODO: TryUpdateModelAsync is controller model-binding. Add properties to Command and update entity directly.\n        // Example: entity.Property = request.Property;\n        if (true /* replace with validation */)",
                System.Text.RegularExpressions.RegexOptions.Singleline);
        }

        // ModelState.AddModelError → return Result.Failure
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"ModelState\.AddModelError\s*\(\s*""[^""]*""\s*,\s*""([^""]+)""\s*\)\s*;",
            "return Result.Failure(\"$1\");",
            System.Text.RegularExpressions.RegexOptions.None);

        // StatusCode(HttpStatusCode.BadRequest) → Result.Failure
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"StatusCode\s*\(\s*HttpStatusCode\.(\w+)\s*\)",
            "Result.Failure(\"$1\")",
            System.Text.RegularExpressions.RegexOptions.None);

        // Remove RetryLimitExceededException (EF6) - use DbUpdateException in EF Core
        code = code.Replace("RetryLimitExceededException", "DbUpdateException");

        // Transform MVC return statements to Result returns (handles returns inside try-catch and other blocks)
        code = TransformMvcReturnsInCode(code);

        return code;
    }

    /// <summary>
    /// Transforms MVC return statements anywhere in code (including inside try-catch blocks).
    /// Preserves original redirect info in TODO comment for developer review.
    /// </summary>
    private static string TransformMvcReturnsInCode(string code)
    {
        // RedirectToAction(...) → Result.Success() with TODO preserving original
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"return\s+RedirectToAction\s*\(([^;]+)\)\s*;",
            "// TODO: Was RedirectToAction($1) - use Result.Failure if this was error handling\n        return Result.Success();",
            System.Text.RegularExpressions.RegexOptions.None);

        // Redirect(...) → Result.Success() with TODO
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"return\s+Redirect\s*\(([^;]+)\)\s*;",
            "// TODO: Was Redirect($1) - use Result.Failure if this was error handling\n        return Result.Success();",
            System.Text.RegularExpressions.RegexOptions.None);

        // RedirectToRoute(...) → Result.Success() with TODO
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"return\s+RedirectToRoute\s*\(([^;]+)\)\s*;",
            "// TODO: Was RedirectToRoute($1) - use Result.Failure if this was error handling\n        return Result.Success();",
            System.Text.RegularExpressions.RegexOptions.None);

        // HttpNotFound() → Result.Failure("Not found")
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"return\s+HttpNotFound\s*\([^)]*\)\s*;",
            "return Result.Failure(\"Not found\");",
            System.Text.RegularExpressions.RegexOptions.None);

        // NotFound() → Result.Failure("Not found")
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"return\s+NotFound\s*\([^)]*\)\s*;",
            "return Result.Failure(\"Not found\");",
            System.Text.RegularExpressions.RegexOptions.None);

        // BadRequest(...) → Result.Failure(...)
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"return\s+BadRequest\s*\(\s*\)\s*;",
            "return Result.Failure(\"Invalid request\");",
            System.Text.RegularExpressions.RegexOptions.None);

        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"return\s+BadRequest\s*\(\s*""([^""]+)""\s*\)\s*;",
            "return Result.Failure(\"$1\");",
            System.Text.RegularExpressions.RegexOptions.None);

        return code;
    }

    /// <summary>
    /// Builds business logic from combined action and service method logic.
    /// Inlines service method bodies into the handler.
    /// </summary>
    public string BuildFromActionContext(ActionLogicContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var logic = context.CombinedLogic ?? context.ActionLogic;
        if (logic == null)
        {
            return string.Empty;
        }

        var entityName = ExtractEntityName(context.Controller.ClassName);
        var businessLogic = Build(logic, context.GenerateCommand, entityName);

        // Transform parameter references to request.ParameterName
        if (!string.IsNullOrEmpty(businessLogic) && context.Action.Parameters.Count > 0)
        {
            businessLogic = TransformParameterReferences(businessLogic, context.Action.Parameters);
        }

        // Inline private method calls
        if (!string.IsNullOrEmpty(businessLogic) && context.Controller.PrivateMethods.Count > 0)
        {
            var relevantPrivateMethods = context.Controller.PrivateMethods
                .Where(pm => pm.CallingActions.Contains(context.Action.Name))
                .ToList();

            if (relevantPrivateMethods.Count > 0)
            {
                businessLogic = InlinePrivateMethodCalls(businessLogic, relevantPrivateMethods);
            }
        }

        return businessLogic;
    }

    /// <summary>
    /// Transforms parameter references to use request.ParameterName syntax.
    /// </summary>
    private string TransformParameterReferences(string code, IReadOnlyList<Core.Models.Modernization.ActionParameter> parameters)
    {
        // First, find all local variable declarations to exclude them from transformation
        var localVariables = ExtractLocalVariableNames(code);

        foreach (var param in parameters)
        {
            var paramName = param.Name;
            var pascalName = char.ToUpperInvariant(paramName[0]) + paramName.Substring(1);

            // Skip if this parameter name matches a local variable (local takes precedence)
            if (localVariables.Contains(paramName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // Pattern: standalone parameter name used as value/argument, not as variable declaration
            // Avoid matching:
            // - When it's already part of request.X
            // - When it's a property access (something.paramName)
            // - When it's a variable declaration (Type paramName = ...)
            // - When it's a type name followed by variable name

            // Use word boundary but exclude common declaration patterns
            var escapedParam = System.Text.RegularExpressions.Regex.Escape(paramName);
            // Pattern matches: param name not preceded by alphanumeric, underscore, dot, or "request."
            // Followed by word boundary (operators, punctuation, whitespace, end of line)
            var pattern = @"(?<![a-zA-Z0-9_\.])(?<!request\.)" + escapedParam + @"(?![a-zA-Z0-9_])";
            code = System.Text.RegularExpressions.Regex.Replace(
                code,
                pattern,
                $"request.{pascalName}",
                System.Text.RegularExpressions.RegexOptions.None);
        }

        return code;
    }

    /// <summary>
    /// Extracts local variable names from code to prevent transforming them to request properties.
    /// </summary>
    private static HashSet<string> ExtractLocalVariableNames(string code)
    {
        var localVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pattern: Type variableName = ... or var variableName = ...
        // Matches: "Student student =", "var instructor =", "Course course ="
        var declarationPattern = @"(?:var|[A-Z][a-zA-Z0-9_<>]*)\s+([a-z][a-zA-Z0-9_]*)\s*=";
        var matches = System.Text.RegularExpressions.Regex.Matches(code, declarationPattern);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                localVars.Add(match.Groups[1].Value);
            }
        }

        return localVars;
    }

    private string TransformDbOperation(DbContextOperation dbOp)
    {
        var source = dbOp.SourceCode.Trim();

        // Transform sync to async
        source = TransformToAsync(source);

        // Add cancellationToken to async calls
        if (source.Contains("Async"))
        {
            source = AddCancellationToken(source);
        }

        // Transform field names (db -> _context)
        source = TransformDbContextField(source);

        return source;
    }

    private string TransformVariableDeclaration(VariableInfo variable)
    {
        var initializer = variable.Initializer ?? string.Empty;

        // Transform service calls
        initializer = TransformServiceCall(initializer);

        // Transform to async
        initializer = TransformToAsync(initializer);

        if (string.IsNullOrWhiteSpace(initializer))
        {
            return $"var {variable.Name};";
        }

        // Add await if async
        var awaitPrefix = initializer.Contains("Async") ? "await " : string.Empty;

        return $"var {variable.Name} = {awaitPrefix}{initializer};";
    }

    private string TransformStatement(StatementInfo statement, bool isCommand)
    {
        // Skip ModelState checks - FluentValidation handles this
        if (statement.SourceCode.Contains("ModelState.IsValid"))
        {
            return string.Empty;
        }

        // Skip ViewBag assignments
        if (statement.SourceCode.Contains("ViewBag.") || statement.SourceCode.Contains("ViewData["))
        {
            return "// ViewBag/ViewData removed - use DTO properties instead";
        }

        // Use transformed code if available
        var code = statement.TransformedCode ?? statement.SourceCode;

        // Transform service calls
        code = TransformServiceCall(code);

        // Transform to async
        code = TransformToAsync(code);

        // Add cancellationToken
        if (code.Contains("Async") && !code.Contains("cancellationToken"))
        {
            code = AddCancellationToken(code);
        }

        // Transform field names
        code = TransformDbContextField(code);

        // Add await if async
        if (code.Contains("Async") && !code.TrimStart().StartsWith("await") && !code.Contains("return"))
        {
            // Check if it's an assignment
            var eqIndex = code.IndexOf('=');
            if (eqIndex > 0 && !code.Contains("=="))
            {
                var beforeEq = code.Substring(0, eqIndex + 1);
                var afterEq = code.Substring(eqIndex + 1).Trim();
                code = $"{beforeEq} await {afterEq}";
            }
            else if (!code.Contains("="))
            {
                code = $"await {code}";
            }
        }

        return code;
    }

    private string BuildReturnStatement(ReturnInfo returnInfo, bool isCommand)
    {
        // View returns → return the model wrapped in Result
        if (returnInfo.IsViewReturn)
        {
            var model = returnInfo.ReturnedModel ?? "result";
            return $"return Result<{GetResultType(model)}>.Success({model});";
        }

        // Redirect returns → return success (for commands)
        if (returnInfo.IsRedirect)
        {
            return "return Result.Success();";
        }

        // Error returns → return failure
        if (returnInfo.IsErrorReturn)
        {
            if (returnInfo.Expression.Contains("NotFound"))
            {
                return "return Result.Failure(\"Not found\");";
            }
            if (returnInfo.Expression.Contains("BadRequest"))
            {
                return "return Result.Failure(\"Invalid request\");";
            }
        }

        // Use transformed return if available
        if (!string.IsNullOrWhiteSpace(returnInfo.TransformedReturn))
        {
            return returnInfo.TransformedReturn;
        }

        // Default: return the expression wrapped in Result
        var expr = returnInfo.Expression;
        if (expr.StartsWith("return ", StringComparison.OrdinalIgnoreCase))
        {
            expr = expr.Substring(7).TrimEnd(';').Trim();
        }

        return $"return Result.Success({expr});";
    }

    private string TransformToAsync(string code)
    {
        foreach (var (sync, async) in SyncToAsyncMap)
        {
            // Only replace method calls, not property access or partial matches
            code = System.Text.RegularExpressions.Regex.Replace(
                code,
                $@"\.{sync}\s*\(",
                $".{async}(",
                System.Text.RegularExpressions.RegexOptions.None);
        }

        return code;
    }

    private string TransformServiceCall(string code)
    {
        // Transform service method calls to DbContext operations
        code = TransformServiceMethodToDbContext(code);
        return code;
    }

    /// <summary>
    /// Intelligently transforms service method calls to DbContext queries.
    /// Recognizes common patterns like Find*, Get*, Create*, Add*, Update*, Delete*.
    /// </summary>
    private string TransformServiceMethodToDbContext(string code)
    {
        var transformed = code;
        var hasTransformation = false;

        // Pattern 1: Find{Entity}(id) or Get{Entity}ById(id) → _context.{Entity}s.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        transformed = System.Text.RegularExpressions.Regex.Replace(
            transformed,
            @"\bservice\.(Find|Get)(\w+?)(?:ById)?\((\w+)\)",
            match =>
            {
                hasTransformation = true;
                var entityName = match.Groups[2].Value;
                var idParam = match.Groups[3].Value;
                var dbSetName = PluralizeName(entityName);
                return $"_context.{dbSetName}.FirstOrDefaultAsync(x => x.Id == {idParam}, cancellationToken)";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Pattern 2: GetAll{Entity}s() or Get{Entity}s() or Get{Entity}List() → _context.{Entity}s.ToListAsync(cancellationToken)
        transformed = System.Text.RegularExpressions.Regex.Replace(
            transformed,
            @"\bservice\.(?:GetAll|Get)(\w+?)(?:s|List)?\(\)",
            match =>
            {
                hasTransformation = true;
                var entityName = match.Groups[1].Value;
                var dbSetName = PluralizeName(entityName);
                return $"_context.{dbSetName}.ToListAsync(cancellationToken)";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Pattern 3: Create{Entity}(item) or Add{Entity}(item) → _context.{Entity}s.AddAsync(item, cancellationToken)
        transformed = System.Text.RegularExpressions.Regex.Replace(
            transformed,
            @"\bservice\.(Create|Add)(\w+?)\((\w+)\)",
            match =>
            {
                hasTransformation = true;
                var entityName = match.Groups[2].Value;
                var itemParam = match.Groups[3].Value;
                var dbSetName = PluralizeName(entityName);
                return $"_context.{dbSetName}.AddAsync({itemParam}, cancellationToken)";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Pattern 4: Update{Entity}(item) → _context.{Entity}s.Update(item)
        transformed = System.Text.RegularExpressions.Regex.Replace(
            transformed,
            @"\bservice\.Update(\w+?)\((\w+)\)",
            match =>
            {
                hasTransformation = true;
                var entityName = match.Groups[1].Value;
                var itemParam = match.Groups[2].Value;
                var dbSetName = PluralizeName(entityName);
                return $"_context.{dbSetName}.Update({itemParam})";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Pattern 5: Delete{Entity}(item) or Remove{Entity}(item) → _context.{Entity}s.Remove(item)
        transformed = System.Text.RegularExpressions.Regex.Replace(
            transformed,
            @"\bservice\.(Delete|Remove)(\w+?)\((\w+)\)",
            match =>
            {
                hasTransformation = true;
                var entityName = match.Groups[2].Value;
                var itemParam = match.Groups[3].Value;
                var dbSetName = PluralizeName(entityName);
                return $"_context.{dbSetName}.Remove({itemParam})";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Fallback: If still has "service." after all patterns, replace with _context. and add TODO
        if (transformed.Contains("service.", StringComparison.OrdinalIgnoreCase) && !hasTransformation)
        {
            transformed = System.Text.RegularExpressions.Regex.Replace(
                transformed,
                @"\bservice\.",
                "_context. /* TODO: Verify this service call transformation */",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return transformed;
    }

    /// <summary>
    /// Simple pluralization for entity names.
    /// Converts entity name to DbSet name (e.g., CatalogItem → CatalogItems).
    /// </summary>
    private static string PluralizeName(string entityName)
    {
        if (string.IsNullOrEmpty(entityName))
        {
            return entityName;
        }

        // Check for special endings BEFORE checking for 's'
        // Ends with 'ch', 'sh', 'x', 'z', 'ss' → add 'es'
        if (entityName.EndsWith("ss", StringComparison.OrdinalIgnoreCase) ||
            entityName.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            entityName.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
            entityName.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            entityName.EndsWith("z", StringComparison.OrdinalIgnoreCase))
        {
            return entityName + "es";
        }

        // Already plural (ends with 's')
        if (entityName.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            return entityName;
        }

        // Ends with 'y' (not preceded by vowel) → 'ies'
        if (entityName.Length > 1 &&
            entityName.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            !"aeiou".Contains(entityName[^2], StringComparison.OrdinalIgnoreCase))
        {
            return entityName[..^1] + "ies";
        }

        // Default: add 's'
        return entityName + "s";
    }

    private string TransformDbContextField(string code)
    {
        // Transform common DbContext field patterns
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"\bdb\.",
            "_context.",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"\b_db\.",
            "_context.",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"\bcontext\.",
            "_context.",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Transform storeDB field (common in MVC Music Store and similar projects)
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"\bstoreDB\.",
            "_context.",
            System.Text.RegularExpressions.RegexOptions.None);

        // Transform MVC-specific constructs
        code = TransformMvcConstructs(code);

        return code;
    }

    /// <summary>
    /// Transforms MVC-specific constructs to ASP.NET Core equivalents.
    /// </summary>
    private string TransformMvcConstructs(string code)
    {
        // Server.HtmlEncode → System.Net.WebUtility.HtmlEncode
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"\bServer\.HtmlEncode\(",
            "System.Net.WebUtility.HtmlEncode(",
            System.Text.RegularExpressions.RegexOptions.None);

        // this.HttpContext → HttpContext (via IHttpContextAccessor - add TODO)
        if (code.Contains("this.HttpContext") || code.Contains("HttpContext"))
        {
            // Replace direct HttpContext access with a TODO comment
            code = System.Text.RegularExpressions.Regex.Replace(
                code,
                @"\bthis\.HttpContext\b",
                "_httpContextAccessor.HttpContext",
                System.Text.RegularExpressions.RegexOptions.None);
        }

        // Session[ → _httpContextAccessor.HttpContext.Session.GetString( with TODO
        code = System.Text.RegularExpressions.Regex.Replace(
            code,
            @"\bSession\[",
            "// TODO: Use IDistributedCache or session service instead\n        _httpContextAccessor.HttpContext?.Session.GetString(",
            System.Text.RegularExpressions.RegexOptions.None);

        return code;
    }

    private string AddCancellationToken(string code)
    {
        // Add cancellationToken to async calls that don't have it
        // Pattern: ...Async() or ...Async(args) → ...Async(cancellationToken) or ...Async(args, cancellationToken)

        return System.Text.RegularExpressions.Regex.Replace(
            code,
            @"Async\(\s*\)",
            "Async(cancellationToken)",
            System.Text.RegularExpressions.RegexOptions.None);
    }

    private bool ShouldSkipStatement(StatementInfo statement)
    {
        var code = statement.SourceCode;

        // DON'T skip IF statements with ModelState.IsValid - we handle them specially in TransformStatementPreservingStructure
        // (unwrap the if and keep the child statements)
        if (statement.Type == StatementType.If && code.Contains("ModelState.IsValid"))
        {
            return false;
        }

        // DON'T skip TRY statements with ModelState.IsValid - we handle them specially in TransformStatementPreservingStructure
        // (unwrap the ModelState.IsValid check but preserve the try-catch)
        if (statement.Type == StatementType.Try && code.Contains("ModelState.IsValid"))
        {
            return false;
        }

        // Skip standalone ModelState validation (shouldn't happen after above checks, but safety)
        if (code.Contains("ModelState.IsValid"))
        {
            return true;
        }

        // Skip logging (can be added via pipeline behavior)
        if (code.Contains("_log.") || code.Contains("_logger."))
        {
            return true;
        }

        return false;
    }

    private bool IsDbOperationVariable(VariableInfo variable, ExtractedLogic logic)
    {
        // Check if this variable is initialized from a DB operation
        return logic.DbOperations.Any(op =>
            op.SourceCode.Contains(variable.Name) ||
            (variable.Initializer?.Contains("_context.") ?? false) ||
            (variable.Initializer?.Contains("db.") ?? false));
    }

    private string GetResultType(string model)
    {
        // Try to infer the result type from the model
        if (model.EndsWith("s") && !model.EndsWith("ss"))
        {
            return $"IReadOnlyList<{model.TrimEnd('s')}Dto>";
        }

        return $"{model}Dto";
    }

    private string ExtractEntityName(string controllerName)
    {
        // CatalogController → Catalog
        if (controllerName.EndsWith("Controller"))
        {
            return controllerName[..^10];
        }

        return controllerName;
    }

    /// <summary>
    /// Extracts content inside parentheses, handling nested parentheses.
    /// </summary>
    /// <param name="expression">The full expression (e.g., "View(albums.ToList())")</param>
    /// <param name="startIndex">Index after the opening parenthesis</param>
    /// <returns>The content between matching parentheses</returns>
    private static string ExtractParenthesesContent(string expression, int startIndex)
    {
        if (startIndex >= expression.Length)
            return string.Empty;

        var depth = 1;
        var endIndex = startIndex;

        while (endIndex < expression.Length && depth > 0)
        {
            var c = expression[endIndex];
            if (c == '(')
                depth++;
            else if (c == ')')
                depth--;

            if (depth > 0)
                endIndex++;
        }

        if (depth != 0)
            return string.Empty;

        return expression.Substring(startIndex, endIndex - startIndex).Trim();
    }

    /// <summary>
    /// Finds the index of the first comma at the top level (not inside parentheses).
    /// </summary>
    private static int FindTopLevelComma(string text)
    {
        var depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(')
                depth++;
            else if (c == ')')
                depth--;
            else if (c == ',' && depth == 0)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Handles private method calls in the generated business logic.
    /// Private methods are NOT inlined - they are kept as method calls and the methods
    /// will be added to the handler class separately. This avoids issues with controller-specific
    /// patterns and makes the generated code cleaner.
    /// </summary>
    private string InlinePrivateMethodCalls(string code, IReadOnlyList<PrivateMethodInfo> privateMethods)
    {
        // Don't inline private methods - they will be added to the handler as private methods
        // Just return the code as-is with the method calls intact
        return code;
    }

    /// <summary>
    /// Determines if a method call at the given position is in an expression context
    /// (where it must evaluate to a value, not a statement).
    /// </summary>
    private static bool IsInExpressionContext(string code, int position)
    {
        if (position <= 0 || position >= code.Length)
        {
            return false;
        }

        // Find the start of the current line
        var lineStart = code.LastIndexOf('\n', position - 1) + 1;
        var beforeCallOnLine = code.Substring(lineStart, position - lineStart).Trim();

        // Check if the method call is:
        // 1. Inside a method argument: someMethod(theCall)
        // 2. On the right side of an assignment: var x = theCall
        // 3. Inside another expression: return theCall, theCall + something

        // If the line starts with "var x = " or "type x = ", it's an assignment RHS
        if (System.Text.RegularExpressions.Regex.IsMatch(beforeCallOnLine, @"(var|const|\w+)\s+\w+\s*=\s*$"))
        {
            return true;
        }

        // If there's an unclosed parenthesis, we're inside a method argument
        var openParens = beforeCallOnLine.Count(c => c == '(');
        var closeParens = beforeCallOnLine.Count(c => c == ')');
        if (openParens > closeParens)
        {
            return true;
        }

        // If line contains "return " before the call
        if (beforeCallOnLine.Contains("return ", StringComparison.Ordinal))
        {
            return true;
        }

        // If line ends with operators
        if (beforeCallOnLine.EndsWith("+") || beforeCallOnLine.EndsWith("-") ||
            beforeCallOnLine.EndsWith("*") || beforeCallOnLine.EndsWith("/") ||
            beforeCallOnLine.EndsWith("&&") || beforeCallOnLine.EndsWith("||") ||
            beforeCallOnLine.EndsWith(",") || beforeCallOnLine.EndsWith("("))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if the code body is a statement block (cannot be used as expression)
    /// vs a pure expression (can be used as a value).
    /// </summary>
    private static bool IsStatementBlock(string body)
    {
        var trimmed = body.Trim();

        // Check for common statement indicators
        // Variable declarations
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\b(var|const|int|string|bool|double|float|decimal|long|short|byte|object|dynamic)\s+\w+\s*="))
        {
            return true;
        }

        // Multiple statements (semicolons not at the end)
        var semicolonCount = trimmed.Count(c => c == ';');
        if (semicolonCount > 1)
        {
            return true;
        }

        // Statement keywords at start of lines
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"(^|\n)\s*(if|for|foreach|while|do|switch|try|using|lock|return|throw|break|continue)\s*[\(\{]?"))
        {
            return true;
        }

        // Contains return statement (not just the value)
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\breturn\s+"))
        {
            return true;
        }

        // Assignment statements (not just expressions)
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\s*\w+(\.\w+)*\s*=\s*[^=]") && !trimmed.Contains("=>"))
        {
            return true;
        }

        // Method calls that stand alone as statements (end with ;)
        if (trimmed.EndsWith(";") && !trimmed.Contains("=>"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts the method body without signature.
    /// Handles both block bodies { } and expression bodies =>.
    /// </summary>
    private static string ExtractMethodBodyWithoutSignature(string fullMethodSource)
    {
        // Check for expression body (=> before any {)
        var arrowIndex = fullMethodSource.IndexOf("=>", StringComparison.Ordinal);
        var firstBraceIndex = fullMethodSource.IndexOf('{');

        if (arrowIndex >= 0 && (firstBraceIndex < 0 || arrowIndex < firstBraceIndex))
        {
            // Expression-bodied method  (e.g., "=> x + 1;")
            var expressionStart = arrowIndex + 2;
            var semicolonIndex = fullMethodSource.LastIndexOf(';');
            if (semicolonIndex > expressionStart)
            {
                return fullMethodSource.Substring(expressionStart, semicolonIndex - expressionStart).Trim();
            }
            return fullMethodSource.Substring(expressionStart).Trim().TrimEnd(';');
        }

        // Block body - find opening and closing braces at the METHOD level
        var openBraceIndex = fullMethodSource.IndexOf('{');
        if (openBraceIndex < 0)
        {
            return fullMethodSource; // No body found, return as-is
        }

        // Find matching closing brace
        var depth = 0;
        var closeBraceIndex = openBraceIndex;
        for (int i = openBraceIndex; i < fullMethodSource.Length; i++)
        {
            if (fullMethodSource[i] == '{')
                depth++;
            else if (fullMethodSource[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    closeBraceIndex = i;
                    break;
                }
            }
        }

        if (closeBraceIndex > openBraceIndex)
        {
            // Extract content between the METHOD braces (this includes the full body)
            var bodyContent = fullMethodSource.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1).Trim();

            // If body is ONLY a return statement, extract what's being returned
            // But preserve the entire expression including nested braces (like switch expressions)
            if (bodyContent.StartsWith("return ", StringComparison.Ordinal))
            {
                // Find the semicolon that ends the return statement
                // We need to be careful about nested braces in switch expressions
                var returnStart = 7; // length of "return "
                var returnContent = bodyContent.Substring(returnStart);

                // Find the last semicolon (end of return statement)
                var lastSemicolon = returnContent.LastIndexOf(';');
                if (lastSemicolon >= 0)
                {
                    returnContent = returnContent.Substring(0, lastSemicolon);
                }

                return returnContent.Trim();
            }

            return bodyContent;
        }

        return fullMethodSource;
    }

    /// <summary>
    /// Substitutes method parameters with actual call arguments in the method body.
    /// </summary>
    private static string SubstituteParameters(string body, string callArguments, IReadOnlyList<Core.Models.Modernization.ActionParameter> methodParams)
    {
        if (methodParams.Count == 0)
        {
            return body;
        }

        // Parse call arguments - split by comma at top level
        var arguments = ParseArguments(callArguments);

        // Ensure we have matching counts
        if (arguments.Count != methodParams.Count)
        {
            // Mismatch - return body with TODO
            return $"/* TODO: Parameter count mismatch */ {body}";
        }

        var result = body;

        // Replace each parameter with its corresponding argument
        for (int i = 0; i < methodParams.Count; i++)
        {
            var paramName = methodParams[i].Name;
            var argumentValue = arguments[i];

            // Use word boundary to avoid partial replacements
            var pattern = $@"\b{System.Text.RegularExpressions.Regex.Escape(paramName)}\b";
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, argumentValue);
        }

        return result;
    }

    /// <summary>
    /// Parses comma-separated arguments, respecting parentheses nesting.
    /// </summary>
    private static List<string> ParseArguments(string arguments)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return result;
        }

        var current = new StringBuilder();
        var depth = 0;

        foreach (var ch in arguments)
        {
            if (ch == ',' && depth == 0)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                if (ch == '(' || ch == '[' || ch == '{')
                    depth++;
                else if (ch == ')' || ch == ']' || ch == '}')
                    depth--;

                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }

        return result;
    }
}
