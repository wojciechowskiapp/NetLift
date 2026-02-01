using NetLift.Transforms.Mvc.Parsers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class AreaRegistrationParserTests
{
    private readonly AreaRegistrationParser _parser = new();

    [Fact]
    public void Parse_EmptySource_ReturnsEmptyList()
    {
        // Arrange
        var source = string.Empty;

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NoAreaRegistrationClass_ReturnsEmptyList()
    {
        // Arrange
        var source = """
using System;

namespace MyApp
{
    public class SomeClass
    {
        public void DoSomething() { }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_AreaRegistrationWithPropertyOverride_ExtractsAreaName()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get { return "Admin"; }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        Assert.Equal("Admin", result[0].Name);
        Assert.Equal("Admin", result[0].RoutePrefix);
    }

    [Fact]
    public void Parse_AreaRegistrationWithExpressionBodyProperty_ExtractsAreaName()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Admin";

        public override void RegisterArea(AreaRegistrationContext context)
        {
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        Assert.Equal("Admin", result[0].Name);
    }

    [Fact]
    public void Parse_AreaRegistrationWithClassNameConvention_ExtractsAreaName()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override void RegisterArea(AreaRegistrationContext context)
        {
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        Assert.Equal("Admin", result[0].Name);
    }

    [Fact]
    public void Parse_MultiWordAreaName_ExtractsCorrectly()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.BackOffice
{
    public class BackOfficeAreaRegistration : AreaRegistration
    {
        public override void RegisterArea(AreaRegistrationContext context)
        {
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        Assert.Equal("BackOffice", result[0].Name);
    }

    [Fact]
    public void Parse_RegisterAreaWithSingleRoute_ExtractsRoute()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Admin";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        var area = result[0];
        Assert.Single(area.Routes);

        var route = area.Routes[0];
        Assert.Equal("Admin_default", route.Name);
        Assert.Equal("Admin/{controller}/{action}/{id}", route.Template);
        Assert.Equal(2, route.Defaults.Count);
        Assert.Equal("Index", route.Defaults["action"]);
    }

    [Fact]
    public void Parse_RegisterAreaWithMultipleRoutes_ExtractsAllRoutes()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Admin";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Admin_users",
                "Admin/Users/{action}/{id}",
                new { controller = "Users", action = "Index", id = UrlParameter.Optional }
            );

            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        var area = result[0];
        Assert.Equal(2, area.Routes.Count);

        Assert.Equal("Admin_users", area.Routes[0].Name);
        Assert.Equal("Admin/Users/{action}/{id}", area.Routes[0].Template);

        Assert.Equal("Admin_default", area.Routes[1].Name);
        Assert.Equal("Admin/{controller}/{action}/{id}", area.Routes[1].Template);
    }

    [Fact]
    public void Parse_RegisterAreaWithNamedArguments_ExtractsRoute()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Admin";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                name: "Admin_default",
                url: "Admin/{controller}/{action}/{id}",
                defaults: new { action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        var area = result[0];
        Assert.Single(area.Routes);

        var route = area.Routes[0];
        Assert.Equal("Admin_default", route.Name);
        Assert.Equal("Admin/{controller}/{action}/{id}", route.Template);
    }

    [Fact]
    public void Parse_RegisterAreaWithConstraints_ExtractsConstraints()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Admin";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional },
                new { id = @"\d+" }
            );
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        var area = result[0];
        Assert.Single(area.Routes);

        var route = area.Routes[0];
        Assert.Single(route.Constraints);
        Assert.Equal(@"\d+", route.Constraints["id"]);
    }

    [Fact]
    public void Parse_EmptyRegisterArea_ReturnsAreaWithNoRoutes()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Admin";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            // No routes defined
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        var area = result[0];
        Assert.Equal("Admin", area.Name);
        Assert.Empty(area.Routes);
    }

    [Fact]
    public void Parse_QualifiedAreaRegistrationBase_ExtractsArea()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : System.Web.Mvc.AreaRegistration
    {
        public override string AreaName => "Admin";

        public override void RegisterArea(AreaRegistrationContext context)
        {
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        Assert.Equal("Admin", result[0].Name);
    }

    [Fact]
    public void Parse_RouteWithComplexDefaults_ParsesCorrectly()
    {
        // Arrange
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Admin";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { controller = "Home", action = "Index", id = UrlParameter.Optional, page = 1 }
            );
        }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Single(result);
        var route = result[0].Routes[0];
        Assert.Equal(4, route.Defaults.Count);
        Assert.Equal("Home", route.Defaults["controller"]);
        Assert.Equal("Index", route.Defaults["action"]);
        Assert.Equal(1, route.Defaults["page"]);
    }

    [Fact]
    public void Parse_MultipleAreaRegistrationClasses_ExtractsAll()
    {
        // Arrange - unlikely in practice but should handle it
        var source = """
using System.Web.Mvc;

namespace MyApp.Areas
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Admin";
        public override void RegisterArea(AreaRegistrationContext context) { }
    }

    public class ApiAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Api";
        public override void RegisterArea(AreaRegistrationContext context) { }
    }
}
""";

        // Act
        var result = _parser.Parse(source);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Name == "Admin");
        Assert.Contains(result, a => a.Name == "Api");
    }
}
