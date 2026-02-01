# [TASK-051] Parse [ServiceContract] and [OperationContract] Attributes

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P0 |
| **Estimate** | L |
| **Sprint** | 6 |
| **Agent** | (unassigned) |
| **Started** | - |
| **Completed** | - |

## Dependencies

- **Depends on:** TASK-004 (Solution Parser)
- **Blocks:** TASK-054, TASK-055, TASK-056

---

## Description

Implement Roslyn-based parser to extract WCF service contracts ([ServiceContract]) and operation contracts ([OperationContract]) from .NET Framework projects. This is the foundation for generating modern gRPC and REST API equivalents.

---

## Acceptance Criteria

- [ ] Parse [ServiceContract] attribute with name, namespace, and session requirements
- [ ] Parse [OperationContract] attributes with IsOneWay, Action, ReplyAction
- [ ] Extract method signatures including parameters and return types
- [ ] Handle generic types and complex parameter types
- [ ] Detect async/await patterns (Task<T> return types)
- [ ] Extract XML documentation comments for operations
- [ ] Store parsed contracts in WcfServiceContract model
- [ ] Unit tests with various WCF contract patterns

---

## Technical Notes

### WCF ServiceContract Example

```csharp
namespace LegacyApp.Services
{
    [ServiceContract(Namespace = "http://legacy.company.com/2015")]
    public interface ICustomerService
    {
        /// <summary>
        /// Gets customer by ID
        /// </summary>
        [OperationContract]
        CustomerDto GetCustomer(int customerId);

        /// <summary>
        /// Creates or updates a customer
        /// </summary>
        [OperationContract]
        int SaveCustomer(CustomerDto customer);

        /// <summary>
        /// Searches customers by criteria
        /// </summary>
        [OperationContract]
        CustomerDto[] SearchCustomers(SearchCriteria criteria);

        /// <summary>
        /// One-way operation for notifications
        /// </summary>
        [OperationContract(IsOneWay = true)]
        void NotifyCustomerUpdated(int customerId);
    }
}
```

### Target gRPC Service (.proto)

```protobuf
syntax = "proto3";

package legacyapp.services;

// Gets customer by ID
service CustomerService {
  rpc GetCustomer (GetCustomerRequest) returns (CustomerDto);

  // Creates or updates a customer
  rpc SaveCustomer (SaveCustomerRequest) returns (SaveCustomerResponse);

  // Searches customers by criteria
  rpc SearchCustomers (SearchCustomersRequest) returns (SearchCustomersResponse);

  // One-way operation for notifications (server streaming)
  rpc NotifyCustomerUpdated (NotifyRequest) returns (google.protobuf.Empty);
}

message GetCustomerRequest {
  int32 customer_id = 1;
}

message SaveCustomerRequest {
  CustomerDto customer = 1;
}

message SaveCustomerResponse {
  int32 customer_id = 1;
}

message SearchCustomersRequest {
  SearchCriteria criteria = 1;
}

message SearchCustomersResponse {
  repeated CustomerDto customers = 1;
}

message NotifyRequest {
  int32 customer_id = 1;
}
```

### Target REST API (Minimal API)

```csharp
namespace ModernApp.Api;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .WithOpenApi();

        // GET /api/customers/{customerId}
        group.MapGet("/{customerId:int}", GetCustomer)
            .WithName("GetCustomer")
            .WithSummary("Gets customer by ID")
            .Produces<CustomerDto>()
            .Produces(404);

        // POST /api/customers
        group.MapPost("/", SaveCustomer)
            .WithName("SaveCustomer")
            .WithSummary("Creates or updates a customer")
            .Produces<int>(201)
            .Produces<int>(200);

        // POST /api/customers/search
        group.MapPost("/search", SearchCustomers)
            .WithName("SearchCustomers")
            .WithSummary("Searches customers by criteria")
            .Produces<CustomerDto[]>();

        // POST /api/customers/{customerId}/notify
        group.MapPost("/{customerId:int}/notify", NotifyCustomerUpdated)
            .WithName("NotifyCustomerUpdated")
            .WithSummary("One-way operation for notifications")
            .Produces(202);
    }

    private static async Task<IResult> GetCustomer(
        int customerId,
        ICustomerService service)
    {
        var customer = await service.GetCustomerAsync(customerId);
        return customer is not null
            ? Results.Ok(customer)
            : Results.NotFound();
    }

    private static async Task<IResult> SaveCustomer(
        CustomerDto customer,
        ICustomerService service)
    {
        var id = await service.SaveCustomerAsync(customer);
        return id > 0
            ? Results.Created($"/api/customers/{id}", id)
            : Results.Ok(id);
    }

    private static async Task<IResult> SearchCustomers(
        SearchCriteria criteria,
        ICustomerService service)
    {
        var customers = await service.SearchCustomersAsync(criteria);
        return Results.Ok(customers);
    }

    private static async Task<IResult> NotifyCustomerUpdated(
        int customerId,
        ICustomerService service)
    {
        await service.NotifyCustomerUpdatedAsync(customerId);
        return Results.Accepted();
    }
}
```

### Roslyn Parsing Strategy

```csharp
public class ServiceContractParser
{
    public WcfServiceContract Parse(SemanticModel model, InterfaceDeclarationSyntax syntax)
    {
        var symbol = model.GetDeclaredSymbol(syntax);
        var serviceAttr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "ServiceContractAttribute");

        var contract = new WcfServiceContract
        {
            InterfaceName = symbol.Name,
            Namespace = GetNamespaceValue(serviceAttr),
            Operations = ParseOperations(symbol, model)
        };

        return contract;
    }

    private List<WcfOperation> ParseOperations(INamedTypeSymbol symbol, SemanticModel model)
    {
        return symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "OperationContractAttribute"))
            .Select(m => ParseOperation(m, model))
            .ToList();
    }
}
```

### Model Classes

```csharp
public record WcfServiceContract
{
    public string InterfaceName { get; init; }
    public string Namespace { get; init; }
    public bool SessionRequired { get; init; }
    public List<WcfOperation> Operations { get; init; } = new();
}

public record WcfOperation
{
    public string Name { get; init; }
    public string ReturnType { get; init; }
    public bool IsOneWay { get; init; }
    public bool IsAsync { get; init; }
    public string XmlDocumentation { get; init; }
    public List<WcfParameter> Parameters { get; init; } = new();
}

public record WcfParameter
{
    public string Name { get; init; }
    public string Type { get; init; }
    public bool IsArray { get; init; }
    public bool IsNullable { get; init; }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
