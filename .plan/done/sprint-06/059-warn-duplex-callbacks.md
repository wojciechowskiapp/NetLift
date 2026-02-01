# [TASK-059] Detect and Warn About Duplex/Callback Contracts

## Meta

| Field | Value |
|-------|-------|
| **Priority** | P2 |
| **Estimate** | S |
| **Sprint** | 6 |
| **Agent** | Claude Code |
| **Started** | 2026-02-01 |
| **Completed** | 2026-02-01 |

## Dependencies

- **Depends on:** TASK-051 (Parse ServiceContract)
- **Blocks:** None

---

## Description

Detect WCF duplex and callback contracts which cannot be automatically migrated to gRPC or REST. Generate warnings and migration guidance for developers to manually handle these patterns using modern alternatives like SignalR, gRPC streaming, or WebSockets.

---

## Acceptance Criteria

- [x] Identify `[ServiceContract(CallbackContract = typeof(...))]` attribute
- [x] Identify duplex binding configurations (wsDualHttpBinding, netTcpBinding with duplex)
- [x] Detect callback interface methods
- [x] Generate clear warning messages with affected service/method names
- [x] Generate migration guidance document for each duplex contract
- [x] Suggest modern alternatives (SignalR, gRPC streaming, WebSockets)
- [x] Include code examples for recommended migration paths
- [x] Unit tests for detection logic

---

## Technical Notes

### Input: WCF Duplex Contract

```csharp
// Callback interface
public interface IOrderCallback
{
    [OperationContract(IsOneWay = true)]
    void OnOrderStatusChanged(int orderId, OrderStatus newStatus);

    [OperationContract(IsOneWay = true)]
    void OnOrderShipped(int orderId, ShippingInfo shipping);
}

// Service contract with callback
[ServiceContract(CallbackContract = typeof(IOrderCallback))]
public interface IOrderService
{
    [OperationContract]
    void SubscribeToOrderUpdates(int orderId);

    [OperationContract]
    void UnsubscribeFromOrderUpdates(int orderId);

    [OperationContract]
    OrderDto GetOrder(int orderId);
}

// Service implementation using callback
public class OrderService : IOrderService
{
    public void SubscribeToOrderUpdates(int orderId)
    {
        var callback = OperationContext.Current.GetCallbackChannel<IOrderCallback>();
        _subscriptions.Add(orderId, callback);
    }
}
```

### Output: Warning Report

```markdown
# NetLift Migration Warning: Duplex/Callback Contracts Detected

## Summary

The following WCF services use duplex or callback contracts which cannot be
automatically migrated. Manual intervention is required.

---

## Affected Services

### IOrderService

**Location:** `LegacyApp.Services.IOrderService`
**Callback Contract:** `IOrderCallback`
**Binding:** `wsDualHttpBinding`

#### Callback Methods Detected:

| Method | Direction | Parameters |
|--------|-----------|------------|
| OnOrderStatusChanged | Server -> Client | orderId: int, newStatus: OrderStatus |
| OnOrderShipped | Server -> Client | orderId: int, shipping: ShippingInfo |

#### Duplex Operations:

| Method | Description |
|--------|-------------|
| SubscribeToOrderUpdates | Registers client for callbacks |
| UnsubscribeFromOrderUpdates | Unregisters client from callbacks |

---

## Recommended Migration Paths

### Option 1: SignalR (Recommended for Web Clients)

SignalR provides real-time bidirectional communication over WebSockets with
automatic fallback to other transports.

```csharp
// OrderHub.cs
public class OrderHub : Hub
{
    public async Task SubscribeToOrder(int orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");
    }

    public async Task UnsubscribeFromOrder(int orderId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order-{orderId}");
    }
}

// Sending notifications
public class OrderNotificationService
{
    private readonly IHubContext<OrderHub> _hubContext;

    public async Task NotifyOrderStatusChanged(int orderId, OrderStatus status)
    {
        await _hubContext.Clients.Group($"order-{orderId}")
            .SendAsync("OnOrderStatusChanged", orderId, status);
    }
}
```

### Option 2: gRPC Server Streaming

For .NET-to-.NET communication, gRPC server streaming provides efficient
push notifications.

```protobuf
service OrderService {
  rpc SubscribeToOrderUpdates (SubscribeRequest) returns (stream OrderUpdate);
  rpc GetOrder (GetOrderRequest) returns (OrderResponse);
}

message OrderUpdate {
  int32 order_id = 1;
  oneof update {
    OrderStatusUpdate status_update = 2;
    ShippingUpdate shipping_update = 3;
  }
}
```

```csharp
public override async Task SubscribeToOrderUpdates(
    SubscribeRequest request,
    IServerStreamWriter<OrderUpdate> responseStream,
    ServerCallContext context)
{
    var channel = Channel.CreateUnbounded<OrderUpdate>();

    _subscriptionManager.Subscribe(request.OrderId, channel.Writer);

    try
    {
        await foreach (var update in channel.Reader.ReadAllAsync(context.CancellationToken))
        {
            await responseStream.WriteAsync(update);
        }
    }
    finally
    {
        _subscriptionManager.Unsubscribe(request.OrderId, channel.Writer);
    }
}
```

### Option 3: WebSocket API

For maximum control, implement a custom WebSocket endpoint.

```csharp
app.UseWebSockets();
app.Map("/ws/orders/{orderId}", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        await HandleOrderWebSocket(ws, orderId);
    }
});
```

---

## Migration Checklist

- [ ] Review all callback methods and their usage
- [ ] Choose appropriate modern technology (SignalR/gRPC streaming/WebSocket)
- [ ] Implement new real-time communication layer
- [ ] Update client applications to use new protocol
- [ ] Test bidirectional communication thoroughly
- [ ] Plan gradual migration with feature flags if needed
```

### Detection Implementation

```csharp
public class DuplexContractDetector
{
    public DuplexWarningReport Detect(WcfServiceContract contract, WcfConfiguration config)
    {
        var warnings = new List<DuplexWarning>();

        // Check for CallbackContract attribute
        if (contract.CallbackContract != null)
        {
            warnings.Add(new DuplexWarning
            {
                ServiceName = contract.InterfaceName,
                CallbackContract = contract.CallbackContract,
                CallbackMethods = ParseCallbackMethods(contract.CallbackContract),
                Severity = WarningSeverity.High
            });
        }

        // Check for duplex bindings
        var duplexBindings = config.Bindings
            .Where(b => IsDuplexBinding(b))
            .ToList();

        if (duplexBindings.Any())
        {
            warnings.Add(new DuplexWarning
            {
                ServiceName = contract.InterfaceName,
                DuplexBindings = duplexBindings,
                Severity = WarningSeverity.High
            });
        }

        return new DuplexWarningReport
        {
            Warnings = warnings,
            MigrationGuidance = GenerateMigrationGuidance(warnings)
        };
    }

    private bool IsDuplexBinding(WcfBinding binding)
    {
        return binding.Type switch
        {
            "wsDualHttpBinding" => true,
            "netTcpBinding" when binding.IsDuplex => true,
            _ => false
        };
    }

    private List<CallbackMethod> ParseCallbackMethods(Type callbackType)
    {
        return callbackType.GetMethods()
            .Where(m => m.GetCustomAttribute<OperationContractAttribute>() != null)
            .Select(m => new CallbackMethod
            {
                Name = m.Name,
                IsOneWay = m.GetCustomAttribute<OperationContractAttribute>()?.IsOneWay ?? false,
                Parameters = m.GetParameters().Select(p => new MethodParameter
                {
                    Name = p.Name,
                    Type = p.ParameterType.Name
                }).ToList()
            })
            .ToList();
    }
}
```

---

## Progress Log

| Timestamp | Agent | Update |
|-----------|-------|--------|
| 2025-01-31 | - | Created |
| 2026-02-01 | Claude Code | Completed implementation |

## Implementation Summary

**Files Created:**
- `F:\src\NetLift\src\NetLift.Core\Models\Wcf\DuplexWarningInfo.cs` - Models for duplex warnings
- `F:\src\NetLift\src\NetLift.Core\Models\Wcf\CommonModels.cs` - Shared MethodParameter model
- `F:\src\NetLift\src\NetLift.Core\Interfaces\IDuplexDetector.cs` - Interface definition
- `F:\src\NetLift\src\NetLift.Transforms\Wcf\Analyzers\DuplexDetector.cs` - Implementation
- `F:\src\NetLift\tests\NetLift.Tests.Unit\Transforms\Wcf\Analyzers\DuplexDetectorTests.cs` - 16 comprehensive unit tests

**Files Modified:**
- `F:\src\NetLift\src\NetLift.Cli\Program.cs` - Registered IDuplexDetector in DI container

**Test Results:**
- All 16 new tests passing
- Full test suite: 1,049 tests passing (100% success rate)

**Features Implemented:**
- Detects CallbackContract attribute on service interfaces (High severity)
- Detects duplex bindings (wsDualHttpBinding, netTcpBinding) (Medium severity)
- Parses callback methods from callback interface source code
- Generates comprehensive migration guidance markdown with:
  - SignalR option with code examples
  - gRPC server streaming option with code examples
  - WebSocket option with code examples
  - Comparison matrix of migration options
  - Migration checklist
- Proper diagnostic logging for troubleshooting
