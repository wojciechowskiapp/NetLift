using FluentAssertions;
using NetLift.Core.Interfaces;
using NetLift.Transforms.Wcf.Parsers;

namespace NetLift.Tests.Unit.Transforms.Wcf.Parsers;

/// <summary>
/// Tests for the WCF DataContract parser.
/// </summary>
public class WcfDataContractParserTests
{
    private readonly IWcfDataContractParser _parser;

    public WcfDataContractParserTests()
    {
        _parser = new WcfDataContractParser();
    }

    [Fact]
    public void Parse_NullOrEmptySource_ReturnsEmptyList()
    {
        // Act
        var result1 = _parser.Parse(null!);
        var result2 = _parser.Parse(string.Empty);
        var result3 = _parser.Parse("   ");

        // Assert
        result1.Should().BeEmpty();
        result2.Should().BeEmpty();
        result3.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoDataContracts_ReturnsEmptyList()
    {
        // Arrange
        var source = @"
using System;

public class RegularClass
{
    public string Name { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_BasicDataContract_ParsesCorrectly()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract]
public class Customer
{
    [DataMember]
    public string Name { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];
        contract.TypeName.Should().Be("Customer");
        contract.FullyQualifiedName.Should().Contain("Customer");
        contract.IsClass.Should().BeTrue();
        contract.IsEnum.Should().BeFalse();
        contract.Properties.Should().HaveCount(1);
        contract.Properties[0].Name.Should().Be("Name");
        contract.Properties[0].Type.Should().Be("String");
    }

    [Fact]
    public void Parse_DataContractWithNamespace_ParsesNamespace()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract(Namespace = ""http://example.com/customer"")]
public class Customer
{
    [DataMember]
    public string Name { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        result[0].Namespace.Should().Be("http://example.com/customer");
    }

    [Fact]
    public void Parse_DataContractWithCustomName_ParsesName()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract(Name = ""CustomerDTO"")]
public class Customer
{
    [DataMember]
    public string Name { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("CustomerDTO");
    }

    [Fact]
    public void Parse_DataMemberWithAllProperties_ParsesCorrectly()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract]
public class Customer
{
    [DataMember(Order = 1, IsRequired = true, EmitDefaultValue = false)]
    public string Name { get; set; }

    [DataMember(Order = 2)]
    public int Age { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];
        contract.Properties.Should().HaveCount(2);

        var nameProp = contract.Properties.First(p => p.Name == "Name");
        nameProp.Order.Should().Be(1);
        nameProp.IsRequired.Should().BeTrue();
        nameProp.EmitDefaultValue.Should().BeFalse();

        var ageProp = contract.Properties.First(p => p.Name == "Age");
        ageProp.Order.Should().Be(2);
        ageProp.IsRequired.Should().BeFalse();
        ageProp.EmitDefaultValue.Should().BeTrue(); // default value
    }

    [Fact]
    public void Parse_EnumWithDataContract_ParsesCorrectly()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract]
public enum OrderStatus
{
    [EnumMember]
    Pending = 0,

    [EnumMember]
    Approved = 1,

    [EnumMember]
    Shipped = 2
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];
        contract.TypeName.Should().Be("OrderStatus");
        contract.IsEnum.Should().BeTrue();
        contract.IsClass.Should().BeFalse();
        contract.EnumMembers.Should().HaveCount(3);

        contract.EnumMembers[0].Name.Should().Be("Pending");
        contract.EnumMembers[0].Value.Should().Be(0);

        contract.EnumMembers[1].Name.Should().Be("Approved");
        contract.EnumMembers[1].Value.Should().Be(1);

        contract.EnumMembers[2].Name.Should().Be("Shipped");
        contract.EnumMembers[2].Value.Should().Be(2);
    }

    [Fact]
    public void Parse_EnumMemberWithCustomValue_ParsesSerializedName()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract]
public enum PaymentMethod
{
    [EnumMember(Value = ""credit_card"")]
    CreditCard = 1,

    [EnumMember(Value = ""paypal"")]
    PayPal = 2
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];
        contract.EnumMembers.Should().HaveCount(2);

        contract.EnumMembers[0].Name.Should().Be("CreditCard");
        contract.EnumMembers[0].SerializedName.Should().Be("credit_card");

        contract.EnumMembers[1].Name.Should().Be("PayPal");
        contract.EnumMembers[1].SerializedName.Should().Be("paypal");
    }

    [Fact]
    public void Parse_KnownTypes_ParsesCorrectly()
    {
        // Arrange
        var source = @"
using System;
using System.Runtime.Serialization;

[DataContract]
[KnownType(typeof(Dog))]
[KnownType(typeof(Cat))]
public class Animal
{
    [DataMember]
    public string Name { get; set; }
}

[DataContract]
public class Dog : Animal
{
    [DataMember]
    public bool CanBark { get; set; }
}

[DataContract]
public class Cat : Animal
{
    [DataMember]
    public bool CanMeow { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(3);
        var animal = result.First(c => c.TypeName == "Animal");
        animal.KnownTypes.Should().HaveCount(2);
        animal.KnownTypes.Should().Contain(kt => kt.Contains("Dog"));
        animal.KnownTypes.Should().Contain(kt => kt.Contains("Cat"));
    }

    [Fact]
    public void Parse_InheritanceHierarchy_ParsesBaseType()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract]
public class BaseEntity
{
    [DataMember]
    public int Id { get; set; }
}

[DataContract]
public class Customer : BaseEntity
{
    [DataMember]
    public string Name { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(2);

        var baseEntity = result.First(c => c.TypeName == "BaseEntity");
        baseEntity.BaseType.Should().NotBeNull();
        baseEntity.BaseType.Should().Contain("object");

        var customer = result.First(c => c.TypeName == "Customer");
        customer.BaseType.Should().NotBeNull();
        customer.BaseType.Should().Contain("BaseEntity");
    }

    [Fact]
    public void Parse_NullableTypes_DetectsNullable()
    {
        // Arrange
        var source = @"
using System;
using System.Runtime.Serialization;

[DataContract]
public class Order
{
    [DataMember]
    public DateTime? CompletedDate { get; set; }

    [DataMember]
    public int? Quantity { get; set; }

    [DataMember]
    public string Name { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];

        var completedDate = contract.Properties.First(p => p.Name == "CompletedDate");
        completedDate.IsNullable.Should().BeTrue();

        var quantity = contract.Properties.First(p => p.Name == "Quantity");
        quantity.IsNullable.Should().BeTrue();

        var name = contract.Properties.First(p => p.Name == "Name");
        name.IsNullable.Should().BeFalse(); // Reference types without ? are not marked nullable in this context
    }

    [Fact]
    public void Parse_CollectionTypes_DetectsCollections()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using System.Runtime.Serialization;

[DataContract]
public class Order
{
    [DataMember]
    public List<string> Items { get; set; }

    [DataMember]
    public string[] Tags { get; set; }

    [DataMember]
    public IEnumerable<int> Numbers { get; set; }

    [DataMember]
    public string SingleItem { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];

        var items = contract.Properties.First(p => p.Name == "Items");
        items.IsCollection.Should().BeTrue();

        var tags = contract.Properties.First(p => p.Name == "Tags");
        tags.IsCollection.Should().BeTrue();

        var numbers = contract.Properties.First(p => p.Name == "Numbers");
        numbers.IsCollection.Should().BeTrue();

        var singleItem = contract.Properties.First(p => p.Name == "SingleItem");
        singleItem.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void Parse_MultipleDataContracts_ParsesAll()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract]
public class Customer
{
    [DataMember]
    public string Name { get; set; }
}

[DataContract]
public class Order
{
    [DataMember]
    public int OrderId { get; set; }
}

[DataContract]
public enum OrderStatus
{
    [EnumMember]
    Pending = 0
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(c => c.TypeName == "Customer");
        result.Should().Contain(c => c.TypeName == "Order");
        result.Should().Contain(c => c.TypeName == "OrderStatus");
    }

    [Fact]
    public void Parse_PropertiesWithoutDataMember_IgnoresThem()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract]
public class Customer
{
    [DataMember]
    public string Name { get; set; }

    public string InternalField { get; set; }

    [DataMember]
    public int Age { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];
        contract.Properties.Should().HaveCount(2);
        contract.Properties.Should().Contain(p => p.Name == "Name");
        contract.Properties.Should().Contain(p => p.Name == "Age");
        contract.Properties.Should().NotContain(p => p.Name == "InternalField");
    }

    [Fact]
    public void Parse_StructDataContract_ParsesCorrectly()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract]
public struct Point
{
    [DataMember]
    public int X { get; set; }

    [DataMember]
    public int Y { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];
        contract.TypeName.Should().Be("Point");
        contract.IsClass.Should().BeTrue(); // Struct is also treated as class-like
        contract.Properties.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ComplexNestedTypes_ParsesFullTypeName()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using System.Runtime.Serialization;

[DataContract]
public class Container
{
    [DataMember]
    public Dictionary<string, List<int>> ComplexType { get; set; }
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];
        var prop = contract.Properties[0];
        prop.FullTypeName.Should().Contain("Dictionary");
        prop.FullTypeName.Should().ContainAny("String", "string");
        prop.FullTypeName.Should().Contain("List");
        prop.FullTypeName.Should().ContainAny("Int32", "int");
    }

    [Fact]
    public void Parse_EnumWithoutEnumMember_StillParsesEnumValues()
    {
        // Arrange
        var source = @"
using System.Runtime.Serialization;

[DataContract]
public enum Status
{
    Active = 1,
    Inactive = 2,
    [EnumMember]
    Deleted = 3
}";

        // Act
        var result = _parser.Parse(source);

        // Assert
        result.Should().HaveCount(1);
        var contract = result[0];
        // Only EnumMember decorated values should be included
        contract.EnumMembers.Should().HaveCount(1);
        contract.EnumMembers[0].Name.Should().Be("Deleted");
    }

    [Fact]
    public void Diagnostics_ClearedOnEachParse()
    {
        // Arrange
        var validSource = @"
using System.Runtime.Serialization;

[DataContract]
public class Customer
{
    [DataMember]
    public string Name { get; set; }
}";

        // Act
        _parser.Parse(validSource);
        var firstDiagnostics = _parser.Diagnostics.Count;

        _parser.Parse(validSource);
        var secondDiagnostics = _parser.Diagnostics.Count;

        // Assert - diagnostics should be cleared on each parse
        firstDiagnostics.Should().Be(0);
        secondDiagnostics.Should().Be(0);
    }
}
