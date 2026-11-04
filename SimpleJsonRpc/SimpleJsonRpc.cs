using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using SimpleJsonRpc.Data;
using SimpleJsonRpc.Delegates;
using SimpleJsonRpc.Exceptions;

namespace SimpleJsonRpc;

public class SimpleJsonRpc : IAsyncDisposable
{
    private static readonly string JsonRpcVersion = "2.0";

    public Task Completion { get; }
    public bool IsStarted { get; private set; }
    public bool IsDisposed { get; private set; }

    public event EventHandler<EventArgs>? Disconnected;

    private readonly ConcurrentDictionary<int, PendingRequest> _requests = new();
    private int _id = -1;

    private readonly ConcurrentDictionary<string, JsonRpcMethod> _methods = new();
    private readonly Stream _readStream;
    private readonly Stream _writeStream;
    private readonly TaskCompletionSource _completionSource = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly byte[] _readBuffer = new byte[1024];
    private readonly ArrayBufferWriter<byte> _buffer = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    internal SimpleJsonRpc() : this(new MemoryStream())
    {
    }

    public SimpleJsonRpc(Stream stream) : this(stream, stream)
    {
    }

    public SimpleJsonRpc(Stream readStream, Stream writeStream)
    {
        _readStream = readStream;
        _writeStream = writeStream;

        Completion = _completionSource.Task;
    }

    public void StartListening()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(SimpleJsonRpc));
        if (IsStarted) throw new InvalidOperationException("Already listening");
        IsStarted = true;
        Task.Run(ListenAsync);
    }

    public async Task<T> Request<T>(string methodName, object? param = null, CancellationToken cancellationToken = default)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(SimpleJsonRpc));
        if (!IsStarted) throw new InvalidOperationException("Not listening yet, call StartListening() first");
        var id = Interlocked.Increment(ref _id);
        PendingRequest<T> pendingRequest = new();
        _requests.TryAdd(id, pendingRequest);
        try
        {
            await SendRequest(methodName, param, id, cancellationToken).ConfigureAwait(false);
            return await pendingRequest.CompletionSource.Task.ConfigureAwait(false);
        }
        finally
        {
            _requests.TryRemove(id, out _);
        }
    }

    public async Task Notify(string methodName, object? param = null, CancellationToken cancellationToken = default)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(SimpleJsonRpc));
        if (!IsStarted) throw new InvalidOperationException("Not listening yet, call StartListening() first");
        await SendRequest(methodName, param, cts: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Register a JSON-RPC method.
    /// <list type="bullet">
    ///   <item><description>Delegate must have at most 1 parameter.</description></item>
    ///   <item><description>If parameter exists it must be a json deserializable type</description></item>
    ///   <item><description>If return exists it must be a json serializable type.</description></item>
    /// </list>
    /// <b>Serialization capabilities are not checked at registration!</b>
    /// </summary>
    /// <param name="methodName"> The method's name.</param>
    /// <param name="delegate">
    /// The delegate to invoke. If it returns a <see cref="Task"/>, <see cref="Task{TResult}"/>,
    /// <see cref="ValueTask"/> or <see cref="ValueTask{TResult}"/>, the result is awaited.
    /// </param>
    /// <exception cref="InvalidOperationException">Delegate has more than one parameter</exception>
    public void RegisterMethod(string methodName, Delegate @delegate)
    {
        ParameterInfo[] parameters = @delegate.Method.GetParameters();
        if (parameters.Length > 1)
            throw new InvalidOperationException("Delegate must have 1 or 0 parameters");
        Type? paramsType = parameters.Length == 1 ? parameters[0].ParameterType : null;
        _methods[methodName] = new() { Name = methodName, Delegate = @delegate, ParamsType = paramsType };
    }

    public async ValueTask Shutdown(string reason = "")
    {
        if (IsDisposed) return;
        IsDisposed = true;
        await _cts.CancelAsync().ConfigureAwait(false);
        await _readStream.DisposeAsync().ConfigureAwait(false);
        await _writeStream.DisposeAsync().ConfigureAwait(false);
        foreach (var request in _requests.Values)
        {
            request.SetException(new ObjectDisposedException(nameof(SimpleJsonRpc)));
        }

        _completionSource.SetResult();

        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    protected async Task ListenAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await _readStream.ReadAsync(_readBuffer, _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Shutdown("Read exception").ConfigureAwait(false);
                return;
            }

            if (read == 0)
            {
                await Shutdown("").ConfigureAwait(false);
                return;
            }

            int startIndex = 0;
            int index;
            while (startIndex < read && (index = Array.IndexOf(_readBuffer, (byte)'\n', startIndex, read - startIndex)) >= 0)
            {
                _buffer.Write(new ReadOnlySpan<byte>(_readBuffer, startIndex, index - startIndex));
                startIndex = index + 1;
                await HandleMessage(_buffer.WrittenMemory).ConfigureAwait(false);
                _buffer.Clear();
            }

            if (startIndex < read) _buffer.Write(new ReadOnlySpan<byte>(_readBuffer, startIndex, read - startIndex));
        }
    }

    protected async Task HandleMessage(ReadOnlyMemory<byte> body)
    {
        JsonElement request;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            request = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            await SendError(new(JsonRpcErrorCodes.ParseError, "Invalid json")).ConfigureAwait(false);
            return;
        }

        if (request.ValueKind == JsonValueKind.Array)
        {
            await HandleBatch(request).ConfigureAwait(false);
        }

        if (request.ValueKind != JsonValueKind.Object)
        {
            await SendError(new(JsonRpcErrorCodes.ParseError, "Value is not an object or array")).ConfigureAwait(false);
            return;
        }

        JsonElement? id = GetId(request);
        // generic validation
        var error = ValidateRequest(request);
        if (error != null)
        {
            await SendError(error, id).ConfigureAwait(false);
            return;
        }

        if (request.TryGetProperty("error", out _) || request.TryGetProperty("result", out _))
        {
            HandleResponse(request, id);
        }
        else
        {
            await HandleRequest(request, id).ConfigureAwait(false);
        }
    }

    protected async Task HandleRequest(JsonElement request, JsonElement? id)
    {
        // get method
        if (!(request.TryGetProperty("method", out var methodElement) && methodElement.ValueKind == JsonValueKind.String && _methods.ContainsKey(methodElement.GetString()!)))
        {
            await SendErrorResponse(new(JsonRpcErrorCodes.MethodNotFound, "Method not found"), id).ConfigureAwait(false);
            return;
        }

        JsonRpcMethod method = _methods[request.GetProperty("method").GetString()!];
        object?[] paramArray = [];
        if (method.ParamsType != null)
        {
            if (!request.TryGetProperty("params", out var parameters))
            {
                await SendErrorResponse(new(JsonRpcErrorCodes.InvalidParams, "Invalid params"), id).ConfigureAwait(false);
                return;
            }

            try
            {
                paramArray = [parameters.Deserialize(method.ParamsType)];
            }
            catch (JsonException)
            {
                await SendErrorResponse(new(JsonRpcErrorCodes.InvalidParams, "Failed to parse params"), id).ConfigureAwait(false);
                return;
            }
        }

        object? result;
        try
        {
            result = await DelegateInvoker.InvokeAsync(method.Delegate, paramArray);
        }
        catch (JsonRpcRequestException ex)
        {
            try
            {
                await SendErrorResponse(ex.Error, id).ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
                await SendErrorResponse(new(JsonRpcErrorCodes.InternalError, "Failed to serialize application error"), id).ConfigureAwait(false);
            }

            return;
        }
        catch (Exception ex)
        {
            await SendErrorResponse(new(JsonRpcErrorCodes.InternalError, "Failed to execute method"), id).ConfigureAwait(false);
            return;
        }

        if (id == null) return;
        try
        {
            await SendResponse(result, id).ConfigureAwait(false);
        }
        catch (NotSupportedException exception)
        {
            await SendErrorResponse(new(JsonRpcErrorCodes.InternalError, "Failed to serialize response"), id).ConfigureAwait(false);
        }
    }

    protected void HandleResponse(JsonElement response, JsonElement? id)
    {
        if (id == null) return;
        if (id.Value.ValueKind != JsonValueKind.Number) return;
        if (!_requests.TryGetValue(id.Value.GetInt32(), out var pendingRequest)) return;
        if (response.TryGetProperty("result", out var resultElement))
        {
            try
            {
                pendingRequest.SetResult(resultElement.Deserialize(pendingRequest.ResponseType));
            }
            catch (Exception ex)
            {
                pendingRequest.SetException(ex);
            }
        }
        else if (response.TryGetProperty("error", out var errorElement))
        {
            JsonRpcError<JsonElement?>? error = null;
            try
            {
                error = errorElement.Deserialize<JsonRpcError<JsonElement?>>();
            }
            catch (Exception ex)
            {
                // ignored
            }

            if (error != null) pendingRequest.SetException(new JsonRpcResponseException(error));
            else pendingRequest.SetException(new JsonRpcInvalidResponseException(response));
        }
        else
        {
            throw new InvalidOperationException();
        }
    }

    protected async Task HandleBatch(JsonElement request)
    {
    }

    private JsonRpcError? ValidateRequest(JsonElement request)
    {
        if (!(request.TryGetProperty("jsonrpc", out var jsonrpc) && jsonrpc.ValueKind == JsonValueKind.String && jsonrpc.GetString() == JsonRpcVersion))
            return new(JsonRpcErrorCodes.InvalidRequest, "Invalid jsonrpc version");
        if (!(!request.TryGetProperty("id", out _) || GetId(request) != null))
            return new(JsonRpcErrorCodes.InvalidRequest, "Invalid id value");
        return null;
    }

    private JsonElement? GetId(JsonElement request)
    {
        if (!(request.TryGetProperty("id", out var id) && id.ValueKind is (JsonValueKind.Null or JsonValueKind.Number or JsonValueKind.String)))
            return null;
        return id;
    }

    protected async Task SendErrorResponse(JsonRpcError error, JsonElement? id = null)
    {
        if (id != null) await SendError(error, id).ConfigureAwait(false);
    }

    protected async Task SendError(JsonRpcError error, JsonElement? id = null)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = JsonRpcVersion,
            ["error"] = JsonSerializer.SerializeToNode(error, error.GetType()),
            ["id"] = id != null ? JsonNode.Parse(id.Value.GetRawText()) : null
        };

        await Send(message, _cts.Token).ConfigureAwait(false);
    }

    protected async Task SendResponse(object? result, JsonElement? id = null)
    {
        if (id == null) return;
        var message = new JsonObject
        {
            ["jsonrpc"] = JsonRpcVersion,
            ["result"] = JsonSerializer.SerializeToNode(result),
            ["id"] = JsonNode.Parse(id.Value.GetRawText())
        };

        await Send(message, _cts.Token).ConfigureAwait(false);
    }

    protected async Task SendRequest(string method, object? param, int? id = null, CancellationToken cts = default)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = JsonRpcVersion,
            ["method"] = method,
        };
        if (param != null) message["params"] = JsonSerializer.SerializeToNode(param);
        if (id != null) message["id"] = id;

        await Send(message, cts).ConfigureAwait(false);
    }

    protected async Task Send(JsonObject message, CancellationToken cts = default)
    {
        MemoryStream ms = new();
        await JsonSerializer.SerializeAsync(
            ms,
            message,
            cancellationToken: cts
        );
        ms.Write("\n"u8);

        ms.Position = 0;
        using var l = _writeLock.WaitAsync(cts);
        try
        {
            await ms.CopyToAsync(_writeStream, cts).ConfigureAwait(false);
            await _writeStream.FlushAsync(cts).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Shutdown("Write exception").ConfigureAwait(false);
            return;
        }
        finally
        {
            _writeLock.Release();
        }
    }


    public async ValueTask DisposeAsync()
    {
        await Shutdown("Disposed").ConfigureAwait(false);
    }
}