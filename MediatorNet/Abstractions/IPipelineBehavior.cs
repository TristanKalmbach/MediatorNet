namespace MediatorNet.Abstractions;

/// <summary>
/// Pipeline behavior to surround the inner handler.
/// Implementations add additional behavior and await the next delegate.
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Pipeline handler. Implement to surround the inner handler.
    /// </summary>
    /// <param name="request">Request</param>
    /// <param name="next">Next pipeline behavior or request handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response from the pipeline</returns>
    ValueTask<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an async continuation for the next task to execute in the pipeline
/// </summary>
/// <typeparam name="TResponse">Response type</typeparam>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();