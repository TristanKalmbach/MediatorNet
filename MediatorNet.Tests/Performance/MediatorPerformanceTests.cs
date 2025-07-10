namespace MediatorNet.Tests.Performance;

using System.Diagnostics;
using Abstractions;
using Implementation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Performance tests to validate optimization improvements
/// </summary>
public class MediatorPerformanceTests
{
    [Fact]
    public async Task SendAsync_WithHighVolume_ShouldExecuteQuickly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatorNet(typeof(TestPerformanceHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        const int iterations = 10000;
        var requests = Enumerable.Range(0, iterations)
            .Select(i => new TestPerformanceRequest($"Test {i}"))
            .ToArray();

        // Warm up the caches
        await mediator.SendAsync(requests[0]);

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        foreach (var request in requests)
        {
            await mediator.SendAsync(request);
        }
        
        stopwatch.Stop();

        // Assert
        // Should complete 10k requests in under 1 second with optimization
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
            $"Performance test took {stopwatch.ElapsedMilliseconds}ms for {iterations} requests");
        
        // Log performance for analysis
        var requestsPerSecond = iterations / (stopwatch.ElapsedMilliseconds / 1000.0);
        Console.WriteLine($"Performance: {requestsPerSecond:N0} requests/second");
    }

    [Fact]
    public async Task PublishAsync_WithMultipleHandlers_ShouldExecuteQuickly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatorNet(typeof(TestPerformanceNotificationHandler1).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        const int iterations = 5000;
        var notifications = Enumerable.Range(0, iterations)
            .Select(i => new TestPerformanceNotification($"Test {i}"))
            .ToArray();

        // Warm up
        await mediator.PublishAsync(notifications[0]);

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        foreach (var notification in notifications)
        {
            await mediator.PublishAsync(notification);
        }
        
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
            $"Notification performance test took {stopwatch.ElapsedMilliseconds}ms for {iterations} notifications");
            
        var notificationsPerSecond = iterations / (stopwatch.ElapsedMilliseconds / 1000.0);
        Console.WriteLine($"Notification Performance: {notificationsPerSecond:N0} notifications/second");
    }

    public record TestPerformanceRequest(string Data) : IRequest<string>;

    public class TestPerformanceHandler : IRequestHandler<TestPerformanceRequest, string>
    {
        public ValueTask<string> HandleAsync(TestPerformanceRequest request, CancellationToken cancellationToken = default)
        {
            return new ValueTask<string>($"Handled: {request.Data}");
        }
    }

    public record TestPerformanceNotification(string Message) : INotification;

    public class TestPerformanceNotificationHandler1 : INotificationHandler<TestPerformanceNotification>
    {
        public ValueTask HandleAsync(TestPerformanceNotification notification, CancellationToken cancellationToken = default)
        {
            // Simulate some work
            return ValueTask.CompletedTask;
        }
    }

    public class TestPerformanceNotificationHandler2 : INotificationHandler<TestPerformanceNotification>
    {
        public ValueTask HandleAsync(TestPerformanceNotification notification, CancellationToken cancellationToken = default)
        {
            // Simulate some work
            return ValueTask.CompletedTask;
        }
    }
}