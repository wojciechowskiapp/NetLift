using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Rewrites controller method bodies from ASP.NET MVC patterns to ASP.NET Core.
/// Handles DbContext references, TryUpdateModel, FormCollection, and authentication patterns.
/// Also transforms DbContext field declarations to use DI pattern.
/// </summary>
public sealed class ControllerMethodBodyRewriter : CSharpSyntaxRewriter, IControllerMethodBodyRewriter
{
    private readonly HashSet<string> _requiredUsings = new(StringComparer.Ordinal);
    private readonly List<RewriterDiagnostic> _diagnostics = new();
    private readonly HashSet<string> _methodsNeedingAsync = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dbContextFieldsToRemove = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _dbContextFieldTypes = new(StringComparer.Ordinal);
    private ISet<string>? _knownDbContextTypes;
    private int _lowestConfidence = 100;
    private bool _needsDbContextInjection;
    private string? _detectedDbContextType;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredUsings => _requiredUsings;

    /// <inheritdoc />
    public int ConfidenceScore => _lowestConfidence;

    /// <inheritdoc />
    public IReadOnlyCollection<RewriterDiagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc />
    public string Rewrite(string sourceCode) => Rewrite(sourceCode, null);

    /// <inheritdoc />
    public string Rewrite(string sourceCode, ISet<string>? knownDbContextTypes)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return sourceCode;
        }

        // Reset state
        _requiredUsings.Clear();
        _diagnostics.Clear();
        _methodsNeedingAsync.Clear();
        _dbContextFieldsToRemove.Clear();
        _dbContextFieldTypes.Clear();
        _knownDbContextTypes = knownDbContextTypes;
        _lowestConfidence = 100;
        _needsDbContextInjection = false;
        _detectedDbContextType = null;

        // Parse the source code
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // First pass: identify methods that need async transformation and DbContext fields
        // Pass known types for more accurate detection
        var firstPassRewriter = new FirstPassRewriter(
            _methodsNeedingAsync,
            _dbContextFieldsToRemove,
            _dbContextFieldTypes,
            _knownDbContextTypes);
        firstPassRewriter.Visit(root);

        // If we found DbContext fields, we need to inject via DI
        if (_dbContextFieldsToRemove.Count > 0)
        {
            _needsDbContextInjection = true;
            _detectedDbContextType = _dbContextFieldTypes.Values.FirstOrDefault();
        }

        // Second pass: perform transformations
        var rewritten = Visit(root);

        if (rewritten == null)
        {
            return sourceCode;
        }

        // Add DbContext DI injection if needed
        if (_needsDbContextInjection && rewritten is CompilationUnitSyntax compilationUnit)
        {
            rewritten = AddDbContextInjection(compilationUnit);
        }

        // Add new using directives
        rewritten = AddRequiredUsings(rewritten);

        return rewritten.ToFullString();
    }

    /// <summary>
    /// First pass rewriter to identify methods that need async transformation and DbContext fields.
    /// Uses both known DbContext types (from project analysis) and pattern matching for detection.
    /// </summary>
    private sealed class FirstPassRewriter : CSharpSyntaxWalker
    {
        private readonly HashSet<string> _methodsNeedingAsync;
        private readonly HashSet<string> _dbContextFieldsToRemove;
        private readonly Dictionary<string, string> _dbContextFieldTypes;
        private readonly ISet<string>? _knownDbContextTypes;
        private MethodDeclarationSyntax? _currentMethod;

        public FirstPassRewriter(
            HashSet<string> methodsNeedingAsync,
            HashSet<string> dbContextFieldsToRemove,
            Dictionary<string, string> dbContextFieldTypes,
            ISet<string>? knownDbContextTypes = null)
        {
            _methodsNeedingAsync = methodsNeedingAsync;
            _dbContextFieldsToRemove = dbContextFieldsToRemove;
            _dbContextFieldTypes = dbContextFieldTypes;
            _knownDbContextTypes = knownDbContextTypes;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var previousMethod = _currentMethod;
            _currentMethod = node;
            base.VisitMethodDeclaration(node);
            _currentMethod = previousMethod;
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            var typeName = node.Declaration.Type.ToString();

            // PRIORITY 1: Check if type is in known DbContext types (from project analysis)
            // This is the most accurate detection - uses actual inheritance from DbContext
            var isKnownDbContextType = _knownDbContextTypes?.Contains(typeName) == true;

            // PRIORITY 2: Pattern matching for types not in known set (fallback)
            // Detect DbContext types by common naming patterns:
            // - *Entities (EF6 style: MusicStoreEntities, NorthwindEntities)
            // - *DbContext (EF Core style: ApplicationDbContext, MyAppDbContext)
            // - *DB (short form: StoreDB, ProductDB, UserDB)
            // - *Context (legacy: DataContext, AppContext) - requires direct instantiation to avoid false positives
            var isDefiniteDbContextType = typeName.EndsWith("Entities", StringComparison.Ordinal) ||
                                          typeName.EndsWith("DbContext", StringComparison.Ordinal) ||
                                          typeName.EndsWith("DB", StringComparison.Ordinal);
            var isPossibleDbContextType = typeName.EndsWith("Context", StringComparison.Ordinal);

            foreach (var variable in node.Declaration.Variables)
            {
                var fieldName = variable.Identifier.Text;
                var hasDirectInstantiation = variable.Initializer?.Value is ObjectCreationExpressionSyntax;

                // Detection logic with priority:
                // 1. Known DbContext type (100% accurate from inheritance analysis)
                // 2. Definite pattern match (*Entities, *DbContext, *DB)
                // 3. Possible pattern match (*Context) with direct instantiation
                // 4. Direct instantiation where instantiated type matches patterns

                if (isKnownDbContextType)
                {
                    // Highest confidence - type confirmed from project analysis
                    _dbContextFieldsToRemove.Add(fieldName);
                    _dbContextFieldTypes[fieldName] = typeName;
                }
                else if (isDefiniteDbContextType)
                {
                    // High confidence - strong pattern match
                    _dbContextFieldsToRemove.Add(fieldName);
                    _dbContextFieldTypes[fieldName] = typeName;
                }
                else if (hasDirectInstantiation)
                {
                    // Check if instantiated type matches patterns or is in known types
                    var initializerType = (variable.Initializer!.Value as ObjectCreationExpressionSyntax)?.Type.ToString();

                    var isKnownInitializerType = initializerType != null &&
                        _knownDbContextTypes?.Contains(initializerType) == true;

                    // Only detect by pattern matching - don't detect arbitrary types
                    var isDbLikeInstantiation = initializerType != null && (
                        initializerType.EndsWith("Entities", StringComparison.Ordinal) ||
                        initializerType.EndsWith("DbContext", StringComparison.Ordinal) ||
                        initializerType.EndsWith("DB", StringComparison.Ordinal) ||
                        initializerType.EndsWith("Context", StringComparison.Ordinal));

                    if (isKnownInitializerType || isDbLikeInstantiation || isPossibleDbContextType)
                    {
                        _dbContextFieldsToRemove.Add(fieldName);
                        _dbContextFieldTypes[fieldName] = typeName;
                    }
                }
            }

            base.VisitFieldDeclaration(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Check for TryUpdateModel calls
            if (node.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.Text == "TryUpdateModel" &&
                _currentMethod != null)
            {
                var methodKey = GetMethodKey(_currentMethod);
                _methodsNeedingAsync.Add(methodKey);
            }

            base.VisitInvocationExpression(node);
        }

        private static string GetMethodKey(MethodDeclarationSyntax method)
        {
            // Create unique key based on method position and identifier
            return $"{method.Identifier.Text}_{method.SpanStart}";
        }
    }

    /// <summary>
    /// Visits member access expressions to transform DbContext field references.
    /// Uses the dynamically detected field names from FirstPassRewriter (generic approach).
    /// </summary>
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var visited = (MemberAccessExpressionSyntax?)base.VisitMemberAccessExpression(node);
        if (visited == null)
        {
            return null;
        }

        // Check if the left side is an identifier that was detected as a DbContext field
        if (visited.Expression is IdentifierNameSyntax identifier)
        {
            var name = identifier.Identifier.Text;

            // Transform ANY detected DbContext field reference to _context
            // This is generic - works with any field name (storeDB, db, dataContext, entities, etc.)
            if (_dbContextFieldsToRemove.Contains(name))
            {
                var newIdentifier = SyntaxFactory.IdentifierName("_context")
                    .WithTriviaFrom(identifier);

                var result = visited.WithExpression(newIdentifier);

                _lowestConfidence = Math.Min(_lowestConfidence, 95);
                _diagnostics.Add(new RewriterDiagnostic(
                    $"Transformed '{name}.' to '_context.' for DbContext field reference",
                    RewriterDiagnosticSeverity.Info));

                return result;
            }
        }

        return visited;
    }

    /// <summary>
    /// Visits invocation expressions to transform method calls.
    /// </summary>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var visited = (InvocationExpressionSyntax?)base.VisitInvocationExpression(node);
        if (visited == null)
        {
            return null;
        }

        // Check for TryUpdateModel(x) → await TryUpdateModelAsync(x)
        if (visited.Expression is IdentifierNameSyntax identifier &&
            identifier.Identifier.Text == "TryUpdateModel")
        {
            var newIdentifier = SyntaxFactory.IdentifierName("TryUpdateModelAsync");

            var newInvocation = visited.WithExpression(newIdentifier)
                .WithLeadingTrivia(SyntaxFactory.TriviaList()); // Clear leading trivia from invocation

            // Wrap with await and preserve original trivia
            var awaitExpression = SyntaxFactory.AwaitExpression(
                SyntaxFactory.Token(SyntaxKind.AwaitKeyword)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                newInvocation)
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());

            _requiredUsings.Add("System.Threading.Tasks");
            _lowestConfidence = Math.Min(_lowestConfidence, 90);
            _diagnostics.Add(new RewriterDiagnostic(
                "Transformed 'TryUpdateModel(x)' to 'await TryUpdateModelAsync(x)'",
                RewriterDiagnosticSeverity.Info));

            return awaitExpression;
        }

        // Check for Membership.CreateUser
        if (visited.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression is IdentifierNameSyntax membershipId &&
            membershipId.Identifier.Text == "Membership" &&
            memberAccess.Name.Identifier.Text == "CreateUser")
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 40);
            _diagnostics.Add(new RewriterDiagnostic(
                "Found Membership.CreateUser - requires manual migration to ASP.NET Core Identity (UserManager<IdentityUser>)",
                RewriterDiagnosticSeverity.Warning));

            // Add TODO comment
            var todoComment = SyntaxFactory.Comment(
                "// TODO: Replace Membership.CreateUser with ASP.NET Core Identity UserManager\n" +
                "            // Example: var user = new IdentityUser { UserName = username, Email = email };\n" +
                "            //          var result = await _userManager.CreateAsync(user, password);\n" +
                "            ");

            return visited.WithLeadingTrivia(
                visited.GetLeadingTrivia().Insert(0, todoComment));
        }

        // Check for Membership.ValidateUser
        if (visited.Expression is MemberAccessExpressionSyntax validateMember &&
            validateMember.Expression is IdentifierNameSyntax validateId &&
            validateId.Identifier.Text == "Membership" &&
            validateMember.Name.Identifier.Text == "ValidateUser")
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 40);
            _diagnostics.Add(new RewriterDiagnostic(
                "Found Membership.ValidateUser - requires manual migration to ASP.NET Core Identity (SignInManager<IdentityUser>)",
                RewriterDiagnosticSeverity.Warning));

            // Add TODO comment
            var todoComment = SyntaxFactory.Comment(
                "// TODO: Replace Membership.ValidateUser with ASP.NET Core Identity SignInManager\n" +
                "            // Example: var result = await _signInManager.PasswordSignInAsync(username, password, isPersistent, lockoutOnFailure);\n" +
                "            ");

            return visited.WithLeadingTrivia(
                visited.GetLeadingTrivia().Insert(0, todoComment));
        }

        // Check for FormsAuthentication.SetAuthCookie
        if (visited.Expression is MemberAccessExpressionSyntax authMember &&
            authMember.Expression is IdentifierNameSyntax authId &&
            authId.Identifier.Text == "FormsAuthentication" &&
            authMember.Name.Identifier.Text == "SetAuthCookie")
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 40);
            _diagnostics.Add(new RewriterDiagnostic(
                "Found FormsAuthentication.SetAuthCookie - requires manual migration to ASP.NET Core Identity (SignInManager handles authentication automatically)",
                RewriterDiagnosticSeverity.Warning));

            // Add TODO comment
            var todoComment = SyntaxFactory.Comment(
                "// TODO: Remove FormsAuthentication.SetAuthCookie - handled automatically by SignInManager.PasswordSignInAsync\n" +
                "            ");

            return visited.WithLeadingTrivia(
                visited.GetLeadingTrivia().Insert(0, todoComment));
        }

        return visited;
    }

    /// <summary>
    /// Visits parameter declarations to transform FormCollection to IFormCollection.
    /// </summary>
    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        var visited = (ParameterSyntax?)base.VisitParameter(node);
        if (visited == null)
        {
            return null;
        }

        // Check if parameter type is FormCollection
        if (visited.Type is IdentifierNameSyntax typeIdentifier &&
            typeIdentifier.Identifier.Text == "FormCollection")
        {
            var newType = SyntaxFactory.IdentifierName("IFormCollection")
                .WithTriviaFrom(typeIdentifier);

            var result = visited.WithType(newType);

            _requiredUsings.Add("Microsoft.AspNetCore.Http");
            _lowestConfidence = Math.Min(_lowestConfidence, 95);
            _diagnostics.Add(new RewriterDiagnostic(
                "Transformed 'FormCollection' to 'IFormCollection' for ASP.NET Core compatibility",
                RewriterDiagnosticSeverity.Info));

            return result;
        }

        return visited;
    }

    /// <summary>
    /// Visits method declarations to add async modifier when needed.
    /// </summary>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax?)base.VisitMethodDeclaration(node);
        if (visited == null)
        {
            return null;
        }

        var methodKey = GetMethodKey(node);

        // Check if this method needs async transformation
        if (_methodsNeedingAsync.Contains(methodKey))
        {
            // Check if already async
            if (visited.Modifiers.Any(SyntaxKind.AsyncKeyword))
            {
                return visited;
            }

            // Add async modifier
            var asyncModifier = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);

            var newModifiers = visited.Modifiers.Add(asyncModifier);
            var result = visited.WithModifiers(newModifiers);

            // Transform return type: ActionResult → Task<ActionResult>, void → Task
            result = TransformReturnTypeForAsync(result);

            _requiredUsings.Add("System.Threading.Tasks");
            _diagnostics.Add(new RewriterDiagnostic(
                $"Added 'async' modifier to method '{visited.Identifier.Text}' due to async API usage",
                RewriterDiagnosticSeverity.Info));

            return result;
        }

        return visited;
    }

    /// <summary>
    /// Visits identifier names to handle type references like MembershipCreateStatus.
    /// </summary>
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var visited = (IdentifierNameSyntax?)base.VisitIdentifierName(node);
        if (visited == null)
        {
            return null;
        }

        // Check for MembershipCreateStatus type
        if (visited.Identifier.Text == "MembershipCreateStatus")
        {
            _lowestConfidence = Math.Min(_lowestConfidence, 40);
            _diagnostics.Add(new RewriterDiagnostic(
                "Found MembershipCreateStatus - requires manual migration to ASP.NET Core Identity (use IdentityResult)",
                RewriterDiagnosticSeverity.Warning));

            // Add TODO comment as trivia
            var todoComment = SyntaxFactory.Comment(
                "/* TODO: Replace MembershipCreateStatus with IdentityResult from ASP.NET Core Identity */ ");

            return visited.WithLeadingTrivia(
                visited.GetLeadingTrivia().Insert(0, todoComment));
        }

        return visited;
    }

    /// <summary>
    /// Visits field declarations to remove DbContext fields that will be replaced with DI.
    /// </summary>
    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var visited = (FieldDeclarationSyntax?)base.VisitFieldDeclaration(node);
        if (visited == null)
        {
            return null;
        }

        // Check if any variable in this field should be removed
        foreach (var variable in visited.Declaration.Variables)
        {
            if (_dbContextFieldsToRemove.Contains(variable.Identifier.Text))
            {
                var fieldName = variable.Identifier.Text;
                var typeName = visited.Declaration.Type.ToString();

                _diagnostics.Add(new RewriterDiagnostic(
                    $"Removed direct DbContext instantiation '{typeName} {fieldName}' - will be injected via constructor",
                    RewriterDiagnosticSeverity.Info));

                // Return null to remove this field - it will be replaced with DI
                return null;
            }
        }

        return visited;
    }

    /// <summary>
    /// Transforms return type for async methods.
    /// </summary>
    private static MethodDeclarationSyntax TransformReturnTypeForAsync(MethodDeclarationSyntax method)
    {
        var returnType = method.ReturnType;

        // If already Task or Task<T>, no transformation needed
        if (returnType is GenericNameSyntax generic &&
            generic.Identifier.Text == "Task")
        {
            return method;
        }

        if (returnType is IdentifierNameSyntax identifier &&
            identifier.Identifier.Text == "Task")
        {
            return method;
        }

        // Transform void → Task
        if (returnType is PredefinedTypeSyntax predefined &&
            predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            var newReturnType = SyntaxFactory.IdentifierName("Task")
                .WithTriviaFrom(returnType);

            return method.WithReturnType(newReturnType);
        }

        // Transform T → Task<T>
        var taskType = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("Task"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList(returnType.WithoutTrivia())))
            .WithTriviaFrom(returnType);

        return method.WithReturnType(taskType);
    }

    /// <summary>
    /// Gets a unique key for a method.
    /// </summary>
    private static string GetMethodKey(MethodDeclarationSyntax method)
    {
        return $"{method.Identifier.Text}_{method.SpanStart}";
    }

    /// <summary>
    /// Adds DbContext DI injection to the controller class.
    /// </summary>
    private CompilationUnitSyntax AddDbContextInjection(CompilationUnitSyntax compilationUnit)
    {
        var classDecl = compilationUnit.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDecl == null)
        {
            return compilationUnit;
        }

        // Check if _context field already exists
        var hasContextField = classDecl.Members
            .OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "_context"));

        if (hasContextField)
        {
            return compilationUnit;
        }

        // Create the _context field
        var contextTypeName = _detectedDbContextType ?? "ApplicationDbContext";
        var fieldCode = $"private readonly {contextTypeName} _context;";
        var fieldTree = CSharpSyntaxTree.ParseText($"class Temp {{ {fieldCode} }}");
        var fieldRoot = fieldTree.GetRoot();
        var contextFieldDecl = fieldRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .First()
            .WithLeadingTrivia(SyntaxFactory.Whitespace("        "))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        // Find existing constructor
        var existingConstructor = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        ClassDeclarationSyntax updatedClass;

        if (existingConstructor != null)
        {
            // Add _context parameter to existing constructor
            updatedClass = AddContextToExistingConstructor(classDecl, existingConstructor, contextTypeName, contextFieldDecl);
        }
        else
        {
            // Create new constructor with _context parameter
            var className = classDecl.Identifier.Text;
            var constructorCode = $@"public {className}({contextTypeName} context)
        {{
            _context = context;
        }}";
            var constructorTree = CSharpSyntaxTree.ParseText($"class Temp {{ {constructorCode} }}");
            var constructorRoot = constructorTree.GetRoot();
            var newConstructor = constructorRoot.DescendantNodes()
                .OfType<ConstructorDeclarationSyntax>()
                .First()
                .WithLeadingTrivia(SyntaxFactory.Whitespace("        "))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

            // Add field at the beginning of class members
            var newMembers = classDecl.Members.Insert(0, contextFieldDecl);

            // Find position after any existing fields (before first method)
            var insertIndex = 1;
            for (var i = 0; i < newMembers.Count; i++)
            {
                if (newMembers[i] is MethodDeclarationSyntax)
                {
                    insertIndex = i;
                    break;
                }
                if (newMembers[i] is FieldDeclarationSyntax)
                {
                    insertIndex = i + 1;
                }
            }

            newMembers = newMembers.Insert(insertIndex, newConstructor);
            updatedClass = classDecl.WithMembers(newMembers);

            _diagnostics.Add(new RewriterDiagnostic(
                $"Added DbContext constructor injection to {className}",
                RewriterDiagnosticSeverity.Info));
        }

        // Replace the class in the compilation unit
        return compilationUnit.ReplaceNode(classDecl, updatedClass);
    }

    /// <summary>
    /// Adds _context parameter to an existing constructor.
    /// </summary>
    private ClassDeclarationSyntax AddContextToExistingConstructor(
        ClassDeclarationSyntax classDecl,
        ConstructorDeclarationSyntax existingConstructor,
        string contextTypeName,
        FieldDeclarationSyntax contextFieldDecl)
    {
        var className = classDecl.Identifier.Text;

        // Check if context parameter already exists
        var hasContextParam = existingConstructor.ParameterList.Parameters
            .Any(p => p.Identifier.Text == "context" ||
                      p.Type?.ToString().Contains("DbContext") == true ||
                      p.Type?.ToString().Contains("Entities") == true);

        if (hasContextParam)
        {
            // Just add the field if it doesn't exist
            var membersWithField = classDecl.Members.Insert(0, contextFieldDecl);
            return classDecl.WithMembers(membersWithField);
        }

        // Build new parameter list by parsing from template
        var existingParams = string.Join(", ", existingConstructor.ParameterList.Parameters.Select(p => p.ToString()));
        var newParamListCode = string.IsNullOrEmpty(existingParams)
            ? $"({contextTypeName} context)"
            : $"({contextTypeName} context, {existingParams})";

        var paramListTree = CSharpSyntaxTree.ParseText($"class Temp {{ void M{newParamListCode} {{}} }}");
        var paramListRoot = paramListTree.GetRoot();
        var newParameterList = paramListRoot.DescendantNodes()
            .OfType<ParameterListSyntax>()
            .First();

        // Parse assignment from template
        var assignmentCode = "_context = context;";
        var assignmentTree = CSharpSyntaxTree.ParseText($"class Temp {{ void M() {{ {assignmentCode} }} }}");
        var assignmentRoot = assignmentTree.GetRoot();
        var assignment = assignmentRoot.DescendantNodes()
            .OfType<ExpressionStatementSyntax>()
            .First()
            .WithLeadingTrivia(SyntaxFactory.Whitespace("            "))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        // Add assignment at the beginning of constructor body
        var newBody = existingConstructor.Body != null
            ? existingConstructor.Body.WithStatements(
                existingConstructor.Body.Statements.Insert(0, assignment))
            : SyntaxFactory.Block(assignment);

        var newConstructor = existingConstructor
            .WithParameterList(newParameterList)
            .WithBody(newBody);

        // Replace constructor in class members
        var newMembers = classDecl.Members.Replace(existingConstructor, newConstructor);

        // Add context field at the beginning
        newMembers = newMembers.Insert(0, contextFieldDecl);

        _diagnostics.Add(new RewriterDiagnostic(
            $"Added DbContext parameter to existing constructor in {className}",
            RewriterDiagnosticSeverity.Info));

        return classDecl.WithMembers(newMembers);
    }

    /// <summary>
    /// Adds required using directives that were identified during rewriting.
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
}
