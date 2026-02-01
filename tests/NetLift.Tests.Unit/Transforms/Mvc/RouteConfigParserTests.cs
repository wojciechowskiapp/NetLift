using FluentAssertions;
using NetLift.Core.Models.Mvc;
using NetLift.Transforms.Mvc.Parsers;
using Xunit;

namespace NetLift.Tests.Unit.Transforms.Mvc;

public sealed class RouteConfigParserTests
{
    private readonly RouteConfigParser _parser = new();

    [Fact]
    public void ParsesSimpleMapRoute()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapRoute(
                        name: "Default",
                        url: "{controller}/{action}/{id}",
                        defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
                    );
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().HaveCount(1);
        var route = routes[0];
        route.Name.Should().Be("Default");
        route.Template.Should().Be("{controller}/{action}/{id}");
        route.Defaults.Should().ContainKey("controller")
            .WhoseValue.Should().Be("Home");
        route.Defaults.Should().ContainKey("action")
            .WhoseValue.Should().Be("Index");
        route.Defaults.Should().ContainKey("id")
            .WhoseValue.Should().Be(RouteDefinition.OptionalParameter);
    }

    [Fact]
    public void ParsesRouteWithConstraints()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapRoute(
                        name: "Blog",
                        url: "blog/{year}/{month}/{id}",
                        defaults: new { controller = "Blog", action = "Archive" },
                        constraints: new { year = @"\d{4}", month = @"\d{2}", id = @"\d+" }
                    );
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().HaveCount(1);
        var route = routes[0];
        route.Name.Should().Be("Blog");
        route.Template.Should().Be("blog/{year}/{month}/{id}");
        route.Constraints.Should().HaveCount(3);
        route.Constraints["year"].Should().Be(@"\d{4}");
        route.Constraints["month"].Should().Be(@"\d{2}");
        route.Constraints["id"].Should().Be(@"\d+");
    }

    [Fact]
    public void ParsesOptionalParameter()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapRoute(
                        "Products",
                        "products/{category}/{id}",
                        new { controller = "Products", action = "List", category = UrlParameter.Optional, id = UrlParameter.Optional }
                    );
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().HaveCount(1);
        var route = routes[0];
        route.Defaults["category"].Should().Be(RouteDefinition.OptionalParameter);
        route.Defaults["id"].Should().Be(RouteDefinition.OptionalParameter);
        route.Defaults["controller"].Should().Be("Products");
        route.Defaults["action"].Should().Be("List");
    }

    [Fact]
    public void ParsesMultipleRoutes()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapRoute(
                        name: "Admin",
                        url: "admin/{controller}/{action}/{id}",
                        defaults: new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional }
                    );

                    routes.MapRoute(
                        name: "Default",
                        url: "{controller}/{action}/{id}",
                        defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
                    );
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().HaveCount(2);
        routes[0].Name.Should().Be("Admin");
        routes[0].Template.Should().Be("admin/{controller}/{action}/{id}");
        routes[1].Name.Should().Be("Default");
        routes[1].Template.Should().Be("{controller}/{action}/{id}");
    }

    [Fact]
    public void ParsesNamedArguments()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapRoute(
                        name: "API",
                        url: "api/{controller}/{action}",
                        defaults: new { controller = "Api", action = "Get" }
                    );
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().HaveCount(1);
        var route = routes[0];
        route.Name.Should().Be("API");
        route.Template.Should().Be("api/{controller}/{action}");
        route.Defaults["controller"].Should().Be("Api");
        route.Defaults["action"].Should().Be("Get");
    }

    [Fact]
    public void ParsesPositionalArguments()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapRoute(
                        "Users",
                        "users/{action}/{id}",
                        new { controller = "Users", action = "List", id = UrlParameter.Optional }
                    );
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().HaveCount(1);
        var route = routes[0];
        route.Name.Should().Be("Users");
        route.Template.Should().Be("users/{action}/{id}");
        route.Defaults["controller"].Should().Be("Users");
        route.Defaults["action"].Should().Be("List");
    }

    [Fact]
    public void IdentifiesDefaultRoute()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapRoute(
                        name: "Default",
                        url: "{controller}/{action}/{id}",
                        defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
                    );
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().HaveCount(1);
        routes[0].IsDefaultRoute.Should().BeTrue();
    }

    [Fact]
    public void HandlesEmptyRouteCollection()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().BeEmpty();
    }

    [Fact]
    public void HandlesEmptyOrNullSourceCode()
    {
        // Act & Assert
        _parser.Parse("").Should().BeEmpty();
        _parser.Parse(null!).Should().BeEmpty();
        _parser.Parse("   ").Should().BeEmpty();
    }

    [Fact]
    public void ParsesRouteWithoutDefaults()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapRoute(
                        name: "Simple",
                        url: "simple/{action}"
                    );
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().HaveCount(1);
        var route = routes[0];
        route.Name.Should().Be("Simple");
        route.Template.Should().Be("simple/{action}");
        route.Defaults.Should().BeEmpty();
        route.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void ParsesRouteWithMixedArgumentStyles()
    {
        // Arrange
        var sourceCode = """
            public class RouteConfig
            {
                public static void RegisterRoutes(RouteCollection routes)
                {
                    routes.MapRoute(
                        "Mixed",
                        url: "mixed/{action}",
                        defaults: new { controller = "Mixed", action = "Index" }
                    );
                }
            }
            """;

        // Act
        var routes = _parser.Parse(sourceCode);

        // Assert
        routes.Should().HaveCount(1);
        var route = routes[0];
        route.Name.Should().Be("Mixed");
        route.Template.Should().Be("mixed/{action}");
        route.Defaults["controller"].Should().Be("Mixed");
    }
}
