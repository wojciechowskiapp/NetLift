using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Ef;

namespace NetLift.Transforms.Ef.Analyzers;

/// <summary>
/// Detects EF6 DbContext classes in C# source code using Roslyn syntax walking.
/// </summary>
public sealed class DbContextDetector : IDbContextDetector
{
    /// <inheritdoc />
    public IReadOnlyList<DbContextInfo> Detect(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return [];
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        var walker = new DbContextWalker();
        walker.Visit(root);

        return walker.DetectedContexts;
    }

    /// <inheritdoc />
    public bool ContainsDbContext(string sourceCode)
    {
        return Detect(sourceCode).Count > 0;
    }

    /// <summary>
    /// Syntax walker that collects DbContext information from the syntax tree.
    /// </summary>
    private sealed class DbContextWalker : CSharpSyntaxWalker
    {
        private readonly List<DbContextInfo> _detectedContexts = new();

        public IReadOnlyList<DbContextInfo> DetectedContexts => _detectedContexts;

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            // Check if this class inherits from DbContext
            if (node.BaseList != null && InheritsFromDbContext(node.BaseList))
            {
                var contextInfo = AnalyzeDbContext(node);
                _detectedContexts.Add(contextInfo);
            }

            // Continue walking to handle nested classes
            base.VisitClassDeclaration(node);
        }

        /// <summary>
        /// Checks if a base list contains DbContext inheritance.
        /// </summary>
        private static bool InheritsFromDbContext(BaseListSyntax baseList)
        {
            foreach (var baseType in baseList.Types)
            {
                var typeName = ExtractTypeName(baseType.Type);
                if (typeName == "DbContext")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts the type name from a TypeSyntax, handling qualified names.
        /// </summary>
        private static string? ExtractTypeName(TypeSyntax type)
        {
            return type switch
            {
                IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
                QualifiedNameSyntax qualifiedName => GetRightmostIdentifier(qualifiedName),
                GenericNameSyntax genericName => genericName.Identifier.Text,
                _ => null
            };
        }

        /// <summary>
        /// Gets the rightmost identifier from a qualified name.
        /// Example: System.Data.Entity.DbContext -> DbContext
        /// </summary>
        private static string GetRightmostIdentifier(QualifiedNameSyntax qualifiedName)
        {
            return qualifiedName.Right.Identifier.Text;
        }

        /// <summary>
        /// Analyzes a DbContext class declaration and extracts its information.
        /// </summary>
        private static DbContextInfo AnalyzeDbContext(ClassDeclarationSyntax node)
        {
            var className = node.Identifier.Text;
            var namespaceName = GetNamespace(node);
            var dbSets = GetDbSets(node);
            var constructors = GetConstructors(node);
            var hasOnModelCreating = HasOnModelCreatingMethod(node);

            // Determine if using connection string name from constructors
            var usesConnectionStringName = false;
            string? connectionStringName = null;

            foreach (var ctor in constructors)
            {
                if (ctor.HasBaseCall && !string.IsNullOrEmpty(ctor.BaseCallArgument))
                {
                    // Check if it's a "name=..." pattern
                    var arg = ctor.BaseCallArgument;
                    if (arg.StartsWith("\"name=", StringComparison.Ordinal) && arg.EndsWith("\"", StringComparison.Ordinal))
                    {
                        usesConnectionStringName = true;
                        // Extract the connection string name from "name=ConnectionStringName"
                        connectionStringName = arg.Substring(6, arg.Length - 7); // Remove "name= and trailing "
                        break;
                    }
                    else if (arg.StartsWith("\"", StringComparison.Ordinal) && arg.EndsWith("\"", StringComparison.Ordinal))
                    {
                        // It's a quoted string - might be a direct connection string name
                        usesConnectionStringName = true;
                        connectionStringName = arg.Substring(1, arg.Length - 2); // Remove quotes
                        break;
                    }
                }
            }

            return new DbContextInfo
            {
                ClassName = className,
                Namespace = namespaceName,
                DbSets = dbSets,
                Constructors = constructors,
                HasOnModelCreating = hasOnModelCreating,
                UsesConnectionStringName = usesConnectionStringName,
                ConnectionStringName = connectionStringName
            };
        }

        /// <summary>
        /// Gets the namespace containing the class.
        /// </summary>
        private static string GetNamespace(ClassDeclarationSyntax node)
        {
            // Walk up the tree to find the namespace
            var parent = node.Parent;
            while (parent != null)
            {
                if (parent is NamespaceDeclarationSyntax namespaceDecl)
                {
                    return namespaceDecl.Name.ToString();
                }
                if (parent is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
                {
                    return fileScopedNamespace.Name.ToString();
                }
                parent = parent.Parent;
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets all DbSet properties from the DbContext class.
        /// </summary>
        private static IReadOnlyList<DbSetInfo> GetDbSets(ClassDeclarationSyntax node)
        {
            var dbSets = new List<DbSetInfo>();

            foreach (var member in node.Members)
            {
                if (member is PropertyDeclarationSyntax property)
                {
                    // Check if the property type is DbSet<T>
                    if (property.Type is GenericNameSyntax genericType &&
                        genericType.Identifier.Text == "DbSet")
                    {
                        var typeArgs = genericType.TypeArgumentList.Arguments;
                        if (typeArgs.Count == 1)
                        {
                            var entityTypeName = typeArgs[0].ToString();
                            var propertyName = property.Identifier.Text;
                            dbSets.Add(new DbSetInfo(propertyName, entityTypeName));
                        }
                    }
                }
            }

            return dbSets;
        }

        /// <summary>
        /// Gets all constructors from the DbContext class.
        /// </summary>
        private static IReadOnlyList<Core.Models.Ef.ConstructorInfo> GetConstructors(ClassDeclarationSyntax node)
        {
            var constructors = new List<Core.Models.Ef.ConstructorInfo>();

            foreach (var member in node.Members)
            {
                if (member is ConstructorDeclarationSyntax ctor)
                {
                    var parameters = ctor.ParameterList.Parameters
                        .Select(p => new Core.Models.Ef.ParameterInfo(
                            p.Identifier.Text,
                            p.Type?.ToString() ?? string.Empty))
                        .ToList();

                    var hasBaseCall = false;
                    string? baseCallArgument = null;

                    // Check for base() call in constructor initializer
                    if (ctor.Initializer?.Kind() == SyntaxKind.BaseConstructorInitializer)
                    {
                        hasBaseCall = true;
                        // Get the first argument if any
                        if (ctor.Initializer.ArgumentList.Arguments.Count > 0)
                        {
                            var arg = ctor.Initializer.ArgumentList.Arguments[0];
                            baseCallArgument = arg.Expression.ToString();
                        }
                    }

                    constructors.Add(new Core.Models.Ef.ConstructorInfo(
                        parameters,
                        hasBaseCall,
                        baseCallArgument));
                }
            }

            return constructors;
        }

        /// <summary>
        /// Checks if the DbContext class has an OnModelCreating method override.
        /// </summary>
        private static bool HasOnModelCreatingMethod(ClassDeclarationSyntax node)
        {
            foreach (var member in node.Members)
            {
                if (member is MethodDeclarationSyntax method &&
                    method.Identifier.Text == "OnModelCreating")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
