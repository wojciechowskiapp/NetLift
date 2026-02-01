using System.Text;
using NetLift.Core.Interfaces;
using NetLift.Core.Models.Wcf;

namespace NetLift.Transforms.Wcf.Analyzers;

/// <summary>
/// Transforms WCF FaultContracts to custom exceptions with gRPC and REST error handling.
/// Generates exception classes, gRPC interceptor, and ProblemDetails exception handler.
/// </summary>
public sealed class FaultContractTransformer : IFaultContractTransformer
{
    private readonly List<string> _diagnostics = new();
    private int _confidenceScore = 100;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Diagnostics => _diagnostics.AsReadOnly();

    /// <inheritdoc />
    public int ConfidenceScore => _confidenceScore;

    /// <inheritdoc />
    public FaultTransformResult Transform(IReadOnlyList<WcfDataContract> faultContracts)
    {
        _diagnostics.Clear();
        _confidenceScore = 100;

        if (faultContracts == null || faultContracts.Count == 0)
        {
            _diagnostics.Add("No fault contracts provided");
            return new FaultTransformResult
            {
                Exceptions = [],
                InterceptorCode = string.Empty,
                ExceptionHandlerCode = string.Empty,
                ExceptionClassesCode = string.Empty
            };
        }

        try
        {
            var exceptions = new List<FaultContractInfo>();

            // Transform each fault contract to exception info
            foreach (var fault in faultContracts)
            {
                if (!IsFaultContract(fault))
                {
                    _diagnostics.Add($"Skipping '{fault.TypeName}' - does not appear to be a fault contract");
                    continue;
                }

                var exceptionInfo = TransformFault(fault);
                exceptions.Add(exceptionInfo);
                _diagnostics.Add($"Transformed fault contract '{fault.TypeName}' to exception '{exceptionInfo.ExceptionClassName}'");
            }

            if (exceptions.Count == 0)
            {
                _diagnostics.Add("No valid fault contracts found to transform");
                _confidenceScore = 50;
            }

            // Generate code artifacts
            var exceptionClassesCode = GenerateExceptionClasses(exceptions);
            var interceptorCode = GenerateGrpcInterceptor(exceptions);
            var exceptionHandlerCode = GenerateRestExceptionHandler(exceptions);

            return new FaultTransformResult
            {
                Exceptions = exceptions,
                InterceptorCode = interceptorCode,
                ExceptionHandlerCode = exceptionHandlerCode,
                ExceptionClassesCode = exceptionClassesCode
            };
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"Error transforming fault contracts: {ex.Message}");
            _confidenceScore = 0;
            throw;
        }
    }

    /// <summary>
    /// Checks if a data contract represents a fault (typically ends with "Fault").
    /// </summary>
    private static bool IsFaultContract(WcfDataContract contract)
    {
        return contract.TypeName.EndsWith("Fault", StringComparison.OrdinalIgnoreCase) ||
               contract.TypeName.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
               contract.TypeName.Contains("Exception", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Transforms a single fault contract to exception info.
    /// </summary>
    private FaultContractInfo TransformFault(WcfDataContract fault)
    {
        var exceptionName = GenerateExceptionClassName(fault.TypeName);
        var (grpcStatus, httpStatus) = DetermineStatusCodes(fault.TypeName);
        var exceptionCode = GenerateExceptionCode(fault.TypeName);

        var properties = fault.Properties.Select(p => new FaultProperty
        {
            Name = p.Name,
            Type = p.Type
        }).ToList();

        return new FaultContractInfo
        {
            FaultTypeName = fault.TypeName,
            ExceptionClassName = exceptionName,
            ExceptionCode = exceptionCode,
            GrpcStatus = grpcStatus,
            HttpStatusCode = httpStatus,
            Properties = properties
        };
    }

    /// <summary>
    /// Generates exception class name from fault type name.
    /// E.g., "CustomerNotFoundFault" -> "CustomerNotFoundException"
    /// </summary>
    private static string GenerateExceptionClassName(string faultTypeName)
    {
        var name = faultTypeName;

        // Remove "Fault" suffix
        if (name.EndsWith("Fault", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 5);
        }

        // Remove "Error" suffix
        if (name.EndsWith("Error", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 5);
        }

        // Add "Exception" suffix if not present
        if (!name.EndsWith("Exception", StringComparison.OrdinalIgnoreCase))
        {
            name += "Exception";
        }

        return name;
    }

    /// <summary>
    /// Generates exception code from fault type name.
    /// E.g., "CustomerNotFoundFault" -> "CUSTOMER_NOT_FOUND"
    /// </summary>
    private static string GenerateExceptionCode(string faultTypeName)
    {
        var name = faultTypeName;

        // Remove "Fault" suffix
        if (name.EndsWith("Fault", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 5);
        }

        // Convert PascalCase to UPPER_SNAKE_CASE
        var result = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0 && (i + 1 < name.Length && char.IsLower(name[i + 1]) || char.IsLower(name[i - 1])))
            {
                result.Append('_');
            }
            result.Append(char.ToUpperInvariant(c));
        }

        return result.ToString();
    }

    /// <summary>
    /// Determines gRPC and HTTP status codes based on fault type name pattern.
    /// </summary>
    private (GrpcStatusCode grpc, int http) DetermineStatusCodes(string faultTypeName)
    {
        var lowerName = faultTypeName.ToLowerInvariant();

        // Pattern matching based on fault name
        if (lowerName.Contains("notfound") || lowerName.Contains("not_found"))
        {
            return (GrpcStatusCode.NotFound, 404);
        }

        if (lowerName.Contains("validation") || lowerName.Contains("invalid") || lowerName.Contains("badrequest"))
        {
            return (GrpcStatusCode.InvalidArgument, 400);
        }

        if (lowerName.Contains("authorization") || lowerName.Contains("forbidden") || lowerName.Contains("permission"))
        {
            return (GrpcStatusCode.PermissionDenied, 403);
        }

        if (lowerName.Contains("authentication") || lowerName.Contains("unauthenticated") || lowerName.Contains("unauthorized"))
        {
            return (GrpcStatusCode.Unauthenticated, 401);
        }

        if (lowerName.Contains("conflict") || lowerName.Contains("duplicate") || lowerName.Contains("alreadyexists"))
        {
            return (GrpcStatusCode.AlreadyExists, 409);
        }

        if (lowerName.Contains("cancelled") || lowerName.Contains("timeout"))
        {
            return (GrpcStatusCode.Cancelled, 408);
        }

        // Default to Internal Server Error
        return (GrpcStatusCode.Internal, 500);
    }

    /// <summary>
    /// Generates custom exception classes code.
    /// </summary>
    private string GenerateExceptionClasses(List<FaultContractInfo> exceptions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("namespace YourNamespace.Exceptions;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Base exception for all custom domain exceptions.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public abstract class DomainException : Exception");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the error code for this exception.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public string ErrorCode { get; }");
        sb.AppendLine();
        sb.AppendLine("    protected DomainException(string errorCode, string message) : base(message)");
        sb.AppendLine("    {");
        sb.AppendLine("        ErrorCode = errorCode;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected DomainException(string errorCode, string message, Exception innerException)");
        sb.AppendLine("        : base(message, innerException)");
        sb.AppendLine("    {");
        sb.AppendLine("        ErrorCode = errorCode;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        foreach (var exception in exceptions)
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// Exception generated from WCF FaultContract '{exception.FaultTypeName}'.");
            sb.AppendLine($"/// gRPC: {exception.GrpcStatus}, HTTP: {exception.HttpStatusCode}");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public sealed class {exception.ExceptionClassName} : DomainException");
            sb.AppendLine("{");

            // Generate properties
            foreach (var prop in exception.Properties)
            {
                sb.AppendLine($"    public {prop.Type} {prop.Name} {{ get; }}");
                sb.AppendLine();
            }

            // Generate constructor
            sb.Append("    public ").Append(exception.ExceptionClassName).Append("(string message");
            foreach (var prop in exception.Properties)
            {
                sb.Append($", {prop.Type} {ToCamelCase(prop.Name)}");
            }
            sb.AppendLine(")");
            sb.AppendLine($"        : base(\"{exception.ExceptionCode}\", message)");
            sb.AppendLine("    {");
            foreach (var prop in exception.Properties)
            {
                sb.AppendLine($"        {prop.Name} = {ToCamelCase(prop.Name)};");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates gRPC interceptor code for exception handling.
    /// </summary>
    private string GenerateGrpcInterceptor(List<FaultContractInfo> exceptions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Grpc.Core;");
        sb.AppendLine("using Grpc.Core.Interceptors;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using YourNamespace.Exceptions;");
        sb.AppendLine();
        sb.AppendLine("namespace YourNamespace.Grpc.Interceptors;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// gRPC interceptor that maps custom domain exceptions to appropriate gRPC status codes.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class ExceptionInterceptor : Interceptor");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly ILogger<ExceptionInterceptor> _logger;");
        sb.AppendLine();
        sb.AppendLine("    public ExceptionInterceptor(ILogger<ExceptionInterceptor> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(");
        sb.AppendLine("        TRequest request,");
        sb.AppendLine("        ServerCallContext context,");
        sb.AppendLine("        UnaryServerMethod<TRequest, TResponse> continuation)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            return await continuation(request, context);");
        sb.AppendLine("        }");

        foreach (var exception in exceptions)
        {
            sb.AppendLine($"        catch ({exception.ExceptionClassName} ex)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _logger.LogWarning(ex, \"{{ExceptionType}} thrown: {{Message}}\", \"{exception.ExceptionClassName}\", ex.Message);");
            sb.AppendLine($"            throw new RpcException(new Status(StatusCode.{exception.GrpcStatus}, ex.Message));");
            sb.AppendLine("        }");
        }

        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            _logger.LogError(ex, \"Unhandled exception in gRPC call\");");
        sb.AppendLine("            throw new RpcException(new Status(StatusCode.Internal, \"An internal error occurred\"));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates REST exception handler code using ProblemDetails (RFC 7807).
    /// </summary>
    private string GenerateRestExceptionHandler(List<FaultContractInfo> exceptions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Net;");
        sb.AppendLine("using Microsoft.AspNetCore.Diagnostics;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using YourNamespace.Exceptions;");
        sb.AppendLine();
        sb.AppendLine("namespace YourNamespace.Api.Middleware;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Global exception handler that maps domain exceptions to ProblemDetails (RFC 7807).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public class GlobalExceptionHandler : IExceptionHandler");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly ILogger<GlobalExceptionHandler> _logger;");
        sb.AppendLine();
        sb.AppendLine("    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public async ValueTask<bool> TryHandleAsync(");
        sb.AppendLine("        HttpContext httpContext,");
        sb.AppendLine("        Exception exception,");
        sb.AppendLine("        CancellationToken cancellationToken)");
        sb.AppendLine("    {");
        sb.AppendLine("        var problemDetails = exception switch");
        sb.AppendLine("        {");

        foreach (var exception in exceptions)
        {
            sb.AppendLine($"            {exception.ExceptionClassName} ex => new ProblemDetails");
            sb.AppendLine("            {");
            sb.AppendLine($"                Status = {exception.HttpStatusCode},");
            sb.AppendLine("                Title = ex.Message,");
            sb.AppendLine($"                Type = \"https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.{GetRfcSection(exception.HttpStatusCode)}\",");
            sb.AppendLine("                Detail = ex.Message,");
            sb.AppendLine("                Extensions = { [\"errorCode\"] = ex.ErrorCode }");
            sb.AppendLine("            },");
        }

        sb.AppendLine("            DomainException ex => new ProblemDetails");
        sb.AppendLine("            {");
        sb.AppendLine("                Status = 500,");
        sb.AppendLine("                Title = \"An error occurred\",");
        sb.AppendLine("                Type = \"https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1\",");
        sb.AppendLine("                Detail = ex.Message,");
        sb.AppendLine("                Extensions = { [\"errorCode\"] = ex.ErrorCode }");
        sb.AppendLine("            },");
        sb.AppendLine("            _ => new ProblemDetails");
        sb.AppendLine("            {");
        sb.AppendLine("                Status = 500,");
        sb.AppendLine("                Title = \"An internal error occurred\",");
        sb.AppendLine("                Type = \"https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1\"");
        sb.AppendLine("            }");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        if (problemDetails.Status >= 500)");
        sb.AppendLine("        {");
        sb.AppendLine("            _logger.LogError(exception, \"An error occurred: {Message}\", exception.Message);");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            _logger.LogWarning(\"Client error: {Message}\", exception.Message);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        httpContext.Response.StatusCode = problemDetails.Status ?? 500;");
        sb.AppendLine("        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);");
        sb.AppendLine();
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Gets RFC 7231 section number based on HTTP status code.
    /// </summary>
    private static string GetRfcSection(int statusCode)
    {
        return statusCode switch
        {
            400 => "1", // Bad Request
            401 => "2", // Unauthorized
            403 => "3", // Forbidden
            404 => "4", // Not Found
            408 => "7", // Request Timeout
            409 => "8", // Conflict
            _ => "1"
        };
    }

    /// <summary>
    /// Converts a string to camelCase.
    /// </summary>
    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
