# Task 034: Controller Base Class Rewriter

## Meta
- **Priority**: P0
- **Estimate**: 8 points
- **Sprint**: 4
- **Dependencies**: 033
- **Status**: Not Started

## Description
Implement Roslyn SyntaxRewriter to update controller base class inheritance and constructor patterns from MVC5 to ASP.NET Core, including dependency injection setup.

## Acceptance Criteria
- [ ] ControllerBaseClassRewriter implemented
- [ ] Controller → Controller (ASP.NET Core)
- [ ] ApiController → ControllerBase
- [ ] Update constructor patterns for DI
- [ ] Remove parameterless constructors where DI is needed
- [ ] Add constructor DI for common dependencies (ILogger, etc.)
- [ ] Handle custom base controllers
- [ ] Unit tests with 95%+ coverage
- [ ] Preserves XML documentation and attributes

## Technical Notes

### Base Class Mapping
```csharp
public static class ControllerBaseMappings
{
    public static readonly Dictionary<string, ControllerMigrationStrategy> Mappings = new()
    {
        // MVC Controllers (with views)
        ["Controller"] = new ControllerMigrationStrategy
        {
            NewBaseClass = "Controller",
            Namespace = "Microsoft.AspNetCore.Mvc",
            RequiresDI = true,
            CommonDependencies = new[] { "ILogger" }
        },

        // Web API Controllers
        ["ApiController"] = new ControllerMigrationStrategy
        {
            NewBaseClass = "ControllerBase",
            Namespace = "Microsoft.AspNetCore.Mvc",
            RequiresDI = true,
            AddAttribute = "[ApiController]",
            CommonDependencies = new[] { "ILogger" }
        },

        // Custom base classes - handle separately
        ["BaseController"] = new ControllerMigrationStrategy
        {
            IsCustomBase = true,
            InheritFrom = "Controller"
        }
    };
}

public class ControllerMigrationStrategy
{
    public string NewBaseClass { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public bool RequiresDI { get; set; }
    public string[]? CommonDependencies { get; set; }
    public string? AddAttribute { get; set; }
    public bool IsCustomBase { get; set; }
    public string? InheritFrom { get; set; }
}
```

### Roslyn SyntaxRewriter Implementation
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetLift.Mvc.Rewriters;

/// <summary>
/// Rewrites MVC5 Controller base classes to ASP.NET Core equivalents
/// </summary>
public class ControllerBaseClassRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<string, ControllerMigrationStrategy> _mappings;
    private readonly HashSet<string> _requiredUsings = new();

    public ControllerBaseClassRewriter(
        SemanticModel semanticModel,
        Dictionary<string, ControllerMigrationStrategy>? customMappings = null)
    {
        _semanticModel = semanticModel;
        _mappings = customMappings ?? ControllerBaseMappings.Mappings;
    }

    public IEnumerable<string> RequiredUsings => _requiredUsings;

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // Check if this is a controller class
        if (!IsControllerClass(node, out var baseTypeName))
            return base.VisitClassDeclaration(node);

        if (baseTypeName == null ||
            !_mappings.TryGetValue(baseTypeName, out var strategy))
            return base.VisitClassDeclaration(node);

        var newNode = node;

        // Update base class
        newNode = UpdateBaseClass(newNode, strategy);

        // Add attributes if needed
        if (!string.IsNullOrEmpty(strategy.AddAttribute))
        {
            newNode = AddAttribute(newNode, strategy.AddAttribute);
        }

        // Update or add constructor for DI
        if (strategy.RequiresDI)
        {
            newNode = EnsureDIConstructor(newNode, strategy);
        }

        // Track required using
        if (!string.IsNullOrEmpty(strategy.Namespace))
        {
            _requiredUsings.Add(strategy.Namespace);
        }

        return base.VisitClassDeclaration(newNode);
    }

    private bool IsControllerClass(
        ClassDeclarationSyntax node,
        out string? baseTypeName)
    {
        baseTypeName = null;

        if (node.BaseList == null || !node.BaseList.Types.Any())
            return false;

        var firstBase = node.BaseList.Types.First().Type;

        // Check via semantic model
        var symbolInfo = _semanticModel.GetSymbolInfo(firstBase);
        if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
        {
            baseTypeName = typeSymbol.Name;

            // Check if it's in System.Web.Mvc namespace
            var containingNamespace = typeSymbol.ContainingNamespace?.ToDisplayString();
            if (containingNamespace?.StartsWith("System.Web.Mvc") == true)
                return true;
        }

        // Fallback to name matching
        baseTypeName = firstBase switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            QualifiedNameSyntax qn => qn.Right.Identifier.Text,
            _ => null
        };

        return baseTypeName != null &&
               (baseTypeName == "Controller" ||
                baseTypeName == "ApiController" ||
                baseTypeName.EndsWith("Controller"));
    }

    private ClassDeclarationSyntax UpdateBaseClass(
        ClassDeclarationSyntax node,
        ControllerMigrationStrategy strategy)
    {
        if (node.BaseList == null || !node.BaseList.Types.Any())
            return node;

        var newBaseType = SyntaxFactory.SimpleBaseType(
            SyntaxFactory.IdentifierName(strategy.NewBaseClass));

        var newTypes = node.BaseList.Types.Replace(
            node.BaseList.Types.First(),
            newBaseType);

        return node.WithBaseList(
            node.BaseList.WithTypes(newTypes));
    }

    private ClassDeclarationSyntax AddAttribute(
        ClassDeclarationSyntax node,
        string attributeText)
    {
        // Parse attribute (e.g., "[ApiController]")
        var attribute = SyntaxFactory.ParseCompilationUnit(attributeText)
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .FirstOrDefault();

        if (attribute == null)
            return node;

        // Check if attribute already exists
        var hasAttribute = node.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString() == attribute.Name.ToString());

        if (hasAttribute)
            return node;

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        return node.AddAttributeLists(attributeList);
    }

    private ClassDeclarationSyntax EnsureDIConstructor(
        ClassDeclarationSyntax node,
        ControllerMigrationStrategy strategy)
    {
        var existingConstructor = node.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        // Analyze existing dependencies
        var dependencies = AnalyzeDependencies(node);

        // Add common dependencies
        if (strategy.CommonDependencies != null)
        {
            foreach (var dep in strategy.CommonDependencies)
            {
                if (!dependencies.Any(d => d.TypeName.Contains(dep)))
                {
                    dependencies.Add(new DependencyInfo
                    {
                        TypeName = $"ILogger<{node.Identifier.Text}>",
                        ParameterName = "logger",
                        FieldName = "_logger"
                    });
                }
            }
        }

        if (!dependencies.Any())
            return node; // No DI needed

        if (existingConstructor == null)
        {
            // Create new constructor
            return AddConstructor(node, dependencies);
        }
        else
        {
            // Update existing constructor
            return UpdateConstructor(node, existingConstructor, dependencies);
        }
    }

    private List<DependencyInfo> AnalyzeDependencies(ClassDeclarationSyntax node)
    {
        var dependencies = new List<DependencyInfo>();

        // Look for field assignments in existing code
        var fields = node.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.Modifiers.Any(SyntaxKind.PrivateKeyword) ||
                       f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword));

        foreach (var field in fields)
        {
            var variable = field.Declaration.Variables.FirstOrDefault();
            if (variable == null)
                continue;

            var fieldName = variable.Identifier.Text;
            var typeName = field.Declaration.Type.ToString();

            // Check if it's an interface (likely a dependency)
            if (typeName.StartsWith("I") && char.IsUpper(typeName[1]))
            {
                dependencies.Add(new DependencyInfo
                {
                    TypeName = typeName,
                    ParameterName = ToCamelCase(fieldName.TrimStart('_')),
                    FieldName = fieldName
                });
            }
        }

        return dependencies;
    }

    private ClassDeclarationSyntax AddConstructor(
        ClassDeclarationSyntax node,
        List<DependencyInfo> dependencies)
    {
        // Create parameters
        var parameters = dependencies
            .Select(d => SyntaxFactory.Parameter(
                SyntaxFactory.Identifier(d.ParameterName))
                .WithType(SyntaxFactory.ParseTypeName(d.TypeName)))
            .ToArray();

        // Create assignments
        var assignments = dependencies
            .Select(d => SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(d.FieldName),
                    SyntaxFactory.IdentifierName(d.ParameterName))))
            .ToArray();

        // Build constructor
        var constructor = SyntaxFactory.ConstructorDeclaration(node.Identifier)
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(parameters)))
            .WithBody(SyntaxFactory.Block(assignments))
            .WithLeadingTrivia(
                SyntaxFactory.Trivia(SyntaxFactory.DocumentationCommentTrivia(
                    SyntaxKind.SingleLineDocumentationCommentTrivia,
                    SyntaxFactory.List(new XmlNodeSyntax[]
                    {
                        SyntaxFactory.XmlText("/// <summary>"),
                        SyntaxFactory.XmlText(
                            $"/// Initializes a new instance of {node.Identifier.Text}"),
                        SyntaxFactory.XmlText("/// </summary>")
                    }))));

        // Add readonly fields if they don't exist
        var newNode = node;
        foreach (var dep in dependencies)
        {
            if (!HasField(node, dep.FieldName))
            {
                newNode = AddReadOnlyField(newNode, dep);
            }
        }

        return newNode.AddMembers(constructor);
    }

    private ClassDeclarationSyntax UpdateConstructor(
        ClassDeclarationSyntax node,
        ConstructorDeclarationSyntax existingConstructor,
        List<DependencyInfo> dependencies)
    {
        // Merge existing parameters with new dependencies
        var existingParams = existingConstructor.ParameterList.Parameters
            .Select(p => p.Identifier.Text)
            .ToHashSet();

        var newDeps = dependencies
            .Where(d => !existingParams.Contains(d.ParameterName))
            .ToList();

        if (!newDeps.Any())
            return node; // No new dependencies to add

        // Add new parameters
        var newParameters = newDeps
            .Select(d => SyntaxFactory.Parameter(
                SyntaxFactory.Identifier(d.ParameterName))
                .WithType(SyntaxFactory.ParseTypeName(d.TypeName)))
            .ToArray();

        var updatedParameters = existingConstructor.ParameterList
            .AddParameters(newParameters);

        // Add new assignments
        var newAssignments = newDeps
            .Select(d => SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(d.FieldName),
                    SyntaxFactory.IdentifierName(d.ParameterName))))
            .ToArray();

        var updatedBody = existingConstructor.Body?.AddStatements(newAssignments)
            ?? SyntaxFactory.Block(newAssignments);

        var updatedConstructor = existingConstructor
            .WithParameterList(updatedParameters)
            .WithBody(updatedBody);

        // Add readonly fields
        var newNode = node;
        foreach (var dep in newDeps)
        {
            if (!HasField(node, dep.FieldName))
            {
                newNode = AddReadOnlyField(newNode, dep);
            }
        }

        return newNode.ReplaceNode(existingConstructor, updatedConstructor);
    }

    private bool HasField(ClassDeclarationSyntax node, string fieldName)
    {
        return node.Members
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables)
            .Any(v => v.Identifier.Text == fieldName);
    }

    private ClassDeclarationSyntax AddReadOnlyField(
        ClassDeclarationSyntax node,
        DependencyInfo dependency)
    {
        var field = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.ParseTypeName(dependency.TypeName))
                .AddVariables(
                    SyntaxFactory.VariableDeclarator(dependency.FieldName)))
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        // Insert at the beginning of the class
        var firstMember = node.Members.FirstOrDefault();
        if (firstMember != null)
        {
            var index = node.Members.IndexOf(firstMember);
            return node.WithMembers(node.Members.Insert(index, field));
        }

        return node.AddMembers(field);
    }

    private string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return char.ToLowerInvariant(text[0]) + text.Substring(1);
    }
}

public class DependencyInfo
{
    public string TypeName { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
}
```

### Usage Example
```csharp
public class ControllerBaseMigrator
{
    public async Task<Document> MigrateControllerAsync(Document document)
    {
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();

        if (root == null || semanticModel == null)
            return document;

        var rewriter = new ControllerBaseClassRewriter(semanticModel);
        var newRoot = rewriter.Visit(root);

        if (newRoot == null)
            return document;

        // Add required using directives
        newRoot = AddRequiredUsings(newRoot, rewriter.RequiredUsings);

        return document.WithSyntaxRoot(newRoot);
    }

    private SyntaxNode AddRequiredUsings(
        SyntaxNode root,
        IEnumerable<string> usings)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
            return root;

        var existingUsings = compilationUnit.Usings
            .Select(u => u.Name?.ToString())
            .ToHashSet();

        var newUsings = usings
            .Where(u => !existingUsings.Contains(u))
            .Select(u => SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName(u)))
            .ToArray();

        if (!newUsings.Any())
            return root;

        return compilationUnit.AddUsings(newUsings);
    }
}
```

### Migration Examples

#### Example 1: Simple Controller
```csharp
// BEFORE (MVC5)
using System.Web.Mvc;

namespace MyApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}

// AFTER (ASP.NET Core)
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MyApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        /// <summary>
        /// Initializes a new instance of HomeController
        /// </summary>
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
```

#### Example 2: API Controller
```csharp
// BEFORE (MVC5 Web API)
using System.Web.Http;

namespace MyApp.Controllers
{
    public class ProductsController : ApiController
    {
        public IHttpActionResult Get()
        {
            return Ok(new[] { "Product1", "Product2" });
        }
    }
}

// AFTER (ASP.NET Core)
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MyApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ILogger<ProductsController> _logger;

        /// <summary>
        /// Initializes a new instance of ProductsController
        /// </summary>
        public ProductsController(ILogger<ProductsController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new[] { "Product1", "Product2" });
        }
    }
}
```

#### Example 3: Controller with Existing Dependencies
```csharp
// BEFORE
using System.Web.Mvc;

namespace MyApp.Controllers
{
    public class OrdersController : Controller
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        public ActionResult Index()
        {
            var orders = _orderService.GetAll();
            return View(orders);
        }
    }
}

// AFTER
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MyApp.Controllers
{
    public class OrdersController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        /// <summary>
        /// Initializes a new instance of OrdersController
        /// </summary>
        public OrdersController(
            IOrderService orderService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var orders = _orderService.GetAll();
            return View(orders);
        }
    }
}
```

## Progress Log
- [Created] - Task definition with comprehensive Roslyn implementation
