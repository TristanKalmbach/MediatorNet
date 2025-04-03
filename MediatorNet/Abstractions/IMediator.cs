namespace MediatorNet.Abstractions;

/// <summary>
/// Mediator implementation for sending requests to handlers
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Sends a request to a single handler
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    /// <param name="request">Request object</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Response from the request handler</returns>
    ValueTask<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a request to a single handler that returns no response
    /// </summary>
    /// <param name="request">Request object</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Task representing the send operation</returns>
    ValueTask SendAsync(IRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Publishes a notification to multiple handlers
    /// </summary>
    /// <typeparam name="TNotification">Notification type</typeparam>
    /// <param name="notification">Notification object</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Task representing the publish operation</returns>
    ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
        
    /// <summary>
    /// Creates a stream from a request to a single handler
    /// </summary>
    /// <typeparam name="TResponse">Response item type</typeparam>
    /// <param name="request">Request object</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>A stream of response items from the request handler</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}