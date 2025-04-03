using MediatorNet.Behaviors;

namespace MediatorNet.Tests.DependencyInjection;

using Abstractions;
using Behaviors;
using FluentValidation;
using Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Tests for the ServiceCollectionExtensions
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMediatorNet_ShouldRegisterMediatorService()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddMediatorNet();
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetService<IMediator>();
        
        Assert.NotNull(mediator);
        Assert.IsType<Mediator>(mediator);
    }
    
    [Fact]
    public void AddMediatorNet_WithAssembly_ShouldRegisterHandlersFromAssembly()
    {
        // Arrange
        var services = new ServiceCollection();
        var assembly = typeof(TestRequestHandler).Assembly;
        
        // Act
        services.AddMediatorNet(assembly);
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetService<IMediator>();
        var handler = serviceProvider.GetService<IRequestHandler<TestRequest, TestResponse>>();
        
        Assert.NotNull(mediator);
        Assert.NotNull(handler);
        Assert.IsType<TestRequestHandler>(handler);
    }
    
    [Fact]
    public void AddPerformanceLogging_ShouldRegisterPerformanceLoggingBehavior()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Register required logging services
        services.AddLogging(builder => builder.AddDebug());
        
        // Act
        services.AddMediatorNet()
                .AddPerformanceLogging();
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<IRequest<object>, object>>();
        
        Assert.Contains(behaviors, b => b.GetType() == typeof(PerformanceLoggingBehavior<IRequest<object>, object>));
    }
    
    [Fact]
    public void AddValidation_ShouldRegisterValidationBehavior()
    {
        // Arrange
        var services = new ServiceCollection();
        var assembly = typeof(TestValidator).Assembly;
        
        // Register required logging services
        services.AddLogging(builder => builder.AddDebug());
        
        // Act
        services.AddMediatorNet()
                .AddValidation(assembly);
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<IRequest<object>, object>>();
        var validator = serviceProvider.GetService<IValidator<TestRequest>>();
        
        Assert.Contains(behaviors, b => b is ValidationBehavior<IRequest<object>, object>);
        Assert.NotNull(validator);
        Assert.IsType<TestValidator>(validator);
    }
    
    [Fact]
    public void AddCaching_ShouldRegisterCachingBehavior()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Register required services
        services.AddMemoryCache();
        services.AddLogging(builder => builder.AddDebug());
        
        // Act
        services.AddMediatorNet()
                .AddCaching();
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // First check if the general behavior was registered
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<IRequest<object>, object>>();
        
        // Then specifically check for a correct cacheable request
        var cacheableBehavior = serviceProvider.GetService<IPipelineBehavior<TestCacheableRequest, string>>();
        
        Assert.NotNull(cacheableBehavior);
        Assert.IsType<CachingBehavior<TestCacheableRequest, string>>(cacheableBehavior);
    }
    
    [Fact]
    public void AddPipelineBehavior_WithCustomBehavior_ShouldRegisterCustomBehavior()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddMediatorNet()
                .AddPipelineBehavior<CustomPipelineBehavior>();
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<IRequest<object>, object>>();
        
        Assert.Contains(behaviors, b => b.GetType() == typeof(CustomPipelineBehavior));
    }
    
    #region Test Types
    
    public record TestRequest(string Name) : IRequest<TestResponse>;
    
    public record TestResponse(string Result);
    
    public class TestRequestHandler : IRequestHandler<TestRequest, TestResponse>
    {
        public ValueTask<TestResponse> HandleAsync(TestRequest request, CancellationToken cancellationToken = default)
        {
            return new ValueTask<TestResponse>(new TestResponse($"{request.Name} Handled"));
        }
    }
    
    public class TestValidator : AbstractValidator<TestRequest>
    {
        public TestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
    
    public class CustomPipelineBehavior : IPipelineBehavior<IRequest<object>, object>
    {
        public ValueTask<object> HandleAsync(IRequest<object> request, RequestHandlerDelegate<object> next, CancellationToken cancellationToken = default)
        {
            return next();
        }
    }
    
    public record TestCacheableRequest(string CacheKey) : IRequest<string>, ICacheableRequest<string>
    {
        public TimeSpan CacheExpirationDuration => TimeSpan.FromMinutes(5);
        
        public CacheItemPriority CachePriority => CacheItemPriority.Normal;
    }
    
    #endregion
}