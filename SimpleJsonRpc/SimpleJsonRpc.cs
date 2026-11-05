using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SimpleJsonRpc.Data;
using SimpleJsonRpc.Delegates;
using SimpleJsonRpc.Exceptions;
using SimpleJsonRpc.Frames;

namespace SimpleJsonRpc;

/// <summary>
/// A joint server and client implementation of the JSON-RPC protocol
/// </summary>
public class SimpleJsonRpc : IAsyncDisposable
{
    private static readonly string JsonRpcVersion = "2.0";
    /// <summary>
    /// A task that completes when the instance is shutdown, returns the reason.
    /// </summary>
    public Task<JsonRpcShutdownReason> Completion { get; }
    /// <summary>
    /// Has the instance been started by a call to <see cref="StartListening"/>.
    /// </summary>
    public bool IsStarted { get; private set; }
    /// <summary>
    /// Has the instance been disposed, could be set because of a call to <see cref="DisposeAsync"/> or by a communication error.
    /// </summary>
    public bool IsDisposed { get; private set; }
    /// <summary>
    /// <see cref="JsonSerializerOptions"/> passed to all serialization and deserialization calls.
    /// </summary>
    public JsonSerializerOptions JsonOptions => _options;

    private readonly ConcurrentDictionary<int, PendingRequest> _requests = new();
    private int _id = -1;

    private readonly ConcurrentDictionary<string, JsonRpcMethod> _methods = new();
    private readonly Stream _readStream;
    private readonly Stream _writeStream;
    private readonly TaskCompletionSource<JsonRpcShutdownReason> _completionSource = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _options = new();
    private readonly IJsonRpcFrameHandler _frameHandler;

    internal SimpleJsonRpc() : this(new MemoryStream())
    {
    }

    /// <summary>
    /// Instantiate an instance with one stream for both listening and writing.
    /// </summary>
    /// <param name="stream">Stream to use for listening and writing.</param>
    /// <param name="frameHandler">Handler to use for message framing, defaults to <see cref="JsonRpcNewLineDelimiterFrameHandler"/>.</param>
    public SimpleJsonRpc(Stream stream, IJsonRpcFrameHandler? frameHandler = null) : this(stream, stream, frameHandler)
    {
    }

    /// <summary>
    /// Instantiate an instance with separate streams for listening and writing.
    /// </summary>
    /// <param name="readStream">Stream to use for listening.</param>
    /// <param name="writeStream">Stream to use for writing.</param>
    /// <param name="frameHandler">Handler to use for message framing, defaults to <see cref="JsonRpcNewLineDelimiterFrameHandler"/>.</param>
    public SimpleJsonRpc(Stream readStream, Stream writeStream, IJsonRpcFrameHandler? frameHandler = null)
    {
        _readStream = readStream;
        _writeStream = writeStream;
        _frameHandler = frameHandler ?? new JsonRpcNewLineDelimiterFrameHandler();
        Completion = _completionSource.Task;
    }

    /// <summary>
    /// Starts the listen task
    /// </summary>
    /// <exception cref="InvalidOperationException">Method has been called multiple times.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public void StartListening()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(SimpleJsonRpc));
        if (IsStarted) throw new InvalidOperationException("Already listening");
        IsStarted = true;
        Task.Run(ListenAsync);
    }

    /// <summary>
    /// Submit a request. Task completes once the server's response gets processed, if the server never responds or
    /// the response is malformed in a way that it can't be attributed to the request the task will never complete.
    /// </summary>
    /// <param name="methodName"> The method's name.</param>
    /// <param name="param">An optional object that will be serialized as the request's "params" field.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. Only response await can be canceled. The default value is <see cref="CancellationToken.None">None</see>.</param>
    /// <exception cref="InvalidOperationException">Method has been called before <see cref="StartListening"/></exception>
    /// <exception cref="NotSupportedException">There is no compatible JsonConverter for <paramref name="param"/> or its serializable members.</exception>
    /// <exception cref="JsonRpcResponseException">The server returned an error.</exception>
    /// <exception cref="JsonRpcInvalidResponseException">The server returned an invalid response.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled. This exception is stored into the returned task.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public async Task Request(string methodName, object? param = null, CancellationToken cancellationToken = default)
    {
        await Request<object>(methodName, param, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="Request(string, object, CancellationToken)" />
    /// <typeparam name="TResult">Method's response type</typeparam>
    /// <exception cref="NotSupportedException">There is no compatible JsonConverter for <typeparamref name="TResult"/> or its serializable members.</exception>
    /// <exception cref="JsonException"><typeparamref name="TResult"/> is not compatible with the JSON.</exception>
    public async Task<TResult> Request<TResult>(string methodName, object? param = null, CancellationToken cancellationToken = default)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(SimpleJsonRpc));
        if (!IsStarted) throw new InvalidOperationException("Not listening yet, call StartListening() first");
        var id = Interlocked.Increment(ref _id);
        PendingRequest<TResult> pendingRequest = new();
        _requests.TryAdd(id, pendingRequest);
        try
        {
            await SendRequest(methodName, param, id).ConfigureAwait(false);
            cancellationToken.Register(() => pendingRequest.CompletionSource.TrySetCanceled(cancellationToken));
            return await pendingRequest.CompletionSource.Task.ConfigureAwait(false);
        }
        finally
        {
            _requests.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Submit a notification request. Task completes when the requests gets dispatched.
    /// </summary>
    /// <param name="methodName"> The method's name.</param>
    /// <param name="param">An optional object that will be serialized as the request's "params" field.</param>
    /// <exception cref="InvalidOperationException">Method has been called before <see cref="StartListening"/></exception>
    /// <exception cref="NotSupportedException">There is no compatible JsonConverter for <paramref name="param"/> or its serializable members.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled. This exception is stored into the returned task.</exception>
    /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
    public async Task Notify(string methodName, object? param = null)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(SimpleJsonRpc));
        if (!IsStarted) throw new InvalidOperationException("Not listening yet, call StartListening() first");
        await SendRequest(methodName, param).ConfigureAwait(false);
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
        _methods[methodName] = new(methodName, @delegate, paramsType);
    }

    /// <summary>
    /// Stops listening, disposes the backing stream and faults all pending requests with <see cref="ObjectDisposedException"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Shutdown(JsonRpcShutdownType.Disposed).ConfigureAwait(false);
    }

    protected async ValueTask Shutdown(JsonRpcShutdownType reason, Exception? exception = null)
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

        _completionSource.SetResult(new JsonRpcShutdownReason(reason, exception));
    }

    protected async Task ListenAsync()
    {
        try
        {
            byte[] readBuffer = new byte[1024];
            while (!_cts.IsCancellationRequested)
            {
                int read = await _readStream.ReadAtLeastAsync(readBuffer, 1, false, _cts.Token).ConfigureAwait(false);
                _frameHandler.IngestData(readBuffer, read);
                while (_frameHandler.TryGetMessage(out var message)) await HandleMessage(message).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await Shutdown(JsonRpcShutdownType.ListeningError, ex).ConfigureAwait(false);
        }
    }

    protected async Task HandleMessage(byte[] body)
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
            await SendError(new(JsonRpcErrorCodes.InternalError, "Batches are not supported by this server")).ConfigureAwait(false);
            return;
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
                paramArray = [parameters.Deserialize(method.ParamsType, options: _options)];
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
        catch (Exception)
        {
            await SendErrorResponse(new(JsonRpcErrorCodes.InternalError, "Failed to execute method"), id).ConfigureAwait(false);
            return;
        }

        if (id == null) return;
        try
        {
            await SendResponse(result, id).ConfigureAwait(false);
        }
        catch (NotSupportedException)
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
                pendingRequest.SetResult(resultElement.Deserialize(pendingRequest.ResponseType, options: _options));
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
                error = errorElement.Deserialize<JsonRpcError<JsonElement?>>(options: _options);
            }
            catch (Exception)
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
            ["error"] = JsonSerializer.SerializeToNode(error, error.GetType(), options: _options),
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
            ["result"] = JsonSerializer.SerializeToNode(result, options: _options),
            ["id"] = JsonNode.Parse(id.Value.GetRawText())
        };

        await Send(message, _cts.Token).ConfigureAwait(false);
    }

    protected async Task SendRequest(string method, object? param, int? id = null)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = JsonRpcVersion,
            ["method"] = method,
        };
        if (param != null) message["params"] = JsonSerializer.SerializeToNode(param, options: _options);
        if (id != null) message["id"] = id;

        await Send(message, _cts.Token).ConfigureAwait(false);
    }

    protected async Task Send(JsonObject message, CancellationToken cts = default)
    {
        byte[] data = _frameHandler.FrameMessage(
            Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(
                    message,
                    options: _options
                )));

        await _writeLock.WaitAsync(cts).ConfigureAwait(false);
        try
        {
            await _writeStream.WriteAsync(data, cts).ConfigureAwait(false);
            await _writeStream.FlushAsync(cts).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Shutdown(JsonRpcShutdownType.WritingError, ex).ConfigureAwait(false);
            return;
        }
        finally
        {
            _writeLock.Release();
        }
    }
}