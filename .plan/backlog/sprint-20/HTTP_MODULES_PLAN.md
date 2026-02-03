# HTTP Modules → Middleware Migration - Implementation Plan

> **Feature:** Transform Global.asax handlers and custom HTTP modules to ASP.NET Core middleware

---

## Scope

| Source Pattern | Target Pattern | Confidence |
|---------------|----------------|-----------|
| Application_Start | Program.cs startup | 90% |
| Application_BeginRequest | Middleware | 80% |
| Application_AuthenticateRequest | AuthN middleware | 75% |
| Application_AuthorizeRequest | AuthZ middleware | 75% |
| Application_Error | Exception middleware | 85% |
| Application_EndRequest | Middleware | 80% |
| Application_End | IHostApplicationLifetime | 85% |
| Custom IHttpModule | Middleware class | 70% |

---

## Key Transformations

### Global.asax → Program.cs

**Before:**
```csharp
public class MvcApplication : HttpApplication
{
    protected void Application_Start()
    {
        AreaRegistration.RegisterAllAreas();
        RouteConfig.RegisterRoutes(RouteTable.Routes);
        BundleConfig.RegisterBundles(BundleTable.Bundles);
    }

    protected void Application_Error()
    {
        var exception = Server.GetLastError();
        Logger.Error(exception);
    }
}
```

**After:**
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseExceptionHandler("/Error");
app.MapControllerRoute(...);
```

### Custom HTTP Module → Middleware

**Before:**
```csharp
public class LoggingModule : IHttpModule
{
    public void Init(HttpApplication context)
    {
        context.BeginRequest += OnBeginRequest;
    }

    private void OnBeginRequest(object sender, EventArgs e)
    {
        var app = (HttpApplication)sender;
        Debug.WriteLine($"Request: {app.Request.Url}");
    }
}
```

**After:**
```csharp
public class LoggingMiddleware
{
    private readonly RequestDelegate _next;

    public LoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Debug.WriteLine($"Request: {context.Request.Path}");
        await _next(context);
    }
}
```

---

## Sprint Tasks (Sprint 20)

| # | Task | Size | Description |
|---|------|------|-------------|
| 195 | GlobalAsaxInfo model | S | Global.asax analysis |
| 196 | HttpModuleInfo model | S | HTTP module analysis |
| 197 | IGlobalAsaxAnalyzer interface | S | Analysis contract |
| 198 | IMiddlewareGenerator interface | S | Generation contract |
| 199 | GlobalAsaxAnalyzer | M | Parse Global.asax events |
| 200 | HttpModuleDetector | M | Find IHttpModule implementations |
| 201 | MiddlewareGenerator | L | Generate middleware classes |
| 202 | Application_Error → ExceptionHandler | M | Error handling migration |
| 203 | Unit tests (25+) | L | Transformation tests |
| 204 | Integration tests | M | Full migration test |

---

*Last updated: 2026-02-03*
