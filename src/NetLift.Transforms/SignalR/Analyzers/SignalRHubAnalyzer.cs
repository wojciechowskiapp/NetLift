using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.SignalR;
using NetLift.Core.Models.SignalR;
using ParameterInfo = NetLift.Core.Models.Modernization.ParameterInfo;

namespace NetLift.Transforms.SignalR.Analyzers;

/// <summary>
/// Roslyn-based analyzer for SignalR Hub classes.
/// </summary>
public class SignalRHubAnalyzer : ISignalRHubAnalyzer
{
    private static readonly HashSet<string> HubBaseTypes =
    [
        "Hub",
        "Hub<T>",
        "Microsoft.AspNet.SignalR.Hub",
        "Microsoft.AspNet.SignalR.Hub<T>"
    ];

    private static readonly HashSet<string> LifecycleMethods =
    [
        "OnConnected",
        "OnDisconnected",
        "OnReconnected"
    ];

    /// <inheritdoc />
    public IReadOnlyList<SignalRHubInfo> AnalyzeFile(string sourceCode, string filePath)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return [];
        }

        var hubs = new List<SignalRHubInfo>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            var classDeclarations = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(IsHubClass)
                .ToList();

            foreach (var classDecl in classDeclarations)
            {
                var hubInfo = AnalyzeHubClass(classDecl, filePath, root);
                hubs.Add(hubInfo);
            }
        }
        catch (ArgumentException)
        {
            // Invalid syntax - return empty
        }

        return hubs;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SignalRHubInfo>> AnalyzeProjectAsync(string projectPath)
    {
        var hubs = new List<SignalRHubInfo>();
        var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"));

        foreach (var file in csFiles)
        {
            try
            {
                var sourceCode = await File.ReadAllTextAsync(file);
                if (ContainsSignalRHub(sourceCode))
                {
                    var fileHubs = AnalyzeFile(sourceCode, file);
                    hubs.AddRange(fileHubs);
                }
            }
            catch (IOException)
            {
                // Skip files that can't be read
            }
        }

        return hubs;
    }

    /// <inheritdoc />
    public bool ContainsSignalRHub(string sourceCode)
    {
        return sourceCode.Contains(": Hub") ||
               sourceCode.Contains("Microsoft.AspNet.SignalR") ||
               sourceCode.Contains("[HubName(");
    }

    private static bool IsHubClass(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList == null)
        {
            return false;
        }

        return classDecl.BaseList.Types.Any(t =>
        {
            var typeName = t.Type.ToString();
            return HubBaseTypes.Any(baseType =>
                typeName == baseType ||
                typeName.StartsWith("Hub<") ||
                typeName.EndsWith(".Hub") ||
                typeName.EndsWith(".Hub<"));
        });
    }

    private SignalRHubInfo AnalyzeHubClass(ClassDeclarationSyntax classDecl, string filePath, SyntaxNode root)
    {
        var className = classDecl.Identifier.Text;
        var ns = GetNamespace(classDecl);

        var lifecycleMethods = AnalyzeLifecycleMethods(classDecl);
        var clientInvocations = AnalyzeClientInvocations(classDecl);
        var hubMethods = AnalyzeHubMethods(classDecl);
        var groupsOperations = AnalyzeGroupsOperations(classDecl);
        var hubRoute = GetHubRoute(classDecl);
        var hasAuth = HasCustomAuthorization(classDecl);

        var confidence = CalculateConfidence(lifecycleMethods, clientInvocations, groupsOperations);

        return new SignalRHubInfo
        {
            ClassName = className,
            Namespace = ns,
            FilePath = filePath,
            LifecycleMethods = lifecycleMethods,
            ClientInvocations = clientInvocations,
            HubMethods = hubMethods,
            GroupsOperations = groupsOperations,
            HubRoute = hubRoute,
            HasCustomAuthorization = hasAuth,
            Confidence = confidence
        };
    }

    private static string GetNamespace(ClassDeclarationSyntax classDecl)
    {
        var namespaceDecl = classDecl.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return namespaceDecl?.Name.ToString() ?? string.Empty;
    }

    private static List<HubLifecycleInfo> AnalyzeLifecycleMethods(ClassDeclarationSyntax classDecl)
    {
        var methods = new List<HubLifecycleInfo>();

        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            var methodName = method.Identifier.Text;
            if (!LifecycleMethods.Contains(methodName))
            {
                continue;
            }

            var lineNumber = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var canAutoTransform = true;
            string? transformNote = null;

            if (methodName == "OnReconnected")
            {
                canAutoTransform = false;
                transformNote = "OnReconnected() does not exist in ASP.NET Core SignalR. Method will be removed with TODO comment.";
            }
            else if (methodName == "OnDisconnected")
            {
                transformNote = "Parameter changes from 'bool stopCalled' to 'Exception exception'.";
            }

            methods.Add(new HubLifecycleInfo
            {
                MethodName = methodName,
                LineNumber = lineNumber,
                CanAutoTransform = canAutoTransform,
                TransformationNote = transformNote
            });
        }

        return methods;
    }

    private static List<ClientInvocationInfo> AnalyzeClientInvocations(ClassDeclarationSyntax classDecl)
    {
        var invocations = new List<ClientInvocationInfo>();

        var memberAccesses = classDecl.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(m => m.Expression.ToString().StartsWith("Clients."));

        foreach (var access in memberAccesses)
        {
            // Look for the invocation: Clients.All.methodName(args)
            if (access.Parent is InvocationExpressionSyntax invocation)
            {
                var pattern = access.Expression.ToString(); // e.g., "Clients.All"
                var methodName = access.Name.ToString();

                // Skip if this is a Clients.Client(), Clients.Group() call
                if (methodName is "Client" or "Group" or "AllExcept" or "Groups")
                {
                    // Find the next level invocation
                    if (invocation.Parent is MemberAccessExpressionSyntax outerAccess &&
                        outerAccess.Parent is InvocationExpressionSyntax outerInvocation)
                    {
                        pattern = invocation.ToString();
                        methodName = outerAccess.Name.ToString();

                        var lineNum = outerInvocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        var originalCode = outerInvocation.ToString();
                        var transformedCode = GenerateTransformedClientInvocation(pattern, methodName, outerInvocation);

                        invocations.Add(new ClientInvocationInfo
                        {
                            Pattern = pattern,
                            MethodName = methodName,
                            LineNumber = lineNum,
                            OriginalCode = originalCode,
                            TransformedCode = transformedCode
                        });
                    }
                    continue;
                }

                var lineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var original = invocation.ToString();
                var transformed = GenerateTransformedClientInvocation(pattern, methodName, invocation);

                invocations.Add(new ClientInvocationInfo
                {
                    Pattern = pattern,
                    MethodName = methodName,
                    LineNumber = lineNumber,
                    OriginalCode = original,
                    TransformedCode = transformed
                });
            }
        }

        return invocations;
    }

    private static string GenerateTransformedClientInvocation(string pattern, string methodName, InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList.Arguments;
        var argsString = args.Any() ? $", {string.Join(", ", args)}" : "";

        // Extract the Clients.X part from the pattern
        var clientsPattern = pattern.Contains("(")
            ? pattern.Substring(0, pattern.IndexOf('(') + 1) + pattern.Substring(pattern.LastIndexOf(')'))
            : pattern;

        return $"await {clientsPattern}.SendAsync(\"{methodName}\"{argsString})";
    }

    private static List<HubMethodInfo> AnalyzeHubMethods(ClassDeclarationSyntax classDecl)
    {
        var methods = new List<HubMethodInfo>();

        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            var methodName = method.Identifier.Text;

            // Skip lifecycle methods and private methods
            if (LifecycleMethods.Contains(methodName))
            {
                continue;
            }

            var isPublic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
            if (!isPublic)
            {
                continue;
            }

            var returnType = method.ReturnType.ToString();
            var isAsync = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)) ||
                         returnType.StartsWith("Task");
            var lineNumber = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            var parameters = method.ParameterList.Parameters
                .Select(p => new ParameterInfo
                {
                    Name = p.Identifier.Text,
                    Type = p.Type?.ToString() ?? "object"
                })
                .ToList();

            methods.Add(new HubMethodInfo
            {
                Name = methodName,
                ReturnType = returnType,
                IsAsync = isAsync,
                LineNumber = lineNumber,
                Parameters = parameters
            });
        }

        return methods;
    }

    private static List<GroupsOperationInfo> AnalyzeGroupsOperations(ClassDeclarationSyntax classDecl)
    {
        var operations = new List<GroupsOperationInfo>();

        var groupsAccesses = classDecl.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => i.ToString().Contains("Groups.Add") || i.ToString().Contains("Groups.Remove"));

        foreach (var invocation in groupsAccesses)
        {
            var code = invocation.ToString();
            var lineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var operationType = code.Contains("Groups.Add") ? GroupsOperationType.Add : GroupsOperationType.Remove;

            operations.Add(new GroupsOperationInfo
            {
                OperationType = operationType,
                LineNumber = lineNumber,
                OriginalCode = code
            });
        }

        return operations;
    }

    private static string? GetHubRoute(ClassDeclarationSyntax classDecl)
    {
        var hubNameAttr = classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "HubName" or "HubNameAttribute");

        if (hubNameAttr?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal)
        {
            return literal.Token.ValueText;
        }

        // Default route is class name without "Hub" suffix, lowercased
        var className = classDecl.Identifier.Text;
        var route = className.EndsWith("Hub", StringComparison.OrdinalIgnoreCase)
            ? className.Substring(0, className.Length - 3)
            : className;

        return $"/{route.ToLowerInvariant()}Hub";
    }

    private static bool HasCustomAuthorization(ClassDeclarationSyntax classDecl)
    {
        var hasAuthorize = classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Contains("Authorize"));

        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
        var methodsHaveAuthorize = methods.Any(m =>
            m.AttributeLists.SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().Contains("Authorize")));

        return hasAuthorize || methodsHaveAuthorize;
    }

    private static int CalculateConfidence(
        List<HubLifecycleInfo> lifecycleMethods,
        List<ClientInvocationInfo> clientInvocations,
        List<GroupsOperationInfo> groupsOperations)
    {
        var confidence = 95; // Start high

        // OnReconnected reduces confidence
        if (lifecycleMethods.Any(m => m.MethodName == "OnReconnected"))
        {
            confidence -= 15;
        }

        // Complex client invocation patterns reduce confidence slightly
        if (clientInvocations.Any(c => c.Pattern.Contains("AllExcept") || c.Pattern.Contains("Groups")))
        {
            confidence -= 5;
        }

        // Groups operations are well-supported
        if (groupsOperations.Count > 5)
        {
            confidence -= 2;
        }

        return Math.Max(confidence, 60);
    }
}
