using System.Text;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Wcf;

namespace NetLift.Transforms.Wcf.Generators;

/// <summary>
/// Generates ASP.NET Core REST API controllers from WCF service contracts.
/// </summary>
public sealed class RestControllerGenerator : IRestControllerGenerator
{
    private readonly List<string> _diagnostics = [];
    private int _confidenceScore = 100;

    /// <inheritdoc />
    public int ConfidenceScore => _confidenceScore;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Diagnostics => _diagnostics.AsReadOnly();

    /// <inheritdoc />
    public RestControllerInfo Generate(WcfServiceContract serviceContract, string targetNamespace)
    {
        if (serviceContract == null)
            throw new ArgumentNullException(nameof(serviceContract));
        if (string.IsNullOrWhiteSpace(targetNamespace))
            throw new ArgumentException("Target namespace cannot be null or empty.", nameof(targetNamespace));

        // Reset state
        _diagnostics.Clear();
        _confidenceScore = 100;

        // Derive controller name from service interface name
        var controllerName = DeriveControllerName(serviceContract.InterfaceName);
        var routePrefix = DeriveRoutePrefix(controllerName);

        // Generate actions
        var actions = new List<RestActionInfo>();
        foreach (var operation in serviceContract.Operations)
        {
            var action = GenerateAction(operation);
            actions.Add(action);
        }

        // Generate complete controller code
        var controllerCode = GenerateControllerCode(
            targetNamespace,
            controllerName,
            routePrefix,
            actions,
            serviceContract);

        return new RestControllerInfo
        {
            ClassName = controllerName,
            Namespace = targetNamespace,
            ControllerCode = controllerCode,
            RoutePrefix = routePrefix,
            Actions = actions
        };
    }

    private string DeriveControllerName(string interfaceName)
    {
        // Remove leading 'I' from interface name (e.g., ICustomerService -> CustomerService)
        var serviceName = interfaceName.StartsWith('I') && interfaceName.Length > 1
            ? interfaceName.Substring(1)
            : interfaceName;

        // Remove 'Service' suffix if present (e.g., CustomerService -> Customer)
        if (serviceName.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
        {
            serviceName = serviceName.Substring(0, serviceName.Length - 7);
        }

        // Add 'Controller' suffix
        return $"{serviceName}Controller";
    }

    private string DeriveRoutePrefix(string controllerName)
    {
        // Remove 'Controller' suffix
        var name = controllerName.EndsWith("Controller")
            ? controllerName.Substring(0, controllerName.Length - 10)
            : controllerName;

        // Convert to lowercase and pluralize if needed
        // Simple pluralization: add 's' if not already plural
        var route = name.ToLowerInvariant();
        if (!route.EndsWith('s'))
        {
            route += 's';
        }

        return $"api/{route}";
    }

    private RestActionInfo GenerateAction(WcfOperation operation)
    {
        // Determine HTTP method based on operation name
        var httpMethod = DetermineHttpMethod(operation.Name);

        // Generate route template
        var route = GenerateRouteTemplate(operation, httpMethod);

        // Determine return type
        var returnType = DetermineReturnType(operation);

        // Generate parameters
        var parameters = GenerateParameters(operation, httpMethod);

        return new RestActionInfo
        {
            Name = operation.Name,
            HttpMethod = httpMethod,
            Route = route,
            ReturnType = returnType,
            Documentation = operation.XmlDocumentation,
            Parameters = parameters
        };
    }

    private string DetermineHttpMethod(string operationName)
    {
        var lowerName = operationName.ToLowerInvariant();

        // GET: Get*, Find*, List*, Retrieve*, Fetch*
        if (lowerName.StartsWith("get") ||
            lowerName.StartsWith("find") ||
            lowerName.StartsWith("list") ||
            lowerName.StartsWith("retrieve") ||
            lowerName.StartsWith("fetch"))
        {
            return "GET";
        }

        // DELETE: Delete*, Remove*
        if (lowerName.StartsWith("delete") || lowerName.StartsWith("remove"))
        {
            return "DELETE";
        }

        // PUT: Update*, Modify*, Edit*
        if (lowerName.StartsWith("update") ||
            lowerName.StartsWith("modify") ||
            lowerName.StartsWith("edit"))
        {
            return "PUT";
        }

        // POST with special route: Search*
        if (lowerName.StartsWith("search"))
        {
            return "POST"; // Search operations use POST with request body
        }

        // POST: Create*, Add*, Save*, Insert*
        if (lowerName.StartsWith("create") ||
            lowerName.StartsWith("add") ||
            lowerName.StartsWith("save") ||
            lowerName.StartsWith("insert"))
        {
            return "POST";
        }

        // Default to POST for unknown operations
        _diagnostics.Add($"Operation '{operationName}' does not follow standard naming conventions. Defaulting to POST.");
        _confidenceScore = Math.Min(_confidenceScore, 85);
        return "POST";
    }

    private string GenerateRouteTemplate(WcfOperation operation, string httpMethod)
    {
        var lowerName = operation.Name.ToLowerInvariant();

        // Special case: Search operations
        if (lowerName.StartsWith("search"))
        {
            return "search";
        }

        // For GET, DELETE, PUT operations with a single ID parameter, include it in the route
        if (httpMethod is "GET" or "DELETE" or "PUT")
        {
            var idParam = FindIdParameter(operation.Parameters);
            if (idParam != null)
            {
                var paramType = GetRouteConstraint(idParam.Type);
                return $"{{{idParam.Name}{paramType}}}";
            }
        }

        // For operations with multiple parameters on GET, use query strings (no route template)
        if (httpMethod == "GET" && operation.Parameters.Count > 1)
        {
            return "";
        }

        // Default: no specific route (uses controller route)
        return "";
    }

    private WcfParameter? FindIdParameter(IReadOnlyList<WcfParameter> parameters)
    {
        // Look for common ID parameter names
        var idParam = parameters.FirstOrDefault(p =>
            p.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        // If found, check if it's a simple type (int, long, guid, string)
        if (idParam != null && IsSimpleType(idParam.Type))
        {
            return idParam;
        }

        // If only one parameter and it's simple, treat it as ID
        if (parameters.Count == 1 && IsSimpleType(parameters[0].Type))
        {
            return parameters[0];
        }

        return null;
    }

    private bool IsSimpleType(string typeName)
    {
        var cleanType = typeName.Replace("?", "").Split('.').Last();
        return cleanType switch
        {
            "Int32" or "int" or "Int64" or "long" or "Guid" or "String" or "string" => true,
            _ => false
        };
    }

    private string GetRouteConstraint(string typeName)
    {
        var cleanType = typeName.Replace("?", "").Split('.').Last();
        return cleanType switch
        {
            "Int32" or "int" => ":int",
            "Int64" or "long" => ":long",
            "Guid" => ":guid",
            _ => ""
        };
    }

    private string DetermineReturnType(WcfOperation operation)
    {
        var returnType = operation.ReturnType;

        // Handle void
        if (returnType == "void")
        {
            return "void";
        }

        // Strip Task<T> wrapper
        if (returnType.StartsWith("Task<") && returnType.EndsWith(">"))
        {
            returnType = returnType.Substring(5, returnType.Length - 6);
        }
        else if (returnType == "Task")
        {
            return "void";
        }

        return returnType;
    }

    private List<RestParameterInfo> GenerateParameters(WcfOperation operation, string httpMethod)
    {
        var parameters = new List<RestParameterInfo>();

        foreach (var param in operation.Parameters)
        {
            var source = DetermineParameterSource(param, operation, httpMethod);
            parameters.Add(new RestParameterInfo
            {
                Name = param.Name,
                Type = param.Type,
                Source = source
            });
        }

        return parameters;
    }

    private string DetermineParameterSource(WcfParameter parameter, WcfOperation operation, string httpMethod)
    {
        // For GET and DELETE, prefer route/query parameters
        if (httpMethod is "GET" or "DELETE")
        {
            // If it's the ID parameter in the route, use FromRoute
            var idParam = FindIdParameter(operation.Parameters);
            if (idParam != null && parameter.Name == idParam.Name)
            {
                return "FromRoute";
            }

            // Otherwise use FromQuery
            return "FromQuery";
        }

        // For POST and PUT, complex types come from body, simple types from route/query
        if (httpMethod is "POST" or "PUT")
        {
            // Check if this is a route parameter
            var idParam = FindIdParameter(operation.Parameters);
            if (idParam != null && parameter.Name == idParam.Name && httpMethod == "PUT")
            {
                return "FromRoute";
            }

            // Complex types (non-primitive) go in the body
            if (!IsSimpleType(parameter.Type))
            {
                return "FromBody";
            }

            // Simple types can be query parameters
            return "FromQuery";
        }

        return "FromQuery";
    }

    private string GenerateControllerCode(
        string targetNamespace,
        string controllerName,
        string routePrefix,
        List<RestActionInfo> actions,
        WcfServiceContract serviceContract)
    {
        var sb = new StringBuilder();

        // Using directives
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine();

        // Controller class documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// REST API controller generated from WCF service contract {serviceContract.InterfaceName}.");
        sb.AppendLine("/// </summary>");

        // Controller attributes
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{routePrefix}\")]");
        sb.AppendLine("[Produces(\"application/json\")]");

        // Class declaration
        sb.AppendLine($"public class {controllerName} : ControllerBase");
        sb.AppendLine("{");

        // Logger field
        sb.AppendLine($"    private readonly ILogger<{controllerName}> _logger;");
        sb.AppendLine();

        // Constructor
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the <see cref=\"{controllerName}\"/> class.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public {controllerName}(ILogger<{controllerName}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        _logger = logger ?? throw new ArgumentNullException(nameof(logger));");
        sb.AppendLine("        // TODO: Inject business services via DI");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate action methods
        foreach (var action in actions)
        {
            GenerateActionMethod(sb, action);
        }

        // Close class
        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateActionMethod(StringBuilder sb, RestActionInfo action)
    {
        // XML documentation
        if (!string.IsNullOrWhiteSpace(action.Documentation))
        {
            sb.AppendLine("    /// <summary>");
            foreach (var line in action.Documentation.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    sb.AppendLine($"    /// {trimmed}");
                }
            }
            sb.AppendLine("    /// </summary>");
        }
        else
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// {action.Name}");
            sb.AppendLine("    /// </summary>");
        }

        // Parameter documentation
        foreach (var param in action.Parameters)
        {
            sb.AppendLine($"    /// <param name=\"{param.Name}\">The {param.Name}.</param>");
        }

        // HTTP method attribute
        var httpAttribute = action.HttpMethod switch
        {
            "GET" => "HttpGet",
            "POST" => "HttpPost",
            "PUT" => "HttpPut",
            "DELETE" => "HttpDelete",
            _ => "HttpPost"
        };

        if (!string.IsNullOrWhiteSpace(action.Route))
        {
            sb.AppendLine($"    [{httpAttribute}(\"{action.Route}\")]");
        }
        else
        {
            sb.AppendLine($"    [{httpAttribute}]");
        }

        // ProducesResponseType attributes
        if (action.ReturnType != "void")
        {
            sb.AppendLine($"    [ProducesResponseType(typeof({action.ReturnType}), StatusCodes.Status200OK)]");
            sb.AppendLine("    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]");

            if (action.HttpMethod == "GET")
            {
                sb.AppendLine("    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]");
            }
        }
        else
        {
            sb.AppendLine("    [ProducesResponseType(StatusCodes.Status200OK)]");
            sb.AppendLine("    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]");
        }

        // Method signature
        var returnTypeSignature = action.ReturnType != "void"
            ? $"ActionResult<{action.ReturnType}>"
            : "IActionResult";

        sb.Append($"    public async Task<{returnTypeSignature}> {action.Name}(");

        // Parameters
        var paramStrings = new List<string>();
        foreach (var param in action.Parameters)
        {
            var attributes = param.Source switch
            {
                "FromRoute" => "[FromRoute] ",
                "FromQuery" => "[FromQuery] ",
                "FromBody" => "[FromBody] ",
                _ => ""
            };
            paramStrings.Add($"{attributes}{param.Type} {param.Name}");
        }
        sb.Append(string.Join(", ", paramStrings));
        sb.AppendLine(")");

        // Method body
        sb.AppendLine("    {");
        sb.AppendLine($"        _logger.LogInformation(\"Executing {action.Name}\");");
        sb.AppendLine();
        sb.AppendLine("        // TODO: Implement business logic");
        sb.AppendLine("        // TODO: Call appropriate service layer");
        sb.AppendLine();

        if (action.ReturnType != "void")
        {
            if (action.HttpMethod == "GET")
            {
                sb.AppendLine("        // TODO: Retrieve and return data");
                sb.AppendLine("        return NotFound(new ProblemDetails");
                sb.AppendLine("        {");
                sb.AppendLine("            Title = \"Not Found\",");
                sb.AppendLine($"            Detail = \"The requested resource was not found.\"");
                sb.AppendLine("        });");
            }
            else
            {
                sb.AppendLine("        // TODO: Process request and return result");
                sb.AppendLine($"        throw new NotImplementedException(\"Business logic for {action.Name} not yet implemented.\");");
            }
        }
        else
        {
            sb.AppendLine("        // TODO: Process request");
            sb.AppendLine("        return Ok();");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
