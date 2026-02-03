# SignalR Modernization Implementation Plan

## Overview

This document outlines the implementation plan for migrating ASP.NET SignalR (Microsoft.AspNet.SignalR) to ASP.NET Core SignalR (Microsoft.AspNetCore.SignalR) in NetLift.

## Research Summary

### Key Migration Differences

#### 1. Package Changes
- **Old**: Microsoft.AspNet.SignalR, Microsoft.AspNet.SignalR.Client
- **New**: Microsoft.AspNetCore.SignalR, Microsoft.AspNetCore.SignalR.Client

#### 2. Hub Lifecycle Methods
| ASP.NET SignalR | ASP.NET Core SignalR | Changes |
|----------------|---------------------|---------|
| `OnConnected()` | `OnConnectedAsync()` | Now async, returns Task |
| `OnDisconnected(bool stopCalled)` | `OnDisconnectedAsync(Exception exception)` | Parameter changed from bool to Exception |
| `OnReconnected()` | REMOVED | No longer exists in Core SignalR |

#### 3. Startup Configuration
**ASP.NET SignalR (OWIN):**
```csharp
// In Startup.cs or Global.asax
app.MapSignalR();
// OR
RouteTable.Routes.MapHubs();
```

**ASP.NET Core SignalR:**
```csharp
// In Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddSignalR();
}

public void Configure(IApplicationBuilder app)
{
    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapHub<ChatHub>("/chatHub");
        endpoints.MapHub<NotificationHub>("/notificationHub");
    });
}

// OR in Program.cs (minimal hosting)
builder.Services.AddSignalR();
app.MapHub<ChatHub>("/chatHub");
```

**Key Changes:**
- Each hub must be mapped individually (no bulk MapHubs())
- Uses middleware pattern with AddSignalR() and MapHub()
- Explicit routing required

#### 4. Client Invocation Patterns
**ASP.NET SignalR:**
```csharp
Clients.All.notifyUser(message);
Clients.Caller.updateProgress(percent);
Clients.Others.broadcastMessage(msg);
Clients.Client(connectionId).sendPrivateMessage(msg);
Clients.Group(groupName).updateGroup(data);
```

**ASP.NET Core SignalR:**
```csharp
await Clients.All.SendAsync("notifyUser", message);
await Clients.Caller.SendAsync("updateProgress", percent);
await Clients.Others.SendAsync("broadcastMessage", msg);
await Clients.Client(connectionId).SendAsync("sendPrivateMessage", msg);
await Clients.Group(groupName).SendAsync("updateGroup", data);
```

**Key Changes:**
- All invocations use `SendAsync(string methodName, params object[] args)`
- Method name passed as string (no hub proxy)
- All calls are async

#### 5. Dependency Injection
**ASP.NET SignalR:**
```csharp
var context = GlobalHost.ConnectionManager.GetHubContext<MyHub>();
context.Clients.All.notify("message");
```

**ASP.NET Core SignalR:**
```csharp
public class MyService
{
    private readonly IHubContext<MyHub> _hubContext;

    public MyService(IHubContext<MyHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyClients()
    {
        await _hubContext.Clients.All.SendAsync("notify", "message");
    }
}
```

**Key Changes:**
- GlobalHost removed entirely
- IHubContext<T> injected via constructor
- Hub classes now support constructor injection

#### 6. JavaScript Client
**ASP.NET SignalR:**
```javascript
<script src="~/Scripts/jquery.signalR-2.4.1.min.js"></script>
<script src="~/signalr/hubs"></script>

var connection = $.connection.chatHub;
connection.client.receiveMessage = function(user, message) {
    console.log(user + ": " + message);
};
$.connection.hub.start().done(function() {
    connection.server.send("Hello");
});
```

**ASP.NET Core SignalR:**
```javascript
// npm install @microsoft/signalr

import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .build();

connection.on("receiveMessage", (user, message) => {
    console.log(user + ": " + message);
});

connection.start().then(() => {
    connection.invoke("send", "Hello");
});
```

**Key Changes:**
- jQuery dependency removed
- @microsoft/signalr npm package (replaces jquery.signalR)
- HubConnectionBuilder API
- No automatic hub proxy generation
- Modern ES6+ JavaScript patterns

#### 7. Removed Features
- **GlobalHost**: Use IHubContext<T> via DI
- **HubState**: Stateless model, use Context.Items or external storage
- **PersistentConnection**: Removed, use Hub instead
- **HubPipeline modules**: Replace with custom middleware
- **Forever Frame transport**: No longer supported

## Architecture

Following NetLift conventions:

```
NetLift.Core/
  Models/SignalR/              (Data models)
  Interfaces/                  (Interfaces)

NetLift.Transforms/
  SignalR/
    Analyzers/                 (Roslyn-based analyzers)
    Transformers/              (Code transformers)
    Generators/                (Code generators)

NetLift.Tests/
  SignalR/
    Analyzers/                 (Analyzer tests)
    Transformers/              (Transformer tests)
    Generators/                (Generator tests)
```

## Models

### 1. SignalRHubInfo
```csharp
/// <summary>
/// Represents a SignalR Hub class with all its methods and configuration.
/// </summary>
public sealed record SignalRHubInfo
{
    public required string FilePath { get; init; }
    public required string ClassName { get; init; }
    public required string Namespace { get; init; }
    public string BaseClass { get; init; } = "Hub";
    public IReadOnlyList<string> ImplementedInterfaces { get; init; } = [];
    public IReadOnlyList<HubMethodInfo> HubMethods { get; init; } = [];
    public required HubLifecycleInfo LifecycleMethods { get; init; }
    public IReadOnlyList<ClientInvocationInfo> ClientInvocations { get; init; } = [];
    public IReadOnlyList<GroupOperationInfo> GroupOperations { get; init; } = [];
    public bool UsesContext { get; init; }
    public IReadOnlyList<ContextUsageInfo> ContextUsages { get; init; } = [];
    public IReadOnlyList<HubDependency> Dependencies { get; init; } = [];
}
```

### 2. HubMethodInfo
```csharp
/// <summary>
/// Represents a hub method that can be called from clients.
/// </summary>
public sealed record HubMethodInfo
{
    public required string Name { get; init; }
    public required string ReturnType { get; init; }
    public bool IsAsync { get; init; }
    public IReadOnlyList<HubParameter> Parameters { get; init; } = [];
    public string? XmlDocumentation { get; init; }
    public required string SourceCode { get; init; }
    public int LineNumber { get; init; }
}
```

### 3. HubLifecycleInfo
```csharp
/// <summary>
/// Tracks which lifecycle methods are present in the hub.
/// </summary>
public sealed record HubLifecycleInfo
{
    public bool HasOnConnected { get; init; }
    public string? OnConnectedSourceCode { get; init; }
    public int OnConnectedLineNumber { get; init; }

    public bool HasOnDisconnected { get; init; }
    public string? OnDisconnectedSourceCode { get; init; }
    public int OnDisconnectedLineNumber { get; init; }
    public bool OnDisconnectedUsesParameter { get; init; }

    public bool HasOnReconnected { get; init; }
    public string? OnReconnectedSourceCode { get; init; }
    public int OnReconnectedLineNumber { get; init; }
}
```

### 4. ClientInvocationInfo
```csharp
/// <summary>
/// Represents a client method invocation (Clients.All, Clients.Caller, etc.).
/// </summary>
public sealed record ClientInvocationInfo
{
    public required ClientInvocationType InvocationType { get; init; }
    public required string MethodName { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public required string SourceCode { get; init; }
    public int LineNumber { get; init; }
    public bool UsesLegacyPattern { get; init; }
    public string? TargetIdentifier { get; init; } // For Client(id), Group(name)
}
```

### 5. ClientInvocationType Enum
```csharp
/// <summary>
/// Types of client invocations in SignalR.
/// </summary>
public enum ClientInvocationType
{
    All,
    Caller,
    Others,
    Client,        // Single client by connection ID
    Clients,       // Multiple clients by connection IDs
    Group,         // Single group
    Groups,        // Multiple groups
    User,          // Single user
    Users,         // Multiple users
    AllExcept,     // All except specific connections
    GroupExcept,   // Group except specific connections
    OthersInGroup  // Others in a specific group
}
```

### 6. GroupOperationInfo
```csharp
/// <summary>
/// Represents Groups.Add or Groups.Remove operations.
/// </summary>
public sealed record GroupOperationInfo
{
    public required GroupOperationType OperationType { get; init; }
    public required string ConnectionIdExpression { get; init; }
    public required string GroupNameExpression { get; init; }
    public required string SourceCode { get; init; }
    public int LineNumber { get; init; }
}

public enum GroupOperationType
{
    Add,
    Remove
}
```

### 7. ContextUsageInfo
```csharp
/// <summary>
/// Tracks usage of the Context property (ConnectionId, User, etc.).
/// </summary>
public sealed record ContextUsageInfo
{
    public required string PropertyName { get; init; }
    public required string SourceCode { get; init; }
    public int LineNumber { get; init; }
}
```

### 8. HubDependency
```csharp
/// <summary>
/// Represents a constructor dependency in a hub.
/// </summary>
public sealed record HubDependency
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsDbContext { get; init; }
    public bool IsRepository { get; init; }
    public bool IsLogger { get; init; }
    public bool IsHubContext { get; init; }
}
```

### 9. HubParameter
```csharp
/// <summary>
/// Represents a hub method parameter.
/// </summary>
public sealed record HubParameter
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsNullable { get; init; }
    public bool HasDefaultValue { get; init; }
    public string? DefaultValue { get; init; }
}
```

### 10. GlobalHostUsageInfo
```csharp
/// <summary>
/// Tracks GlobalHost usage that needs to be migrated to IHubContext.
/// </summary>
public sealed record GlobalHostUsageInfo
{
    public required GlobalHostUsageType UsageType { get; init; }
    public string? HubType { get; init; }
    public required string SourceCode { get; init; }
    public required string FilePath { get; init; }
    public int LineNumber { get; init; }
}

public enum GlobalHostUsageType
{
    GetHubContext,
    ConnectionManager,
    DependencyResolver
}
```

### 11. SignalRStartupInfo
```csharp
/// <summary>
/// Tracks SignalR startup configuration in Startup.cs or Program.cs.
/// </summary>
public sealed record SignalRStartupInfo
{
    public required string FilePath { get; init; }
    public bool HasMapSignalR { get; init; }
    public string? MapSignalRSourceCode { get; init; }
    public int MapSignalRLineNumber { get; init; }
    public bool UsesOwin { get; init; }
    public IReadOnlyList<string> DetectedHubRoutes { get; init; } = [];
}
```

### 12. SignalRJavaScriptInfo
```csharp
/// <summary>
/// Tracks JavaScript SignalR client usage.
/// </summary>
public sealed record SignalRJavaScriptInfo
{
    public required string FilePath { get; init; }
    public bool UsesJQuery { get; init; }
    public string? ConnectionCode { get; init; }
    public string? HubProxyName { get; init; }
    public IReadOnlyList<JavaScriptEventHandler> EventHandlers { get; init; } = [];
    public IReadOnlyList<string> ServerMethodCalls { get; init; } = [];
}
```

### 13. JavaScriptEventHandler
```csharp
/// <summary>
/// Represents a JavaScript event handler (connection.client.methodName).
/// </summary>
public sealed record JavaScriptEventHandler
{
    public required string EventName { get; init; }
    public required string HandlerCode { get; init; }
    public int LineNumber { get; init; }
}
```

### 14. ScriptReferenceInfo
```csharp
/// <summary>
/// Tracks SignalR script references in HTML/Razor files.
/// </summary>
public sealed record ScriptReferenceInfo
{
    public required string FilePath { get; init; }
    public required string ScriptPath { get; init; }
    public int LineNumber { get; init; }
    public bool IsJQuerySignalR { get; init; }
    public bool IsGeneratedProxy { get; init; } // ~/signalr/hubs
}
```

## Interfaces

### 1. ISignalRHubAnalyzer
```csharp
/// <summary>
/// Analyzes SignalR Hub classes to extract methods, lifecycle, and client invocations.
/// </summary>
public interface ISignalRHubAnalyzer
{
    /// <summary>
    /// Analyzes all SignalR hubs in the specified project.
    /// </summary>
    Task<IReadOnlyList<SignalRHubInfo>> AnalyzeHubsAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a single hub file.
    /// </summary>
    Task<SignalRHubInfo?> AnalyzeHubFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects lifecycle methods in a hub class.
    /// </summary>
    HubLifecycleInfo DetectLifecycleMethods(ClassDeclarationSyntax hubClass);

    /// <summary>
    /// Detects client invocations in a method.
    /// </summary>
    IReadOnlyList<ClientInvocationInfo> DetectClientInvocations(MethodDeclarationSyntax method);
}
```

### 2. ISignalRStartupAnalyzer
```csharp
/// <summary>
/// Analyzes SignalR startup configuration in Startup.cs or Program.cs.
/// </summary>
public interface ISignalRStartupAnalyzer
{
    /// <summary>
    /// Analyzes SignalR startup configuration.
    /// </summary>
    Task<SignalRStartupInfo?> AnalyzeStartupAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects MapSignalR or MapHubs calls.
    /// </summary>
    bool DetectMapSignalR(SyntaxNode syntaxNode);
}
```

### 3. IGlobalHostAnalyzer
```csharp
/// <summary>
/// Analyzes GlobalHost usage that needs migration to IHubContext.
/// </summary>
public interface IGlobalHostAnalyzer
{
    /// <summary>
    /// Analyzes GlobalHost usage across the project.
    /// </summary>
    Task<IReadOnlyList<GlobalHostUsageInfo>> AnalyzeGlobalHostUsageAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}
```

### 4. ISignalRJavaScriptAnalyzer
```csharp
/// <summary>
/// Analyzes JavaScript SignalR client code.
/// </summary>
public interface ISignalRJavaScriptAnalyzer
{
    /// <summary>
    /// Analyzes JavaScript files for SignalR client usage.
    /// </summary>
    Task<IReadOnlyList<SignalRJavaScriptInfo>> AnalyzeJavaScriptFilesAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes HTML/Razor files for SignalR script references.
    /// </summary>
    Task<IReadOnlyList<ScriptReferenceInfo>> AnalyzeScriptReferencesAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}
```

### 5. ISignalRHubTransformer
```csharp
/// <summary>
/// Transforms SignalR Hub classes for ASP.NET Core.
/// </summary>
public interface ISignalRHubTransformer
{
    /// <summary>
    /// Transforms hub lifecycle methods.
    /// </summary>
    Task<TransformResult> TransformHubLifecycleAsync(
        SignalRHubInfo hubInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transforms client invocations to use SendAsync.
    /// </summary>
    Task<TransformResult> TransformClientInvocationsAsync(
        SignalRHubInfo hubInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transforms Groups.Add/Remove operations (mostly compatible).
    /// </summary>
    Task<TransformResult> TransformGroupOperationsAsync(
        SignalRHubInfo hubInfo,
        CancellationToken cancellationToken = default);
}
```

### 6. ISignalRStartupTransformer
```csharp
/// <summary>
/// Transforms SignalR startup configuration.
/// </summary>
public interface ISignalRStartupTransformer
{
    /// <summary>
    /// Transforms Startup.cs configuration.
    /// </summary>
    Task<TransformResult> TransformStartupConfigurationAsync(
        SignalRStartupInfo startupInfo,
        IReadOnlyList<SignalRHubInfo> hubs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates MapHub configuration for all detected hubs.
    /// </summary>
    string GenerateMapHubConfiguration(IReadOnlyList<SignalRHubInfo> hubs);
}
```

### 7. IGlobalHostTransformer
```csharp
/// <summary>
/// Transforms GlobalHost usage to IHubContext dependency injection.
/// </summary>
public interface IGlobalHostTransformer
{
    /// <summary>
    /// Transforms GlobalHost.ConnectionManager.GetHubContext to IHubContext injection.
    /// </summary>
    Task<TransformResult> TransformGlobalHostUsageAsync(
        GlobalHostUsageInfo usageInfo,
        CancellationToken cancellationToken = default);
}
```

### 8. ISignalRJavaScriptGenerator
```csharp
/// <summary>
/// Generates JavaScript client code for ASP.NET Core SignalR.
/// </summary>
public interface ISignalRJavaScriptGenerator
{
    /// <summary>
    /// Generates HubConnectionBuilder code from jQuery-based code.
    /// </summary>
    string GenerateHubConnectionBuilderCode(SignalRJavaScriptInfo jsInfo);

    /// <summary>
    /// Generates package.json updates for @microsoft/signalr.
    /// </summary>
    string GeneratePackageJsonUpdates();

    /// <summary>
    /// Generates TypeScript interface for strongly-typed hub client.
    /// </summary>
    string GenerateTypeScriptHubInterface(SignalRHubInfo hubInfo);
}
```

## Implementation Approach

### Phase 1: Analysis (Analyzers)

#### 1.1 SignalRHubAnalyzer
- Use Roslyn to parse C# files
- Find classes inheriting from `Hub` base class
- Extract public methods (hub methods)
- Detect lifecycle methods: OnConnected, OnDisconnected, OnReconnected
- Parse Clients.* invocations using MemberAccessExpressionSyntax
- Parse Groups.Add/Remove operations
- Track Context property usage
- Extract constructor dependencies

**Detection patterns:**
```csharp
// Detect Hub class
var hubClass = classDecl.BaseList?.Types
    .Any(t => t.Type.ToString().Contains("Hub"));

// Detect Clients.All.method()
var clientInvocation = memberAccess.Expression is MemberAccessExpressionSyntax inner &&
    inner.Expression.ToString() == "Clients";

// Detect OnConnected
var onConnected = methods.FirstOrDefault(m => m.Identifier.Text == "OnConnected");
```

#### 1.2 SignalRStartupAnalyzer
- Find Startup.cs or Program.cs
- Detect `app.MapSignalR()` or `RouteTable.Routes.MapHubs()`
- Detect OWIN configuration
- Extract existing hub routes if explicitly defined

#### 1.3 GlobalHostAnalyzer
- Search for `GlobalHost` usage across all C# files
- Detect `GlobalHost.ConnectionManager.GetHubContext<T>()`
- Extract hub type from generic parameter
- Track file and line number for each usage

#### 1.4 SignalRJavaScriptAnalyzer
- Search for .js files containing `$.connection` or `jquery.signalR`
- Parse connection setup code
- Extract hub proxy names
- Find event handlers (connection.client.methodName)
- Find server method calls (connection.server.methodName)
- Search HTML/Razor for script references to jquery.signalR or /signalr/hubs

### Phase 2: Transformation (Transformers)

#### 2.1 SignalRHubTransformer

**Lifecycle transformation:**
```csharp
// OnConnected() → OnConnectedAsync()
public override Task OnConnected()
{
    // body
}
// Becomes
public override async Task OnConnectedAsync()
{
    await base.OnConnectedAsync();
    // body
}

// OnDisconnected(bool stopCalled) → OnDisconnectedAsync(Exception exception)
public override Task OnDisconnected(bool stopCalled)
{
    if (stopCalled) { /* handle stop */ }
}
// Becomes
public override async Task OnDisconnectedAsync(Exception? exception)
{
    // TODO: Review - stopCalled parameter replaced with exception parameter
    // Original logic: if (stopCalled) { /* handle stop */ }
    await base.OnDisconnectedAsync(exception);
}

// OnReconnected() → REMOVED
public override Task OnReconnected()
{
    // body
}
// Becomes
// TODO: OnReconnected no longer exists in ASP.NET Core SignalR
// Consider handling reconnection logic in OnConnectedAsync if needed
// Original code:
// public override Task OnReconnected() { /* body */ }
```

**Confidence:**
- OnConnected → OnConnectedAsync: 95%
- OnDisconnected without parameter usage: 90%
- OnDisconnected with parameter usage: 70% (requires manual review)
- OnReconnected removal: 60% (requires manual review)

**Client invocation transformation:**
```csharp
// Before
Clients.All.notifyUser(message);
Clients.Caller.updateProgress(percent);
Clients.Others.broadcastMessage(msg);
Clients.Client(connectionId).sendPrivate(msg);
Clients.Group(groupName).updateGroup(data);

// After
await Clients.All.SendAsync("notifyUser", message);
await Clients.Caller.SendAsync("updateProgress", percent);
await Clients.Others.SendAsync("broadcastMessage", msg);
await Clients.Client(connectionId).SendAsync("sendPrivate", msg);
await Clients.Group(groupName).SendAsync("updateGroup", data);
```

**Confidence:**
- Simple client invocations: 95%
- Dynamic method names: 70%
- Complex expressions: 60%

**Group operations:**
```csharp
// These remain largely the same, just async
Groups.Add(Context.ConnectionId, groupName);
await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

Groups.Remove(Context.ConnectionId, groupName);
await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
```

**Confidence:** 90%

#### 2.2 SignalRStartupTransformer

**Startup transformation:**
```csharp
// Before (OWIN)
public void Configuration(IAppBuilder app)
{
    app.MapSignalR();
}

// After (ASP.NET Core)
public void ConfigureServices(IServiceCollection services)
{
    services.AddSignalR();
}

public void Configure(IApplicationBuilder app)
{
    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapHub<ChatHub>("/chatHub");
        endpoints.MapHub<NotificationHub>("/notificationHub");
    });
}
```

**Confidence:**
- Simple MapSignalR: 85%
- Complex configuration: 70%

#### 2.3 GlobalHostTransformer

**GlobalHost transformation:**
```csharp
// Before
var hubContext = GlobalHost.ConnectionManager.GetHubContext<MyHub>();
await hubContext.Clients.All.SendAsync("notify", "message");

// After - requires DI registration and constructor injection
// Add to constructor:
private readonly IHubContext<MyHub> _hubContext;

public MyService(IHubContext<MyHub> hubContext)
{
    _hubContext = hubContext;
}

// Replace GlobalHost usage:
await _hubContext.Clients.All.SendAsync("notify", "message");
```

**Confidence:**
- Simple GetHubContext: 75%
- Complex scenarios: 60%

### Phase 3: Generation (Generators)

#### 3.1 SignalRJavaScriptGenerator

**Generate HubConnectionBuilder code:**
```javascript
// Before
var connection = $.connection.chatHub;
connection.client.receiveMessage = function(user, msg) { ... };
$.connection.hub.start().done(function() {
    connection.server.send("Hello");
});

// After
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

connection.on("receiveMessage", (user, msg) => {
    // ...
});

connection.start()
    .then(() => connection.invoke("send", "Hello"))
    .catch(err => console.error(err));
```

**Generate package.json:**
```json
{
  "dependencies": {
    "@microsoft/signalr": "^8.0.0"
  }
}
```

**Generate TypeScript interface:**
```typescript
// Generated from ChatHub
export interface IChatHubClient {
    receiveMessage(user: string, message: string): void;
    userJoined(userName: string): void;
    userLeft(userName: string): void;
}

export interface IChatHubServer {
    send(message: string): Promise<void>;
    joinGroup(groupName: string): Promise<void>;
    leaveGroup(groupName: string): Promise<void>;
}
```

**Confidence:**
- Basic connection setup: 80%
- Event handlers: 85%
- TypeScript generation: 90%

## Test Cases

### Analyzer Tests

#### SignalRHubAnalyzerTests
1. **DetectSimpleHub** - Detects a basic hub with no methods
2. **DetectHubWithMethods** - Detects hub methods
3. **DetectLifecycleMethods** - Detects OnConnected, OnDisconnected, OnReconnected
4. **DetectClientInvocations_All** - Detects Clients.All.method()
5. **DetectClientInvocations_Caller** - Detects Clients.Caller.method()
6. **DetectClientInvocations_Others** - Detects Clients.Others.method()
7. **DetectClientInvocations_Client** - Detects Clients.Client(id).method()
8. **DetectClientInvocations_Group** - Detects Clients.Group(name).method()
9. **DetectGroupOperations** - Detects Groups.Add and Groups.Remove
10. **DetectContextUsage** - Detects Context.ConnectionId, Context.User
11. **DetectHubDependencies** - Detects constructor dependencies
12. **DetectAsyncHubMethods** - Detects async hub methods
13. **DetectHubParameters** - Extracts method parameters correctly

#### SignalRStartupAnalyzerTests
1. **DetectMapSignalR_OWIN** - Detects app.MapSignalR()
2. **DetectMapHubs_GlobalAsax** - Detects RouteTable.Routes.MapHubs()
3. **DetectNoSignalR** - Returns null when no SignalR configuration
4. **DetectCustomConfiguration** - Handles custom HubConfiguration

#### GlobalHostAnalyzerTests
1. **DetectGetHubContext** - Detects GlobalHost.ConnectionManager.GetHubContext<T>()
2. **DetectMultipleUsages** - Detects multiple GlobalHost usages
3. **ExtractHubType** - Extracts generic type parameter correctly
4. **DetectNoGlobalHost** - Returns empty list when no usage

#### SignalRJavaScriptAnalyzerTests
1. **DetectJQueryConnection** - Detects $.connection.hubName
2. **DetectHubProxy** - Extracts hub proxy name
3. **DetectEventHandlers** - Parses connection.client.methodName
4. **DetectServerCalls** - Parses connection.server.methodName
5. **DetectScriptReference** - Finds jquery.signalR script tag
6. **DetectGeneratedProxy** - Finds ~/signalr/hubs reference

### Transformer Tests

#### SignalRHubTransformerTests
1. **TransformOnConnected** - OnConnected() → OnConnectedAsync()
2. **TransformOnDisconnected_NoParam** - OnDisconnected(bool) without param usage
3. **TransformOnDisconnected_WithParam** - OnDisconnected(bool) with param usage
4. **RemoveOnReconnected** - Removes OnReconnected with TODO
5. **TransformClientsAll** - Clients.All.method() → SendAsync
6. **TransformClientsCaller** - Clients.Caller.method() → SendAsync
7. **TransformClientsOthers** - Clients.Others.method() → SendAsync
8. **TransformClientsClient** - Clients.Client(id).method() → SendAsync
9. **TransformClientsGroup** - Clients.Group(name).method() → SendAsync
10. **TransformGroupsAdd** - Groups.Add → AddToGroupAsync
11. **TransformGroupsRemove** - Groups.Remove → RemoveFromGroupAsync
12. **TransformMultipleInvocations** - Handles multiple client calls
13. **PreserveContextUsage** - Context.ConnectionId unchanged
14. **AddAsyncModifier** - Adds async to method signature when needed

#### SignalRStartupTransformerTests
1. **TransformMapSignalR** - app.MapSignalR() → AddSignalR + MapHub
2. **GenerateMapHubForMultipleHubs** - Creates MapHub for each hub
3. **GenerateStartupServices** - Creates AddSignalR() in ConfigureServices
4. **TransformOwinStartup** - Transforms OWIN Startup class
5. **PreserveOtherMiddleware** - Doesn't affect other configuration

#### GlobalHostTransformerTests
1. **TransformGetHubContext** - Transforms to IHubContext injection
2. **GenerateConstructorInjection** - Generates constructor parameter
3. **GenerateFieldDeclaration** - Generates private readonly field
4. **ReplaceUsage** - Replaces GlobalHost.* with field
5. **HandleMultipleHubs** - Handles multiple hub contexts

### Generator Tests

#### SignalRJavaScriptGeneratorTests
1. **GenerateHubConnectionBuilder** - Generates modern connection code
2. **GeneratePackageJson** - Creates package.json entry
3. **GenerateTypeScriptInterface_Client** - Generates client interface
4. **GenerateTypeScriptInterface_Server** - Generates server interface
5. **TransformEventHandlers** - Converts connection.client to connection.on
6. **TransformServerCalls** - Converts connection.server to connection.invoke
7. **GenerateConnectionSetup** - Creates start/stop logic
8. **GenerateErrorHandling** - Adds catch blocks

## Confidence Scoring

### High Confidence (95-100%)
- Hub base class (no changes needed)
- OnConnected → OnConnectedAsync (straightforward)
- Simple Clients.All.SendAsync transformations
- Package reference updates
- Groups.AddToGroupAsync/RemoveFromGroupAsync

### Medium-High Confidence (80-94%)
- Clients.Group/Client transformations
- MapSignalR → MapHub (with detected hubs)
- JavaScript package.json updates
- TypeScript interface generation

### Medium Confidence (60-79%)
- OnDisconnected with parameter usage (bool → Exception)
- GlobalHost → IHubContext (requires DI setup)
- Complex client invocations with dynamic names
- JavaScript HubConnectionBuilder generation
- OnReconnected removal (needs replacement logic)

### Low Confidence (<60%)
- HubPipeline module replacements
- PersistentConnection migrations
- Complex GlobalHost patterns
- Custom authentication/authorization
- Complex OWIN middleware integration

## Package Mappings

Add to `package-mappings.yml`:

```yaml
signalr:
  - old: Microsoft.AspNet.SignalR
    new: Microsoft.AspNetCore.SignalR
    confidence: 100
    notes: Core SignalR hub server package

  - old: Microsoft.AspNet.SignalR.Core
    new: Microsoft.AspNetCore.SignalR.Core
    confidence: 100
    notes: Core SignalR abstractions

  - old: Microsoft.AspNet.SignalR.Client
    new: Microsoft.AspNetCore.SignalR.Client
    confidence: 100
    notes: .NET SignalR client

  - old: Microsoft.AspNet.SignalR.SystemWeb
    new: null
    confidence: 100
    notes: Not needed in ASP.NET Core

  - old: Microsoft.Owin.Host.SystemWeb
    new: null
    confidence: 100
    notes: ASP.NET Core has built-in hosting
```

## Migration Workflow

1. **Analysis Phase**
   ```csharp
   var hubAnalyzer = new SignalRHubAnalyzer();
   var hubs = await hubAnalyzer.AnalyzeHubsAsync(projectPath);

   var startupAnalyzer = new SignalRStartupAnalyzer();
   var startupInfo = await startupAnalyzer.AnalyzeStartupAsync(projectPath);

   var globalHostAnalyzer = new GlobalHostAnalyzer();
   var globalHostUsages = await globalHostAnalyzer.AnalyzeGlobalHostUsageAsync(projectPath);
   ```

2. **Transformation Phase**
   ```csharp
   var hubTransformer = new SignalRHubTransformer();
   foreach (var hub in hubs)
   {
       await hubTransformer.TransformHubLifecycleAsync(hub);
       await hubTransformer.TransformClientInvocationsAsync(hub);
       await hubTransformer.TransformGroupOperationsAsync(hub);
   }

   var startupTransformer = new SignalRStartupTransformer();
   await startupTransformer.TransformStartupConfigurationAsync(startupInfo, hubs);

   var globalHostTransformer = new GlobalHostTransformer();
   foreach (var usage in globalHostUsages)
   {
       await globalHostTransformer.TransformGlobalHostUsageAsync(usage);
   }
   ```

3. **Generation Phase**
   ```csharp
   var jsGenerator = new SignalRJavaScriptGenerator();
   var packageJson = jsGenerator.GeneratePackageJsonUpdates();

   foreach (var hub in hubs)
   {
       var tsInterface = jsGenerator.GenerateTypeScriptHubInterface(hub);
       await File.WriteAllTextAsync($"{hub.ClassName}.d.ts", tsInterface);
   }
   ```

## Integration with NetLift

### Add to MigrationOrchestrator
```csharp
// After WCF migration, before validation
if (options.MigrateSignalR)
{
    await MigrateSignalRAsync(migrationContext, cancellationToken);
}
```

### Add to Analysis Report
```csharp
report.SignalRAnalysis = new SignalRAnalysis
{
    TotalHubs = hubs.Count,
    HubsWithLifecycleMethods = hubs.Count(h => h.LifecycleMethods.HasOnConnected ||
                                                h.LifecycleMethods.HasOnDisconnected),
    TotalClientInvocations = hubs.Sum(h => h.ClientInvocations.Count),
    GlobalHostUsages = globalHostUsages.Count,
    JavaScriptClientsDetected = jsFiles.Count
};
```

## Sources

- [Differences between SignalR and ASP.NET Core SignalR | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/signalr/version-differences?view=aspnetcore-8.0)
- [Understanding and Handling Connection Lifetime Events in SignalR | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/signalr/overview/guide-to-the-api/handling-connection-lifetime-events)
- [ASP.NET Core SignalR JavaScript client | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-9.0)
- [SignalR HubContext | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubcontext?view=aspnetcore-10.0)
- [Use hubs in ASP.NET Core SignalR | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-7.0)
- [SignalR JavaScript client changing package name to @microsoft/signalr · Issue #372 · aspnet/Announcements](https://github.com/aspnet/Announcements/issues/372)

## Next Steps

1. Create models in `NetLift.Core/Models/SignalR/`
2. Create interfaces in `NetLift.Core/Interfaces/`
3. Implement analyzers in `NetLift.Transforms/SignalR/Analyzers/`
4. Implement transformers in `NetLift.Transforms/SignalR/Transformers/`
5. Implement generators in `NetLift.Transforms/SignalR/Generators/`
6. Write comprehensive tests for each component
7. Update package-mappings.yml
8. Integrate with migration orchestrator
9. Add to HTML report generator
10. Update CLAUDE.md with SignalR migration details
