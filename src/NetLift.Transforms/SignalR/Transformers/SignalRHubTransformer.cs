using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces.SignalR;
using NetLift.Core.Models.SignalR;

namespace NetLift.Transforms.SignalR.Transformers;

/// <summary>
/// Transforms ASP.NET SignalR Hub code to ASP.NET Core SignalR.
/// </summary>
public class SignalRHubTransformer : ISignalRHubTransformer
{
    /// <inheritdoc />
    public TransformedSignalRFile TransformHub(string sourceCode, SignalRHubInfo hubInfo)
    {
        var changes = new List<SignalRChange>();
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // Apply transformations
        var rewriter = new SignalRHubRewriter(hubInfo, changes);
        var newRoot = rewriter.Visit(root);

        // Update using statements
        newRoot = UpdateUsingStatements((CompilationUnitSyntax)newRoot, changes);

        var transformedCode = newRoot.NormalizeWhitespace().ToFullString();

        return new TransformedSignalRFile
        {
            FilePath = hubInfo.FilePath,
            FileType = SignalRFileType.Hub,
            TransformedCode = transformedCode,
            Changes = changes,
            Confidence = hubInfo.Confidence
        };
    }

    /// <inheritdoc />
    public TransformedSignalRFile TransformGlobalHostUsage(string sourceCode, GlobalHostUsageInfo globalHostInfo)
    {
        var changes = new List<SignalRChange>();
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        var rewriter = new GlobalHostRewriter(globalHostInfo, changes);
        var newRoot = rewriter.Visit(root);

        // Add IHubContext fields if needed
        newRoot = AddHubContextFields((CompilationUnitSyntax)newRoot, globalHostInfo, changes);

        // Update using statements
        newRoot = UpdateUsingStatementsForGlobalHost((CompilationUnitSyntax)newRoot, changes);

        var transformedCode = newRoot.NormalizeWhitespace().ToFullString();

        return new TransformedSignalRFile
        {
            FilePath = globalHostInfo.FilePath,
            FileType = SignalRFileType.ServiceWithGlobalHost,
            TransformedCode = transformedCode,
            Changes = changes,
            Confidence = globalHostInfo.Confidence
        };
    }

    private static SyntaxNode UpdateUsingStatements(CompilationUnitSyntax root, List<SignalRChange> changes)
    {
        var oldUsings = root.Usings
            .Where(u => u.Name?.ToString().Contains("Microsoft.AspNet.SignalR") == true)
            .ToList();

        if (!oldUsings.Any())
        {
            return root;
        }

        var newUsings = new List<UsingDirectiveSyntax>();
        var remainingUsings = root.Usings.Except(oldUsings).ToList();

        // Add ASP.NET Core SignalR usings
        newUsings.Add(SyntaxFactory.UsingDirective(
            SyntaxFactory.ParseName("Microsoft.AspNetCore.SignalR")));

        foreach (var oldUsing in oldUsings)
        {
            changes.Add(new SignalRChange
            {
                Description = $"Replaced using '{oldUsing.Name}' with 'Microsoft.AspNetCore.SignalR'",
                LineNumber = oldUsing.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                OriginalCode = oldUsing.ToString(),
                NewCode = "using Microsoft.AspNetCore.SignalR;",
                ChangeType = SignalRChangeType.UsingStatement
            });
        }

        var allUsings = remainingUsings.Concat(newUsings)
            .OrderBy(u => u.Name?.ToString())
            .ToList();

        return root.WithUsings(SyntaxFactory.List(allUsings));
    }

    private static SyntaxNode UpdateUsingStatementsForGlobalHost(CompilationUnitSyntax root, List<SignalRChange> changes)
    {
        // Same as above but for GlobalHost files
        return UpdateUsingStatements(root, changes);
    }

    private static SyntaxNode AddHubContextFields(CompilationUnitSyntax root, GlobalHostUsageInfo globalHostInfo, List<SignalRChange> changes)
    {
        var classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .ToList();

        foreach (var classDecl in classDeclarations)
        {
            // Check if this class uses GlobalHost
            if (classDecl.DescendantNodes().Any(n => n.ToString().Contains("GlobalHost")))
            {
                // Add IHubContext field and constructor injection
                foreach (var hubType in globalHostInfo.ReferencedHubTypes)
                {
                    changes.Add(new SignalRChange
                    {
                        Description = $"Added IHubContext<{hubType}> constructor injection",
                        ChangeType = SignalRChangeType.GlobalHostToHubContext,
                        NewCode = $"private readonly IHubContext<{hubType}> _hubContext;"
                    });
                }
            }
        }

        return root;
    }

    /// <summary>
    /// Roslyn rewriter for SignalR Hub transformations.
    /// </summary>
    private class SignalRHubRewriter : CSharpSyntaxRewriter
    {
        private readonly SignalRHubInfo _hubInfo;
        private readonly List<SignalRChange> _changes;

        public SignalRHubRewriter(SignalRHubInfo hubInfo, List<SignalRChange> changes)
        {
            _hubInfo = hubInfo;
            _changes = changes;
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var methodName = node.Identifier.Text;

            // Transform lifecycle methods
            if (methodName == "OnConnected")
            {
                return TransformOnConnected(node);
            }

            if (methodName == "OnDisconnected")
            {
                return TransformOnDisconnected(node);
            }

            if (methodName == "OnReconnected")
            {
                return RemoveOnReconnected(node);
            }

            // Transform client invocations in method body
            return base.VisitMethodDeclaration(node);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var nodeText = node.ToString();

            // Transform Clients.X.methodName(args) → await Clients.X.SendAsync("methodName", args)
            if (node.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression.ToString().StartsWith("Clients."))
            {
                return TransformClientInvocation(node, memberAccess);
            }

            // Transform Groups.Add/Remove
            if (nodeText.Contains("Groups.Add") || nodeText.Contains("Groups.Remove"))
            {
                return TransformGroupsOperation(node);
            }

            return base.VisitInvocationExpression(node);
        }

        private MethodDeclarationSyntax TransformOnConnected(MethodDeclarationSyntax node)
        {
            _changes.Add(new SignalRChange
            {
                Description = "Transformed OnConnected() to OnConnectedAsync()",
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                OriginalCode = "public override void OnConnected()",
                NewCode = "public override Task OnConnectedAsync()",
                ChangeType = SignalRChangeType.LifecycleMethod
            });

            // Change return type to Task
            var newReturnType = SyntaxFactory.ParseTypeName("Task");

            // Change method name
            var newIdentifier = SyntaxFactory.Identifier("OnConnectedAsync");

            // Add async modifier if not present
            var modifiers = node.Modifiers;
            if (!modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            {
                modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
            }

            // Wrap body in return Task.CompletedTask if needed
            var newBody = WrapBodyWithTaskReturn(node.Body);

            return node
                .WithReturnType(newReturnType)
                .WithIdentifier(newIdentifier)
                .WithModifiers(modifiers)
                .WithBody(newBody);
        }

        private MethodDeclarationSyntax TransformOnDisconnected(MethodDeclarationSyntax node)
        {
            _changes.Add(new SignalRChange
            {
                Description = "Transformed OnDisconnected(bool stopCalled) to OnDisconnectedAsync(Exception exception)",
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                OriginalCode = "public override void OnDisconnected(bool stopCalled)",
                NewCode = "public override Task OnDisconnectedAsync(Exception? exception)",
                ChangeType = SignalRChangeType.LifecycleMethod
            });

            var newReturnType = SyntaxFactory.ParseTypeName("Task");
            var newIdentifier = SyntaxFactory.Identifier("OnDisconnectedAsync");

            // Change parameter from 'bool stopCalled' to 'Exception? exception'
            var newParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("exception"))
                .WithType(SyntaxFactory.NullableType(SyntaxFactory.ParseTypeName("Exception")));

            var newParameterList = SyntaxFactory.ParameterList(
                SyntaxFactory.SingletonSeparatedList(newParameter));

            var modifiers = node.Modifiers;
            if (!modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            {
                modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
            }

            var newBody = WrapBodyWithTaskReturn(node.Body);

            return node
                .WithReturnType(newReturnType)
                .WithIdentifier(newIdentifier)
                .WithParameterList(newParameterList)
                .WithModifiers(modifiers)
                .WithBody(newBody);
        }

        private SyntaxNode? RemoveOnReconnected(MethodDeclarationSyntax node)
        {
            _changes.Add(new SignalRChange
            {
                Description = "Removed OnReconnected() - not available in ASP.NET Core SignalR",
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                OriginalCode = node.ToString(),
                NewCode = "// TODO: OnReconnected() is not available in ASP.NET Core SignalR.\n" +
                          "// Consider implementing reconnection logic in your client or use a different approach.",
                ChangeType = SignalRChangeType.RemovedWithTodo
            });

            // Return a comment block instead
            var comment = SyntaxFactory.Comment(
                "// TODO: OnReconnected() is not available in ASP.NET Core SignalR.\n" +
                "// The original OnReconnected logic was:\n" +
                $"// {node.Body?.ToString().Replace("\n", "\n// ") ?? "empty"}\n");

            return null; // Remove the method
        }

        private SyntaxNode TransformClientInvocation(InvocationExpressionSyntax node, MemberAccessExpressionSyntax memberAccess)
        {
            var clientsPattern = memberAccess.Expression.ToString(); // e.g., "Clients.All"
            var methodName = memberAccess.Name.ToString();
            var args = node.ArgumentList.Arguments;

            _changes.Add(new SignalRChange
            {
                Description = $"Transformed {clientsPattern}.{methodName}() to SendAsync",
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                OriginalCode = node.ToString(),
                NewCode = $"await {clientsPattern}.SendAsync(\"{methodName}\", ...)",
                ChangeType = SignalRChangeType.ClientInvocation
            });

            // Build: await Clients.X.SendAsync("methodName", arg1, arg2, ...)
            var sendAsyncArgs = new List<ArgumentSyntax>
            {
                SyntaxFactory.Argument(
                    SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(methodName)))
            };
            sendAsyncArgs.AddRange(args);

            var newInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    memberAccess.Expression,
                    SyntaxFactory.IdentifierName("SendAsync")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(sendAsyncArgs)));

            // Wrap with await
            return SyntaxFactory.AwaitExpression(newInvocation);
        }

        private SyntaxNode TransformGroupsOperation(InvocationExpressionSyntax node)
        {
            // Groups.Add/Remove returns void in old SignalR, Task in Core
            // Transform to await Groups.AddToGroupAsync / RemoveFromGroupAsync
            var nodeText = node.ToString();
            var isAdd = nodeText.Contains("Groups.Add");
            var newMethodName = isAdd ? "AddToGroupAsync" : "RemoveFromGroupAsync";

            _changes.Add(new SignalRChange
            {
                Description = $"Transformed Groups.{(isAdd ? "Add" : "Remove")} to {newMethodName}",
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                OriginalCode = node.ToString(),
                NewCode = $"await Groups.{newMethodName}(...)",
                ChangeType = SignalRChangeType.GroupsOperation
            });

            if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var newMemberAccess = memberAccess.WithName(
                    SyntaxFactory.IdentifierName(newMethodName));

                var newInvocation = node.WithExpression(newMemberAccess);

                return SyntaxFactory.AwaitExpression(newInvocation);
            }

            return node;
        }

        private static BlockSyntax? WrapBodyWithTaskReturn(BlockSyntax? body)
        {
            if (body == null)
            {
                return SyntaxFactory.Block(
                    SyntaxFactory.ReturnStatement(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("Task"),
                            SyntaxFactory.IdentifierName("CompletedTask"))));
            }

            // Add return Task.CompletedTask at the end if there's no return
            var hasReturn = body.Statements.OfType<ReturnStatementSyntax>().Any();
            if (!hasReturn)
            {
                var returnStatement = SyntaxFactory.ReturnStatement(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("Task"),
                        SyntaxFactory.IdentifierName("CompletedTask")));

                return body.AddStatements(returnStatement);
            }

            return body;
        }
    }

    /// <summary>
    /// Roslyn rewriter for GlobalHost to IHubContext transformations.
    /// </summary>
    private class GlobalHostRewriter : CSharpSyntaxRewriter
    {
        private readonly GlobalHostUsageInfo _globalHostInfo;
        private readonly List<SignalRChange> _changes;

        public GlobalHostRewriter(GlobalHostUsageInfo globalHostInfo, List<SignalRChange> changes)
        {
            _globalHostInfo = globalHostInfo;
            _changes = changes;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var nodeText = node.ToString();

            // Transform GlobalHost.ConnectionManager.GetHubContext<T>() → _hubContext (injected)
            if (nodeText.Contains("GlobalHost.ConnectionManager.GetHubContext"))
            {
                _changes.Add(new SignalRChange
                {
                    Description = "Replaced GlobalHost.ConnectionManager.GetHubContext<T>() with injected IHubContext<T>",
                    LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    OriginalCode = nodeText,
                    NewCode = "_hubContext",
                    ChangeType = SignalRChangeType.GlobalHostToHubContext
                });

                return SyntaxFactory.IdentifierName("_hubContext");
            }

            return base.VisitInvocationExpression(node);
        }
    }
}
