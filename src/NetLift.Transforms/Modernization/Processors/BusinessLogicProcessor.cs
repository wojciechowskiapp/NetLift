using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Transforms.Modernization.Processors;

/// <summary>
/// Processes extracted business logic to fix common issues and detect dependencies.
/// </summary>
public static partial class BusinessLogicProcessor
{
    /// <summary>
    /// Known service dependencies that may be referenced in business logic.
    /// </summary>
    private static readonly Dictionary<string, ServiceDependency> KnownDependencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["_httpContextAccessor"] = new("_httpContextAccessor", "IHttpContextAccessor", "httpContextAccessor", "Microsoft.AspNetCore.Http"),
        ["_userManager"] = new("_userManager", "UserManager<ApplicationUser>", "userManager", "Microsoft.AspNetCore.Identity"),
        ["_signInManager"] = new("_signInManager", "SignInManager<ApplicationUser>", "signInManager", "Microsoft.AspNetCore.Identity"),
        ["_memoryCache"] = new("_memoryCache", "IMemoryCache", "memoryCache", "Microsoft.Extensions.Caching.Memory"),
        ["_cache"] = new("_cache", "IMemoryCache", "cache", "Microsoft.Extensions.Caching.Memory"),
        ["_distributedCache"] = new("_distributedCache", "IDistributedCache", "distributedCache", "Microsoft.Extensions.Caching.Distributed"),
        ["_emailSender"] = new("_emailSender", "IEmailSender", "emailSender", null),
        ["_urlHelper"] = new("_urlHelper", "IUrlHelper", "urlHelper", "Microsoft.AspNetCore.Mvc"),
    };

    /// <summary>
    /// Processes business logic to fix common issues.
    /// </summary>
    /// <param name="businessLogic">The extracted business logic code</param>
    /// <param name="isAsync">Whether the handler method is async</param>
    /// <returns>Processed business logic with detected dependencies</returns>
    public static ProcessedBusinessLogic Process(string? businessLogic, bool isAsync)
    {
        if (string.IsNullOrWhiteSpace(businessLogic))
        {
            return new ProcessedBusinessLogic
            {
                Code = string.Empty,
                RequiredDependencies = [],
                RequiredUsings = []
            };
        }

        var code = businessLogic;

        // Step 1: Ensure all *Async method calls have await (if handler is async)
        if (isAsync)
        {
            code = EnsureAsyncAwait(code);
        }

        // Step 2: Transform ViewData/ViewBag to result.X
        code = TransformViewDataToResult(code);

        // Step 3: Transform HttpContext.User to use IHttpContextAccessor
        code = TransformHttpContextAccess(code);

        // Step 4: Detect required service dependencies
        var dependencies = DetectServiceDependencies(code);

        // Step 5: Detect required usings from type references
        var usings = DetectRequiredUsings(code, dependencies);

        return new ProcessedBusinessLogic
        {
            Code = code,
            RequiredDependencies = dependencies,
            RequiredUsings = usings
        };
    }

    /// <summary>
    /// Ensures all *Async method calls have await prefix using Roslyn.
    /// </summary>
    private static string EnsureAsyncAwait(string code)
    {
        // Wrap code in a method to make it parseable
        var wrappedCode = $@"
class TempWrapper
{{
    async Task MethodWrapper()
    {{
        {code}
    }}
}}";

        var tree = CSharpSyntaxTree.ParseText(wrappedCode);
        var root = tree.GetRoot();

        // Apply the rewriter
        var rewriter = new AsyncAwaitRewriter();
        var newRoot = rewriter.Visit(root);

        // Extract the transformed code back from the method body
        var method = newRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (method?.Body == null)
        {
            return code; // Fallback if parsing failed
        }

        // Get the statements from the method body and convert back to string
        var statements = method.Body.Statements;
        var transformedCode = string.Join("\n", statements.Select(s => s.ToFullString().TrimEnd()));

        return transformedCode;
    }

    /// <summary>
    /// Roslyn rewriter that adds await to async method calls.
    /// </summary>
    private sealed class AsyncAwaitRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // First, visit children to handle nested invocations
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

            // Check if this invocation is for a method ending with "Async"
            if (!IsAsyncMethodCall(visited))
            {
                return visited;
            }

            // Check if already awaited
            if (IsAlreadyAwaited(visited))
            {
                return visited;
            }

            // Check if this is in a context where we can add await
            if (!CanAddAwait(visited))
            {
                return visited;
            }

            // Wrap in await expression
            var awaitExpression = SyntaxFactory.AwaitExpression(visited)
                .WithAwaitKeyword(SyntaxFactory.Token(SyntaxKind.AwaitKeyword)
                    .WithTrailingTrivia(SyntaxFactory.Space));

            return awaitExpression;
        }

        /// <summary>
        /// Checks if the invocation is for a method ending with "Async".
        /// </summary>
        private static bool IsAsyncMethodCall(InvocationExpressionSyntax invocation)
        {
            var methodName = invocation.Expression switch
            {
                IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                _ => null
            };

            return methodName?.EndsWith("Async", StringComparison.Ordinal) == true;
        }

        /// <summary>
        /// Checks if the invocation is already wrapped in an await expression.
        /// </summary>
        private static bool IsAlreadyAwaited(InvocationExpressionSyntax invocation)
        {
            var parent = invocation.Parent;
            return parent is AwaitExpressionSyntax;
        }

        /// <summary>
        /// Checks if we can add await in this context.
        /// This is true for most contexts except when the invocation is used in certain places.
        /// </summary>
        private static bool CanAddAwait(InvocationExpressionSyntax invocation)
        {
            var parent = invocation.Parent;

            // Can add await in most expression contexts
            // Cannot add await if parent is:
            // - Already an await expression (handled by IsAlreadyAwaited)
            // - Part of a method declaration (but this shouldn't happen in our wrapped code)

            return parent switch
            {
                // These contexts support await
                ExpressionStatementSyntax => true,
                EqualsValueClauseSyntax => true,
                ReturnStatementSyntax => true,
                ArgumentSyntax => true,
                AssignmentExpressionSyntax => true,
                BinaryExpressionSyntax => true,
                ConditionalExpressionSyntax => true,
                ParenthesizedExpressionSyntax => true,
                IfStatementSyntax => true,
                WhileStatementSyntax => true,
                ForStatementSyntax => true,
                LocalDeclarationStatementSyntax => true,
                MemberAccessExpressionSyntax => true,
                InvocationExpressionSyntax => true,
                ArrowExpressionClauseSyntax => true,
                SimpleLambdaExpressionSyntax => true,
                ParenthesizedLambdaExpressionSyntax => true,
                _ => true // Default to true, be permissive
            };
        }
    }

    /// <summary>
    /// Transforms ViewData["Key"] and ViewBag.Property to result.Key and result.Property.
    /// </summary>
    private static string TransformViewDataToResult(string code)
    {
        // Transform ViewData["Key"] = value -> result.Key = value
        code = ViewDataAssignmentRegex().Replace(code, match =>
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            return $"result.{key} = {value}";
        });

        // Transform ViewBag.Property = value -> result.Property = value
        code = ViewBagAssignmentRegex().Replace(code, match =>
        {
            var property = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            return $"result.{property} = {value}";
        });

        // Transform ViewData["Key"] reads -> result.Key
        code = ViewDataReadRegex().Replace(code, "result.$1");

        // Transform ViewBag.Property reads -> result.Property
        code = ViewBagReadRegex().Replace(code, "result.$1");

        return code;
    }

    /// <summary>
    /// Transforms direct HttpContext access to use IHttpContextAccessor.
    /// </summary>
    private static string TransformHttpContextAccess(string code)
    {
        // Transform HttpContext.User -> _httpContextAccessor.HttpContext?.User
        code = code.Replace("HttpContext.User", "_httpContextAccessor.HttpContext?.User");

        // Transform User.Identity -> _httpContextAccessor.HttpContext?.User?.Identity
        // Be careful not to match _userManager.User or similar
        code = UserIdentityRegex().Replace(code, "_httpContextAccessor.HttpContext?.User?$1");

        return code;
    }

    /// <summary>
    /// Detects service dependencies referenced in the code.
    /// </summary>
    private static HashSet<ServiceDependency> DetectServiceDependencies(string code)
    {
        var dependencies = new HashSet<ServiceDependency>();

        foreach (var (fieldName, dependency) in KnownDependencies)
        {
            if (code.Contains(fieldName, StringComparison.Ordinal))
            {
                dependencies.Add(dependency);
            }
        }

        // Also check for HttpContext access patterns that need IHttpContextAccessor
        if (code.Contains("_httpContextAccessor", StringComparison.Ordinal) ||
            code.Contains("HttpContext.User", StringComparison.Ordinal) ||
            UserIdentityRegex().IsMatch(code))
        {
            dependencies.Add(KnownDependencies["_httpContextAccessor"]);
        }

        return dependencies;
    }

    /// <summary>
    /// Detects required using statements from type references in the code.
    /// </summary>
    private static HashSet<string> DetectRequiredUsings(string code, HashSet<ServiceDependency> dependencies)
    {
        var usings = new HashSet<string>();

        // Add usings for detected dependencies
        foreach (var dep in dependencies)
        {
            if (!string.IsNullOrEmpty(dep.RequiredUsing))
            {
                usings.Add(dep.RequiredUsing);
            }
        }

        // Detect common types that need usings
        if (code.Contains("SelectList", StringComparison.Ordinal) ||
            code.Contains("SelectListItem", StringComparison.Ordinal))
        {
            usings.Add("Microsoft.AspNetCore.Mvc.Rendering");
        }

        if (code.Contains("JsonResult", StringComparison.Ordinal) ||
            code.Contains("RedirectToAction", StringComparison.Ordinal))
        {
            usings.Add("Microsoft.AspNetCore.Mvc");
        }

        if (code.Contains("ClaimTypes", StringComparison.Ordinal))
        {
            usings.Add("System.Security.Claims");
        }

        return usings;
    }

    // Regex patterns using source generators for better performance

    /// <summary>
    /// Matches ViewData["Key"] = value assignments.
    /// </summary>
    [GeneratedRegex(@"ViewData\[""(\w+)""\]\s*=\s*(.+?)(?=;|$)", RegexOptions.Compiled)]
    private static partial Regex ViewDataAssignmentRegex();

    /// <summary>
    /// Matches ViewBag.Property = value assignments.
    /// </summary>
    [GeneratedRegex(@"ViewBag\.(\w+)\s*=\s*(.+?)(?=;|$)", RegexOptions.Compiled)]
    private static partial Regex ViewBagAssignmentRegex();

    /// <summary>
    /// Matches ViewData["Key"] reads (not assignments).
    /// </summary>
    [GeneratedRegex(@"ViewData\[""(\w+)""\](?!\s*=)", RegexOptions.Compiled)]
    private static partial Regex ViewDataReadRegex();

    /// <summary>
    /// Matches ViewBag.Property reads (not assignments).
    /// </summary>
    [GeneratedRegex(@"ViewBag\.(\w+)(?!\s*=)", RegexOptions.Compiled)]
    private static partial Regex ViewBagReadRegex();

    /// <summary>
    /// Matches User.Identity access that's not part of a larger identifier.
    /// </summary>
    [GeneratedRegex(@"(?<![.\w_])User(\.Identity\b)", RegexOptions.Compiled)]
    private static partial Regex UserIdentityRegex();
}

/// <summary>
/// Result of processing business logic.
/// </summary>
public sealed record ProcessedBusinessLogic
{
    /// <summary>
    /// The processed code with fixes applied.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Service dependencies detected in the code.
    /// </summary>
    public required HashSet<ServiceDependency> RequiredDependencies { get; init; }

    /// <summary>
    /// Using statements required by the code.
    /// </summary>
    public required HashSet<string> RequiredUsings { get; init; }
}

/// <summary>
/// Represents a service dependency that needs to be injected.
/// </summary>
public sealed record ServiceDependency(
    string FieldName,
    string InterfaceType,
    string ConstructorParamName,
    string? RequiredUsing);
