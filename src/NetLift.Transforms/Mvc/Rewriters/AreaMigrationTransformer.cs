using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Mvc;

namespace NetLift.Transforms.Mvc.Rewriters;

/// <summary>
/// Transforms MVC Area definitions into ASP.NET Core migration plans.
/// Generates folder structure, _ViewImports.cshtml, _ViewStart.cshtml, and route registration code.
/// </summary>
public sealed class AreaMigrationTransformer : IAreaMigrationTransformer
{
    private readonly IViewImportsGenerator _viewImportsGenerator;

    public AreaMigrationTransformer(IViewImportsGenerator viewImportsGenerator)
    {
        _viewImportsGenerator = viewImportsGenerator ?? throw new ArgumentNullException(nameof(viewImportsGenerator));
    }

    /// <inheritdoc />
    public AreaMigrationPlan CreateMigrationPlan(AreaDefinition area, string projectRoot, string rootNamespace)
    {
        if (area == null)
        {
            throw new ArgumentNullException(nameof(area));
        }

        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Project root cannot be null or whitespace.", nameof(projectRoot));
        }

        if (string.IsNullOrWhiteSpace(rootNamespace))
        {
            throw new ArgumentException("Root namespace cannot be null or whitespace.", nameof(rootNamespace));
        }

        var areaName = area.Name;
        var plan = new AreaMigrationPlan
        {
            AreaName = areaName,
            FoldersToCreate = GenerateFolderStructure(areaName),
            FilesToGenerate = GenerateFiles(area, rootNamespace),
            ControllersToUpdate = new List<string>(), // Will be populated by the caller when scanning for controllers
            RouteRegistration = GenerateRouteRegistration(area),
            ConfidenceScore = 95, // High confidence for standard area migration
            Diagnostics = new List<string>
            {
                $"Area '{areaName}' will be migrated to ASP.NET Core conventions",
                $"Controllers in this area will need [Area(\"{areaName}\")] attribute"
            }
        };

        return plan;
    }

    /// <inheritdoc />
    public string AddAreaAttribute(string controllerSource, string areaName)
    {
        if (string.IsNullOrWhiteSpace(controllerSource))
        {
            return controllerSource;
        }

        if (string.IsNullOrWhiteSpace(areaName))
        {
            throw new ArgumentException("Area name cannot be null or whitespace.", nameof(areaName));
        }

        var tree = CSharpSyntaxTree.ParseText(controllerSource);
        var root = tree.GetRoot();

        // Find controller classes that need the attribute
        var controllerClasses = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(IsControllerClass)
            .Where(c => !HasAreaAttribute(c))
            .ToList();

        if (controllerClasses.Count == 0)
        {
            return controllerSource;
        }

        // Build a dictionary of replacements
        var replacements = controllerClasses.ToDictionary(
            classDecl => classDecl,
            classDecl => AddAreaAttributeToClass(classDecl, areaName));

        // Apply all replacements at once
        var newRoot = root.ReplaceNodes(replacements.Keys, (oldNode, _) => replacements[oldNode]);

        // Add using directive if needed
        newRoot = EnsureUsingDirective(newRoot, "Microsoft.AspNetCore.Mvc");

        return newRoot.ToFullString();
    }

    /// <summary>
    /// Generates the folder structure for the area.
    /// </summary>
    private static List<string> GenerateFolderStructure(string areaName)
    {
        return new List<string>
        {
            $"Areas/{areaName}",
            $"Areas/{areaName}/Controllers",
            $"Areas/{areaName}/Views",
            $"Areas/{areaName}/Models"
        };
    }

    /// <summary>
    /// Generates the files needed for the area migration.
    /// </summary>
    private Dictionary<string, string> GenerateFiles(AreaDefinition area, string rootNamespace)
    {
        var files = new Dictionary<string, string>();
        var areaName = area.Name;

        // Generate _ViewImports.cshtml
        var viewImportsContent = _viewImportsGenerator.GenerateForArea(areaName, rootNamespace);
        files[$"Areas/{areaName}/Views/_ViewImports.cshtml"] = viewImportsContent;

        // Generate _ViewStart.cshtml
        var viewStartContent = GenerateViewStart();
        files[$"Areas/{areaName}/Views/_ViewStart.cshtml"] = viewStartContent;

        return files;
    }

    /// <summary>
    /// Generates _ViewStart.cshtml content.
    /// </summary>
    private static string GenerateViewStart()
    {
        return """
@{
    Layout = "_Layout";
}
""";
    }

    /// <summary>
    /// Generates the route registration code for Program.cs.
    /// </summary>
    private static string GenerateRouteRegistration(AreaDefinition area)
    {
        var sb = new StringBuilder();
        var areaName = area.Name;

        // Generate MapAreaControllerRoute for each route
        if (area.Routes.Count > 0)
        {
            foreach (var route in area.Routes)
            {
                sb.AppendLine($"app.MapAreaControllerRoute(");
                sb.AppendLine($"    name: \"{route.Name}\",");
                sb.AppendLine($"    areaName: \"{areaName}\",");
                sb.AppendLine($"    pattern: \"{route.Template}\"");

                // Add defaults if present
                if (route.Defaults.Count > 0)
                {
                    sb.Append($"    defaults: new {{ ");
                    var defaultPairs = route.Defaults.Select(kvp =>
                    {
                        var value = kvp.Value == RouteDefinition.OptionalParameter
                            ? "(string?)null"
                            : kvp.Value is string strValue
                                ? $"\"{strValue}\""
                                : kvp.Value?.ToString() ?? "null";
                        return $"{kvp.Key} = {value}";
                    });
                    sb.Append(string.Join(", ", defaultPairs));
                    sb.AppendLine(" }");
                }

                sb.AppendLine(");");
                sb.AppendLine();
            }
        }
        else
        {
            // Default route registration if no routes found
            sb.AppendLine($"app.MapAreaControllerRoute(");
            sb.AppendLine($"    name: \"{areaName}_default\",");
            sb.AppendLine($"    areaName: \"{areaName}\",");
            sb.AppendLine($"    pattern: \"{areaName}/{{controller=Home}}/{{action=Index}}/{{id?}}\"");
            sb.AppendLine(");");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Determines if a class is a controller class.
    /// </summary>
    private static bool IsControllerClass(ClassDeclarationSyntax classDecl)
    {
        var className = classDecl.Identifier.Text;

        // Check if it ends with "Controller"
        if (!className.EndsWith("Controller", StringComparison.Ordinal))
        {
            return false;
        }

        // Check if it has a base class (Controller, ControllerBase, etc.)
        return classDecl.BaseList != null && classDecl.BaseList.Types.Count > 0;
    }

    /// <summary>
    /// Checks if a class already has an [Area] attribute.
    /// </summary>
    private static bool HasAreaAttribute(ClassDeclarationSyntax classDecl)
    {
        return classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr =>
            {
                var name = attr.Name.ToString();
                return name.Equals("Area", StringComparison.Ordinal) ||
                       name.Equals("AreaAttribute", StringComparison.Ordinal);
            });
    }

    /// <summary>
    /// Adds [Area("AreaName")] attribute to a class.
    /// </summary>
    private static ClassDeclarationSyntax AddAreaAttributeToClass(ClassDeclarationSyntax classDecl, string areaName)
    {
        // Create [Area("AreaName")] attribute
        var areaArgument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(areaName)));

        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("Area"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(areaArgument)));

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        return classDecl.WithAttributeLists(
            classDecl.AttributeLists.Add(attributeList));
    }

    /// <summary>
    /// Ensures a using directive exists in the compilation unit.
    /// </summary>
    private static SyntaxNode EnsureUsingDirective(SyntaxNode root, string usingNamespace)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        // Check if using already exists
        var hasUsing = compilationUnit.Usings.Any(u =>
            u.Name?.ToString().Equals(usingNamespace, StringComparison.Ordinal) == true);

        if (hasUsing)
        {
            return root;
        }

        // Parse complete using directive to ensure proper spacing
        var usingCode = $"using {usingNamespace};";
        var usingTree = CSharpSyntaxTree.ParseText(usingCode);
        var usingDirective = usingTree.GetRoot()
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .First()
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        return compilationUnit.AddUsings(usingDirective);
    }
}
