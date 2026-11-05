# SimpleJsonRpc

A standalone mostly correct .NET Stream based [JSON-RPC 2.0](https://www.jsonrpc.org/specification) protocol
implementation.

## Differences

* Supports any JSON type in `params` field of a Request object instead of just structured types.
* No support for Batch requests.

## Example

### Server examples

```csharp
int Sum(TwoNumbers numbers) => numbers.A + numbers.B;

JsonRpc serverRpc = new(serverStream);
serverRpc.RegisterMethod("sum", Sum);
serverRpc.StartListening();
await serverRpc.Completion;
```

### Client example

```csharp
JsonRpc clientRpc = new(clientStream);
clientRpc.StartListening();
await clientRpc.Request<int>("sum", new TwoNumbers(1, 1)); // returns 2
```

## Transport frame

The default transport frame
is [JsonRpcNewLineDelimiterFrameHandler](SimpleJsonRpc/Frames/JsonRpcNewLineDelimiterFrameHandler.cs) which splits
messages by a `\n` symbol delimiter. Custom frame handlers can be defined by
implementing [IJsonRpcFrameHandler](SimpleJsonRpc/Frames/IJsonRpcFrameHandler.cs) and passing it in the constructor.

## Serialization

The implementation uses `System.Text.Json` to serialize and deserialize, it can be configured through the
`SimpleJsonRpc.JsonOptions` property.

## Server error handling

The server emits `JsonRpc.Error` when various errors happen.