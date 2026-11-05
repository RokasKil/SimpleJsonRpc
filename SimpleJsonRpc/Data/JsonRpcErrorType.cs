using SimpleJsonRpc.Exceptions;

namespace SimpleJsonRpc.Data;

/// <summary>
/// Possible error types emitted by <see cref="JsonRpc.Error"/>
/// </summary>
public enum JsonRpcErrorType
{
    /// <summary>
    /// An unexpected error
    /// </summary>
    InternalError,
    /// <summary>
    /// Received a server response with a recipient that could not be resolved.
    /// </summary>
    ResponseWithUnknownRecipient,
    /// <summary>
    /// Received a request with invalid method field.
    /// </summary>
    RequestWithInvalidMethodField,
    /// <summary>
    /// Received a request with an unknown method.
    /// </summary>
    RequestWithUnknownMethod,
    /// <summary>
    /// Received a request with invalid params.
    /// </summary>
    RequestWithInvalidParams,
    /// <summary>
    /// Processed a request and the registered method threw <see cref="JsonRpcRequestException"/> but the error serialization failed.
    /// </summary>
    RequestFailedToSerializeResponseError,
    /// <summary>
    /// Processed a request and the registered method returned successfully but the result serialization failed.
    /// </summary>
    RequestFailedToSerializeResponseResult,
    /// <summary>
    /// Processed a request and the registered method threw an unhandled exception.
    /// </summary>
    RequestFailedToExecuteMethod,
    /// <summary>
    /// Received an invalid request.
    /// </summary>
    RequestInvalid,
    /// <summary>
    /// Received a batch request which are unsupported.
    /// </summary>
    RequestUnsupportedBatch
}