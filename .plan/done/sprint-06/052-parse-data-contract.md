# [TASK-052] Parse [DataContract] and [DataMember] Attributes

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
- **Blocks:** TASK-054 (Generate .proto)

---

## Description

Implement Roslyn-based parser to extract WCF data contracts ([DataContract]) and data members ([DataMember]) for conversion to gRPC messages and REST DTOs. Handle complex scenarios like inheritance, enums, and collections.

---

## Acceptance Criteria

- [ ] Parse [DataContract] attributes with name and namespace
- [ ] Parse [DataMember] attributes with Order, IsRequired, EmitDefaultValue
- [ ] Extract property types including primitives, complex types, collections
- [ ] Handle inheritance hierarchies and base contracts
- [ ] Parse enum types with [EnumMember] attributes
- [ ] Detect nullable reference types and value types
- [ ] Store parsed contracts in WcfDataContract model
- [ ] Unit tests with complex data contract scenarios

---

## Technical Notes

### WCF DataContract Example

```csharp
namespace LegacyApp.Contracts
{
    [DataContract(Namespace = "http://legacy.company.com/2015")]
    public class CustomerDto
    {
        [DataMember(Order = 1, IsRequired = true)]
        public int CustomerId { get; set; }

        [DataMember(Order = 2, IsRequired = true)]
        public string FirstName { get; set; }

        [DataMember(Order = 3, IsRequired = true)]
        public string LastName { get; set; }

        [DataMember(Order = 4)]
        public string Email { get; set; }

        [DataMember(Order = 5)]
        public DateTime? BirthDate { get; set; }

        [DataMember(Order = 6)]
        public CustomerStatus Status { get; set; }

        [DataMember(Order = 7)]
        public AddressDto[] Addresses { get; set; }

        [DataMember(Order = 8)]
        public decimal TotalPurchases { get; set; }
    }

    [DataContract]
    public class AddressDto
    {
        [DataMember(Order = 1)]
        public string Street { get; set; }

        [DataMember(Order = 2)]
        public string City { get; set; }

        [DataMember(Order = 3)]
        public string PostalCode { get; set; }

        [DataMember(Order = 4)]
        public string Country { get; set; }
    }

    [DataContract]
    public enum CustomerStatus
    {
        [EnumMember]
        Active = 0,

        [EnumMember]
        Inactive = 1,

        [EnumMember]
        Suspended = 2,

        [EnumMember]
        Deleted = 3
    }

    [DataContract]
    public class SearchCriteria
    {
        [DataMember]
        public string NamePattern { get; set; }

        [DataMember]
        public CustomerStatus? Status { get; set; }

        [DataMember]
        public int? MinPurchases { get; set; }

        [DataMember]
        public int PageSize { get; set; } = 20;

        [DataMember]
        public int PageNumber { get; set; } = 1;
    }
}
```

### Target gRPC Messages (.proto)

```protobuf
syntax = "proto3";

package legacyapp.contracts;

import "google/protobuf/timestamp.proto";
import "google/protobuf/wrappers.proto";

message CustomerDto {
  int32 customer_id = 1;
  string first_name = 2;
  string last_name = 3;
  string email = 4;
  google.protobuf.Timestamp birth_date = 5;
  CustomerStatus status = 6;
  repeated AddressDto addresses = 7;
  double total_purchases = 8;
}

message AddressDto {
  string street = 1;
  string city = 2;
  string postal_code = 3;
  string country = 4;
}

enum CustomerStatus {
  CUSTOMER_STATUS_ACTIVE = 0;
  CUSTOMER_STATUS_INACTIVE = 1;
  CUSTOMER_STATUS_SUSPENDED = 2;
  CUSTOMER_STATUS_DELETED = 3;
}

message SearchCriteria {
  string name_pattern = 1;
  google.protobuf.Int32Value status = 2;
  google.protobuf.Int32Value min_purchases = 3;
  int32 page_size = 4;
  int32 page_number = 5;
}
```

### Target REST DTO (C# Records)

```csharp
namespace ModernApp.Contracts;

/// <summary>
/// Customer data transfer object
/// </summary>
public record CustomerDto
{
    public required int CustomerId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; init; }
    public DateTime? BirthDate { get; init; }
    public CustomerStatus Status { get; init; }
    public AddressDto[]? Addresses { get; init; }
    public decimal TotalPurchases { get; init; }
}

public record AddressDto
{
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

public enum CustomerStatus
{
    Active = 0,
    Inactive = 1,
    Suspended = 2,
    Deleted = 3
}

public record SearchCriteria
{
    public string? NamePattern { get; init; }
    public CustomerStatus? Status { get; init; }
    public int? MinPurchases { get; init; }
    public int PageSize { get; init; } = 20;
    public int PageNumber { get; init; } = 1;
}
```

### Roslyn Parsing Strategy

```csharp
public class DataContractParser
{
    public WcfDataContract ParseClass(SemanticModel model, ClassDeclarationSyntax syntax)
    {
        var symbol = model.GetDeclaredSymbol(syntax);
        var dataContractAttr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "DataContractAttribute");

        if (dataContractAttr == null)
            return null;

        return new WcfDataContract
        {
            ClassName = symbol.Name,
            Namespace = GetNamespaceValue(dataContractAttr),
            IsClass = true,
            Properties = ParseDataMembers(symbol),
            BaseType = symbol.BaseType?.Name
        };
    }

    public WcfDataContract ParseEnum(SemanticModel model, EnumDeclarationSyntax syntax)
    {
        var symbol = model.GetDeclaredSymbol(syntax);
        var dataContractAttr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "DataContractAttribute");

        return new WcfDataContract
        {
            ClassName = symbol.Name,
            Namespace = GetNamespaceValue(dataContractAttr),
            IsEnum = true,
            EnumMembers = ParseEnumMembers(symbol)
        };
    }

    private List<WcfDataMember> ParseDataMembers(INamedTypeSymbol symbol)
    {
        return symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Select(p => ParseDataMember(p))
            .Where(m => m != null)
            .OrderBy(m => m.Order)
            .ToList();
    }

    private WcfDataMember ParseDataMember(IPropertySymbol property)
    {
        var attr = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "DataMemberAttribute");

        if (attr == null)
            return null;

        return new WcfDataMember
        {
            Name = property.Name,
            Type = property.Type.ToDisplayString(),
            Order = GetOrderValue(attr),
            IsRequired = GetIsRequiredValue(attr),
            IsNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated,
            IsCollection = IsCollectionType(property.Type)
        };
    }
}
```

### Model Classes

```csharp
public record WcfDataContract
{
    public string ClassName { get; init; }
    public string Namespace { get; init; }
    public bool IsClass { get; init; }
    public bool IsEnum { get; init; }
    public string BaseType { get; init; }
    public List<WcfDataMember> Properties { get; init; } = new();
    public List<WcfEnumMember> EnumMembers { get; init; } = new();
}

public record WcfDataMember
{
    public string Name { get; init; }
    public string Type { get; init; }
    public int Order { get; init; }
    public bool IsRequired { get; init; }
    public bool IsNullable { get; init; }
    public bool IsCollection { get; init; }
    public bool EmitDefaultValue { get; init; } = true;
}

public record WcfEnumMember
{
    public string Name { get; init; }
    public int Value { get; init; }
}
```

### Type Mapping Reference

| WCF Type | gRPC Type | C# Record Type |
|----------|-----------|----------------|
| `int` | `int32` | `int` |
| `long` | `int64` | `long` |
| `string` | `string` | `string` |
| `bool` | `bool` | `bool` |
| `decimal` | `double` | `decimal` |
| `DateTime` | `google.protobuf.Timestamp` | `DateTime` |
| `int?` | `google.protobuf.Int32Value` | `int?` |
| `string[]` | `repeated string` | `string[]` |
| Custom class | message | record |

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
