namespace MediatorNet;

using System.Reflection;
using Abstractions;
using Behaviors;
using FluentValidation;
using Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Extension methods to configure MediatorNet services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MediatorNet core services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddMediatorNet(this IServiceCollection services)
    {
        // Register optimized mediator
        services.TryAddTransient<IMediator, Mediator>();
            
        return services;
    }
    
    /// <summary>
    /// Adds MediatorNet core services to the service collection and configures options
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure MediatorNet options</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddMediatorNet(
        this IServiceCollection services,
        Action<MediatorNetOptions> configureOptions)
    {
        var options = new MediatorNetOptions();
        configureOptions(options);
        
        // Register optimized mediator
        services.TryAddTransient<IMediator, Mediator>();
        
        // Apply options
        foreach (var behaviorType in options.Behaviors)
        {
            services.TryAddTransient(behaviorType);
        }
        
        return services;
    }
    
    /// <summary>
    /// Adds MediatorNet core services to the service collection and registers handlers from the specified assemblies
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assemblies">The assemblies to scan for handlers</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddMediatorNet(
        this IServiceCollection services, 
        params Assembly[] assemblies)
    {
        return services
            .AddMediatorNet()
            .AddHandlers(assemblies);
    }
    
    /// <summary>
    /// Registers request and notification handlers from the specified assemblies
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assemblies">The assemblies to scan for handlers</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddHandlers(
        this IServiceCollection services, 
        params Assembly[] assemblies)
    {
        return services
            .AddRequestHandlers(assemblies)
            .AddNotificationHandlers(assemblies)
            .AddStreamRequestHandlers(assemblies);
    }
    
    /// <summary>
    /// Registers request handlers from the specified assemblies
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assemblies">The assemblies to scan for handlers</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddRequestHandlers(
        this IServiceCollection services, 
        params Assembly[] assemblies)
    {
        // Register request handlers implementing IRequestHandler<TRequest,TResponse>
        var requestHandlersWithResponse = new List<(Type serviceType, Type implementationType)>();
        var requestHandlerTypes = new List<(Type serviceType, Type implementationType)>();
        
        // Find and register all handler implementations
        foreach (var assembly in assemblies)
        {
            foreach (var implementationType in assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface))
            {
                // Find all implemented interfaces that are IRequestHandler<,>
                foreach (var interfaceType in implementationType.GetInterfaces())
                {
                    // Handle IRequestHandler<TRequest, TResponse>
                    if (interfaceType.IsGenericType && 
                        interfaceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                    {
                        requestHandlersWithResponse.Add((interfaceType, implementationType));
                    }
                    // Handle IRequestHandler<TRequest>
                    else if (interfaceType.IsGenericType && 
                             interfaceType.GetGenericTypeDefinition() == typeof(IRequestHandler<>))
                    {
                        requestHandlerTypes.Add((interfaceType, implementationType));
                    }
                }
            }
        }
        
        // Register all discovered handlers
        foreach (var (serviceType, implementationType) in requestHandlersWithResponse)
        {
            services.TryAddTransient(serviceType, implementationType);
        }
        
        foreach (var (serviceType, implementationType) in requestHandlerTypes)
        {
            services.TryAddTransient(serviceType, implementationType);
        }
        
        return services;
    }
    
    /// <summary>
    /// Registers notification handlers from the specified assemblies
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assemblies">The assemblies to scan for handlers</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddNotificationHandlers(
        this IServiceCollection services, 
        params Assembly[] assemblies)
    {
        // Register notification handlers implementing INotificationHandler<TNotification>
        var notificationHandlerTypes = new List<(Type serviceType, Type implementationType)>();
        
        // Find and register all handler implementations
        foreach (var assembly in assemblies)
        {
            foreach (var implementationType in assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface))
            {
                // Find all implemented interfaces that are INotificationHandler<>
                foreach (var interfaceType in implementationType.GetInterfaces())
                {
                    if (interfaceType.IsGenericType && 
                        interfaceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                    {
                        notificationHandlerTypes.Add((interfaceType, implementationType));
                    }
                }
            }
        }
        
        // Register all discovered handlers
        foreach (var (serviceType, implementationType) in notificationHandlerTypes)
        {
            services.TryAddTransient(serviceType, implementationType);
        }
        
        return services;
    }
    
    /// <summary>
    /// Registers stream request handlers from the specified assemblies
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assemblies">The assemblies to scan for handlers</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddStreamRequestHandlers(
        this IServiceCollection services, 
        params Assembly[] assemblies)
    {
        // Register stream request handlers implementing IStreamRequestHandler<TRequest, TResponse>
        var streamRequestHandlerTypes = new List<(Type serviceType, Type implementationType)>();
        
        // Find and register all handler implementations
        foreach (var assembly in assemblies)
        {
            foreach (var implementationType in assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface))
            {
                // Find all implemented interfaces that are IStreamRequestHandler<,>
                foreach (var interfaceType in implementationType.GetInterfaces())
                {
                    if (interfaceType.IsGenericType && 
                        interfaceType.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>))
                    {
                        streamRequestHandlerTypes.Add((interfaceType, implementationType));
                    }
                }
            }
        }
        
        // Register all discovered handlers
        foreach (var (serviceType, implementationType) in streamRequestHandlerTypes)
        {
            services.TryAddTransient(serviceType, implementationType);
        }
        
        return services;
    }
    
    /// <summary>
    /// Adds a pipeline behavior to the service collection
    /// </summary>
    /// <typeparam name="TPipelineBehavior">The type of the pipeline behavior to register</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddPipelineBehavior<TPipelineBehavior>(
        this IServiceCollection services)
        where TPipelineBehavior : class
    {
        // Find all interfaces this behavior implements
        var behaviorType = typeof(TPipelineBehavior);
        var behaviorInterfaces = behaviorType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));
            
        foreach (var interfaceType in behaviorInterfaces)
        {
            services.AddTransient(interfaceType, behaviorType);
        }
        
        return services;
    }
    
    /// <summary>
    /// Adds the performance logging behavior to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddPerformanceLogging(this IServiceCollection services)
    {
        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceLoggingBehavior<,>));
        return services;
    }
    
    /// <summary>
    /// Adds the validation behavior to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assemblies">The assemblies to scan for validators</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddValidation(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        // Register validator interface type
        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(IValidator<>)))
            .AsImplementedInterfaces()
            .WithTransientLifetime());
            
        // Register validation behavior
        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            
        return services;
    }
    
    /// <summary>
    /// Adds the caching behavior to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        return services;
    }
}

/// <summary>
/// Options for configuring MediatorNet
/// </summary>
public class MediatorNetOptions
{
    private readonly List<Type> _behaviors = new List<Type>();
    
    /// <summary>
    /// Gets the collection of registered behavior types
    /// </summary>
    public IReadOnlyList<Type> Behaviors => _behaviors;
    
    /// <summary>
    /// Adds a pipeline behavior to the mediator pipeline
    /// </summary>
    /// <param name="behaviorType">The type of behavior to add</param>
    /// <returns>The options object for chaining</returns>
    public MediatorNetOptions AddBehavior(Type behaviorType)
    {
        _behaviors.Add(behaviorType);
        return this;
    }
    
    /// <summary>
    /// Adds a pipeline behavior to the mediator pipeline
    /// </summary>
    /// <typeparam name="TBehavior">The type of behavior to add</typeparam>
    /// <returns>The options object for chaining</returns>
    public MediatorNetOptions AddBehavior<TBehavior>() where TBehavior : class
    {
        return AddBehavior(typeof(TBehavior));
    }
}