namespace MediatorNet.Implementation;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Default implementation of <see cref="IMediator"/>
/// </summary>
public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    
    // Cache for handler types to avoid expensive reflection lookups per request
    private static readonly ConcurrentDictionary<Type, Type> _handlerCache = new();

    /// <inheritdoc />
    public async ValueTask<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request, 
        CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        
        // Get the response type 
        var responseType = typeof(TResponse);
        
        // Get all pipeline behaviors for this request/response combination
        var pipelineBehaviors = _serviceProvider
            .GetServices<IPipelineBehavior<IRequest<TResponse>, TResponse>>()
            .ToArray();
        
        // Create request handler delegate that will process the actual handler
        RequestHandlerDelegate<TResponse> handlerDelegate = () => ExecuteRequestHandlerAsync(request, cancellationToken);
        
        // Execute the pipeline behaviors in sequence
        foreach (var behavior in pipelineBehaviors.Reverse())
        {
            var currentBehavior = behavior;
            var previousHandlerDelegate = handlerDelegate;
            
            handlerDelegate = () => currentBehavior.HandleAsync(request, previousHandlerDelegate, cancellationToken);
        }
        
        // Execute the request pipeline
        return await handlerDelegate();
    }

    /// <inheritdoc />
    public async ValueTask SendAsync(
        IRequest request, 
        CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        
        // Get all pipeline behaviors for this request/Unit combination
        var pipelineBehaviors = _serviceProvider
            .GetServices<IPipelineBehavior<IRequest, Unit>>()
            .ToArray();
        
        // Create request handler delegate that will process the actual handler
        RequestHandlerDelegate<Unit> handlerDelegate = () => ExecuteRequestHandlerAsync(request, cancellationToken);
        
        // Execute the pipeline behaviors in sequence
        foreach (var behavior in pipelineBehaviors.Reverse())
        {
            var currentBehavior = behavior;
            var previousHandlerDelegate = handlerDelegate;
            
            handlerDelegate = () => currentBehavior.HandleAsync(request, previousHandlerDelegate, cancellationToken);
        }
        
        // Execute the request pipeline
        await handlerDelegate();
    }

    /// <inheritdoc />
    public async ValueTask PublishAsync<TNotification>(
        TNotification notification, 
        CancellationToken cancellationToken = default) 
        where TNotification : INotification
    {
        // Get all notification handlers for this notification type
        var handlers = _serviceProvider
            .GetServices<INotificationHandler<TNotification>>()
            .ToArray();
        
        if (handlers.Length == 0)
        {
            return;
        }
        
        // Create task list to hold all notification handler tasks
        var tasks = new List<ValueTask>(handlers.Length);
        
        // Execute all notification handlers
        foreach (var handler in handlers)
        {
            tasks.Add(handler.HandleAsync(notification, cancellationToken));
        }
        
        // Wait for all handlers to complete
        foreach (var task in tasks)
        {
            await task;
        }
    }
    
    /// <inheritdoc />
    public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        
        // Get the handler type from cache or discover it
        var handlerType = _handlerCache.GetOrAdd(
            requestType, 
            static requestType => 
            {
                var responseType = typeof(TResponse);
                var handlerInterfaceType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, responseType);
                return handlerInterfaceType;
            });
        
        // Get the handler from the service provider
        var handler = _serviceProvider.GetService(handlerType) 
            ?? throw new InvalidOperationException($"Handler not found for stream request type {requestType.Name}");
        
        // Invoke the handler's method
        var method = handlerType.GetMethod("HandleAsync") 
            ?? throw new InvalidOperationException($"HandleAsync method not found on handler type {handlerType.Name}");
        
        var enumerable = (IAsyncEnumerable<TResponse>)method.Invoke(
            handler,
            new object[] { request, cancellationToken })!;
            
        await foreach (var item in enumerable.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
    
    private async ValueTask<TResponse> ExecuteRequestHandlerAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        
        // Get the handler type from cache or discover it
        var handlerType = _handlerCache.GetOrAdd(
            requestType, 
            static requestType => 
            {
                var handlerInterfaceType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
                return handlerInterfaceType;
            });
        
        // Get the handler from the service provider
        var handler = _serviceProvider.GetService(handlerType) 
            ?? throw new InvalidOperationException($"Handler not found for request type {requestType.Name}");
        
        // Invoke the handler's method
        var method = handlerType.GetMethod("HandleAsync") 
            ?? throw new InvalidOperationException($"HandleAsync method not found on handler type {handlerType.Name}");
        
        var result = await (ValueTask<TResponse>)method.Invoke(
            handler,
            new object[] { request, cancellationToken })!;
            
        return result;
    }
    
    private async ValueTask<Unit> ExecuteRequestHandlerAsync(
        IRequest request,
        CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        
        // Get the handler type from cache or discover it
        var handlerType = _handlerCache.GetOrAdd(
            requestType, 
            static requestType => 
            {
                var handlerInterfaceType = typeof(IRequestHandler<>).MakeGenericType(requestType);
                return handlerInterfaceType;
            });
        
        // Get the handler from the service provider
        var handler = _serviceProvider.GetService(handlerType) 
            ?? throw new InvalidOperationException($"Handler not found for request type {requestType.Name}");
        
        // Invoke the handler's method
        var method = handlerType.GetMethod("HandleAsync") 
            ?? throw new InvalidOperationException($"HandleAsync method not found on handler type {handlerType.Name}");
        
        await (ValueTask)method.Invoke(
            handler,
            new object[] { request, cancellationToken })!;
            
        return Unit.Value;
    }
}