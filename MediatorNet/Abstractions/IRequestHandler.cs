namespace MediatorNet.Abstractions;

/// <summary>
/// Defines a handler for a request with no response
/// </summary>
/// <typeparam name="TRequest">The type of request being handled</typeparam>
public interface IRequestHandler<in TRequest> 
    where TRequest : IRequest
{
    /// <summary>
    /// Handles a request
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    ValueTask HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a handler for a request with a response
/// </summary>
/// <typeparam name="TRequest">The type of request being handled</typeparam>
/// <typeparam name="TResponse">The type of response from the handler</typeparam>
public interface IRequestHandler<in TRequest, TResponse> 
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles a request
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response from the request</returns>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}