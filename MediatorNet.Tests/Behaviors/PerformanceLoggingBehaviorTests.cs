namespace MediatorNet.Tests.Behaviors;

using System.Diagnostics;
using System.Threading;
using MediatorNet.Abstractions;
using MediatorNet.Behaviors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// Tests for the PerformanceLoggingBehavior
/// </summary>
public class PerformanceLoggingBehaviorTests
{
    private readonly Mock<ILogger<PerformanceLoggingBehavior<TestRequest, string>>> _loggerMock = new();

    [Fact]
    public async Task HandleAsync_WithFastExecution_ShouldLogInformationLevel()
    {
        // Arrange
        var request = new TestRequest(10);
        var next = new Mock<RequestHandlerDelegate<string>>();
        next
            .Setup(n => n())
            .ReturnsAsync("result");
        
        var behavior = new PerformanceLoggingBehavior<TestRequest, string>(_loggerMock.Object);
        
        // Act
        var result = await behavior.HandleAsync(request, next.Object, CancellationToken.None);
        
        // Assert
        Assert.Equal("result", result);
        
        // Verify that the log method was called with LogLevel.Information
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task HandleAsync_WithSlowExecution_ShouldLogWarningLevel()
    {
        // Arrange
        var request = new TestRequest(1000);
        var next = new Mock<RequestHandlerDelegate<string>>();
        
        // Set up the delegate to delay execution to simulate a slow request (> 500ms)
        next
            .Setup(n => n())
            .Returns(async () => {
                await Task.Delay(600); // Delay longer than the 500ms threshold
                return "result";
            });
        
        var behavior = new PerformanceLoggingBehavior<TestRequest, string>(_loggerMock.Object);
        
        // Act
        var result = await behavior.HandleAsync(request, next.Object, CancellationToken.None);
        
        // Assert
        Assert.Equal("result", result);
        
        // Verify that the log method was called with LogLevel.Warning
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    private class TestRequest : IRequest<string>
    {
        public int Value { get; }
        
        public TestRequest(int value)
        {
            Value = value;
        }
    }
}