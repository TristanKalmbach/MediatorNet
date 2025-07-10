namespace MediatorNet.Implementation;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Optimized implementation of <see cref="IMediator"/> using compiled expression trees for maximum performance
/// </summary>
public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    
    // Cache for handler types to avoid expensive reflection lookups per request
    private static readonly ConcurrentDictionary<Type, Type> _handlerCache = new();
    
    // Cache for compiled delegates to avoid reflection and expression compilation on every call
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, object, CancellationToken, ValueTask<object>>> _compiledRequestHandlers = new();
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, object, CancellationToken, ValueTask>> _compiledVoidHandlers = new();
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, object, CancellationToken, object>> _compiledStreamHandlers = new();

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
            
            handlerDelegate = async () => await currentBehavior.HandleAsync(request, previousHandlerDelegate, cancellationToken);
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
        RequestHandlerDelegate<Unit> handlerDelegate = async () => await ExecuteRequestHandlerAsync(request, cancellationToken);
        
        // Execute the pipeline behaviors in sequence
        foreach (var behavior in pipelineBehaviors.Reverse())
        {
            var currentBehavior = behavior;
            var previousHandlerDelegate = handlerDelegate;
            
            handlerDelegate = async () => await currentBehavior.HandleAsync(request, previousHandlerDelegate, cancellationToken);
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
        
        // Use compiled delegate for maximum performance
        var compiledHandler = GetOrCreateCompiledStreamHandler<TResponse>(requestType);
        var enumerable = (IAsyncEnumerable<TResponse>)compiledHandler(_serviceProvider, request, cancellationToken);
            
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
        
        // Use compiled delegate for maximum performance
        var compiledHandler = GetOrCreateCompiledRequestHandler<TResponse>(requestType);
        var result = await compiledHandler(_serviceProvider, request, cancellationToken);
        return (TResponse)result;
    }
    
    private async ValueTask<Unit> ExecuteRequestHandlerAsync(
        IRequest request,
        CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        
        // Use compiled delegate for maximum performance
        var compiledHandler = GetOrCreateCompiledVoidHandler(requestType);
        await compiledHandler(_serviceProvider, request, cancellationToken);
        return Unit.Value;
    }

    private static Func<IServiceProvider, object, CancellationToken, ValueTask<object>> GetOrCreateCompiledRequestHandler<TResponse>(Type requestType)
    {
        return _compiledRequestHandlers.GetOrAdd(requestType, static requestType =>
        {
            var responseType = typeof(TResponse);
            var handlerInterfaceType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
            var handlerMethod = handlerInterfaceType.GetMethod("HandleAsync")!;

            // Create expression tree: (serviceProvider, request, cancellationToken) => 
            //   ((IRequestHandler<TRequest, TResponse>)serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>())
            //     .HandleAsync((TRequest)request, cancellationToken)
            
            var serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var requestParam = Expression.Parameter(typeof(object), "request");
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
            
            // Get the GetRequiredService method
            var getRequiredServiceMethod = typeof(ServiceProviderServiceExtensions)
                .GetMethods()
                .First(m => m.Name == "GetRequiredService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
                .MakeGenericMethod(handlerInterfaceType);
            
            // serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>()
            var getHandlerCall = Expression.Call(
                getRequiredServiceMethod,
                serviceProviderParam);
            
            // (TRequest)request
            var castRequest = Expression.Convert(requestParam, requestType);
            
            // handler.HandleAsync((TRequest)request, cancellationToken)
            var handleAsyncCall = Expression.Call(
                getHandlerCall,
                handlerMethod,
                castRequest,
                cancellationTokenParam);
            
            // Convert result to ValueTask<object>
            var valueTaskType = typeof(ValueTask<>).MakeGenericType(responseType);
            var resultConverterMethod = typeof(Mediator).GetMethod("ConvertToObjectValueTask", BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(responseType);
            
            var convertCall = Expression.Call(
                resultConverterMethod,
                handleAsyncCall);
            
            var lambda = Expression.Lambda<Func<IServiceProvider, object, CancellationToken, ValueTask<object>>>(
                convertCall,
                serviceProviderParam,
                requestParam,
                cancellationTokenParam);
            
            return lambda.Compile();
        });
    }

    private static Func<IServiceProvider, object, CancellationToken, ValueTask> GetOrCreateCompiledVoidHandler(Type requestType)
    {
        return _compiledVoidHandlers.GetOrAdd(requestType, static requestType =>
        {
            var handlerInterfaceType = typeof(IRequestHandler<>).MakeGenericType(requestType);
            var handlerMethod = handlerInterfaceType.GetMethod("HandleAsync")!;

            var serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var requestParam = Expression.Parameter(typeof(object), "request");
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
            
            var getRequiredServiceMethod = typeof(ServiceProviderServiceExtensions)
                .GetMethods()
                .First(m => m.Name == "GetRequiredService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
                .MakeGenericMethod(handlerInterfaceType);
            
            var getHandlerCall = Expression.Call(
                getRequiredServiceMethod,
                serviceProviderParam);
            
            var castRequest = Expression.Convert(requestParam, requestType);
            
            var handleAsyncCall = Expression.Call(
                getHandlerCall,
                handlerMethod,
                castRequest,
                cancellationTokenParam);
            
            var lambda = Expression.Lambda<Func<IServiceProvider, object, CancellationToken, ValueTask>>(
                handleAsyncCall,
                serviceProviderParam,
                requestParam,
                cancellationTokenParam);
            
            return lambda.Compile();
        });
    }

    private static Func<IServiceProvider, object, CancellationToken, object> GetOrCreateCompiledStreamHandler<TResponse>(Type requestType)
    {
        return _compiledStreamHandlers.GetOrAdd(requestType, static requestType =>
        {
            var responseType = typeof(TResponse);
            var handlerInterfaceType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, responseType);
            var handlerMethod = handlerInterfaceType.GetMethod("HandleAsync")!;

            var serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var requestParam = Expression.Parameter(typeof(object), "request");
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
            
            var getRequiredServiceMethod = typeof(ServiceProviderServiceExtensions)
                .GetMethods()
                .First(m => m.Name == "GetRequiredService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
                .MakeGenericMethod(handlerInterfaceType);
            
            var getHandlerCall = Expression.Call(
                getRequiredServiceMethod,
                serviceProviderParam);
            
            var castRequest = Expression.Convert(requestParam, requestType);
            
            var handleAsyncCall = Expression.Call(
                getHandlerCall,
                handlerMethod,
                castRequest,
                cancellationTokenParam);
            
            var lambda = Expression.Lambda<Func<IServiceProvider, object, CancellationToken, object>>(
                handleAsyncCall,
                serviceProviderParam,
                requestParam,
                cancellationTokenParam);
            
            return lambda.Compile();
        });
    }

    // Helper method to convert ValueTask<T> to ValueTask<object>
    private static async ValueTask<object> ConvertToObjectValueTask<T>(ValueTask<T> valueTask)
    {
        var result = await valueTask;
        return result!;
    }
}