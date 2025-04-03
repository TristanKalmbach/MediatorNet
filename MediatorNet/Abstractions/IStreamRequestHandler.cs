namespace MediatorNet.Abstractions;

/// <summary>
/// Defines a handler for a streaming request
/// </summary>
/// <typeparam name="TRequest">The type of request being handled</typeparam>
/// <typeparam name="TResponse">The type of response items from the handler</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles a streaming request
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An asynchronous stream of response items</returns>
    IAsyncEnumerable<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}