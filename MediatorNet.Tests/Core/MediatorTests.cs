namespace MediatorNet.Tests.Core;

using System.Runtime.CompilerServices;
using System.Threading;
using Abstractions;
using Implementation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tests the core functionality of the Mediator implementation
/// </summary>
public class MediatorTests
{
    [Fact]
    public async Task SendAsync_WithResponse_ShouldInvokeRequestHandler()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<IRequestHandler<TestRequest, TestResponse>, TestRequestHandler>()
            .BuildServiceProvider();

        var mediator = new Mediator(serviceProvider);
        var request = new TestRequest("Test");

        // Act
        var response = await mediator.SendAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Test Handled", response.Result);
    }

    [Fact]
    public async Task SendAsync_WithoutResponse_ShouldInvokeRequestHandler()
    {
        // Arrange
        var handlerMock = new Mock<IRequestHandler<TestCommand>>();
        handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var serviceProvider = new ServiceCollection()
            .AddTransient(_ => handlerMock.Object)
            .BuildServiceProvider();

        var mediator = new Mediator(serviceProvider);
        var command = new TestCommand("Test");

        // Act
        await mediator.SendAsync(command);

        // Assert
        handlerMock.Verify(h => h.HandleAsync(It.Is<TestCommand>(c => c.Name == "Test"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WithNotification_ShouldInvokeAllNotificationHandlers()
    {
        // Arrange
        var handlerMock1 = new Mock<INotificationHandler<TestNotification>>();
        handlerMock1
            .Setup(h => h.HandleAsync(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var handlerMock2 = new Mock<INotificationHandler<TestNotification>>();
        handlerMock2
            .Setup(h => h.HandleAsync(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var serviceProvider = new ServiceCollection()
            .AddTransient(_ => handlerMock1.Object)
            .AddTransient(_ => handlerMock2.Object)
            .BuildServiceProvider();

        var mediator = new Mediator(serviceProvider);
        var notification = new TestNotification("Test");

        // Act
        await mediator.PublishAsync(notification);

        // Assert
        handlerMock1.Verify(h => h.HandleAsync(It.Is<TestNotification>(n => n.Message == "Test"), It.IsAny<CancellationToken>()), Times.Once);
        handlerMock2.Verify(h => h.HandleAsync(It.Is<TestNotification>(n => n.Message == "Test"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateStream_WithStreamRequest_ShouldReturnStreamFromHandler()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<IStreamRequestHandler<TestStreamRequest, string>, TestStreamRequestHandler>()
            .BuildServiceProvider();

        var mediator = new Mediator(serviceProvider);
        var request = new TestStreamRequest(5);

        // Act
        var stream = mediator.CreateStream(request);

        // Assert
        var results = new List<string>();
        await foreach (var item in stream)
        {
            results.Add(item);
        }
        Assert.Equal(5, results.Count);
        Assert.Equal("Item 0", results[0]);
        Assert.Equal("Item 4", results[4]);
    }

    [Fact]
    public async Task SendAsync_WithMissingHandler_ShouldThrowException()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var mediator = new Mediator(serviceProvider);
        var request = new TestRequest("Test");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            mediator.SendAsync(request).AsTask());
    }

    #region Test Types

    public record TestRequest(string Query) : IRequest<TestResponse>;

    public record TestResponse(string Result);

    public class TestRequestHandler : IRequestHandler<TestRequest, TestResponse>
    {
        public ValueTask<TestResponse> HandleAsync(TestRequest request, CancellationToken cancellationToken = default)
        {
            return new ValueTask<TestResponse>(new TestResponse($"{request.Query} Handled"));
        }
    }

    public record TestCommand(string Name) : IRequest;

    public record TestNotification(string Message) : INotification;

    public record TestStreamRequest(int Count) : IStreamRequest<string>;

    public class TestStreamRequestHandler : IStreamRequestHandler<TestStreamRequest, string>
    {
        public async IAsyncEnumerable<string> HandleAsync(
            TestStreamRequest request, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < request.Count; i++)
            {
                yield return $"Item {i}";
                await Task.Delay(1, cancellationToken);
            }
        }
    }

    #endregion
}