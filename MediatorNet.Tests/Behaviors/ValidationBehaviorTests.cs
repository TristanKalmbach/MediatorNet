namespace MediatorNet.Tests.Behaviors;

using System.Threading;
using Abstractions;
using MediatorNet.Behaviors;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tests for the ValidationBehavior
/// </summary>
public class ValidationBehaviorTests
{
    private readonly Mock<ILogger<ValidationBehavior<TestRequest, TestResponse>>> _loggerMock;
    
    public ValidationBehaviorTests()
    {
        _loggerMock = new Mock<ILogger<ValidationBehavior<TestRequest, TestResponse>>>();
    }
    
    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldCallNextDelegate()
    {
        // Arrange
        var request = new TestRequest("ValidName");
        var expectedResponse = new TestResponse("Success");
        
        var validator = new Mock<IValidator<TestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
            
        var nextMock = new Mock<RequestHandlerDelegate<TestResponse>>();
        nextMock
            .Setup(n => n())
            .ReturnsAsync(expectedResponse);
            
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(
            new[] { validator.Object }, 
            _loggerMock.Object);
            
        // Act
        var result = await behavior.HandleAsync(request, nextMock.Object);
        
        // Assert
        Assert.Same(expectedResponse, result);
        nextMock.Verify(n => n(), Times.Once);
    }
    
    [Fact]
    public async Task HandleAsync_WithInvalidRequest_ShouldThrowValidationException()
    {
        // Arrange
        var request = new TestRequest(string.Empty); // Invalid - empty name
        
        var validationFailures = new[]
        {
            new ValidationFailure("Name", "Name cannot be empty")
        };
        
        var validator = new Mock<IValidator<TestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));
            
        var nextMock = new Mock<RequestHandlerDelegate<TestResponse>>();
        nextMock
            .Setup(n => n())
            .ReturnsAsync(new TestResponse("This should not be called"));
            
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(
            new[] { validator.Object }, 
            _loggerMock.Object);
            
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => 
            behavior.HandleAsync(request, nextMock.Object).AsTask());
            
        Assert.Single(exception.Errors);
        Assert.Equal("Name", exception.Errors.First().PropertyName);
        nextMock.Verify(n => n(), Times.Never);
    }
    
    [Fact]
    public async Task HandleAsync_WithNoValidators_ShouldCallNextDelegate()
    {
        // Arrange
        var request = new TestRequest("Name");
        var expectedResponse = new TestResponse("Success");
            
        var nextMock = new Mock<RequestHandlerDelegate<TestResponse>>();
        nextMock
            .Setup(n => n())
            .ReturnsAsync(expectedResponse);
            
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(
            Array.Empty<IValidator<TestRequest>>(), 
            _loggerMock.Object);
            
        // Act
        var result = await behavior.HandleAsync(request, nextMock.Object);
        
        // Assert
        Assert.Same(expectedResponse, result);
        nextMock.Verify(n => n(), Times.Once);
    }
    
    [Fact]
    public async Task HandleAsync_WithMultipleValidatorsAndAllValid_ShouldCallNextDelegate()
    {
        // Arrange
        var request = new TestRequest("ValidName");
        var expectedResponse = new TestResponse("Success");
        
        var validator1 = new Mock<IValidator<TestRequest>>();
        validator1
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
            
        var validator2 = new Mock<IValidator<TestRequest>>();
        validator2
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
            
        var nextMock = new Mock<RequestHandlerDelegate<TestResponse>>();
        nextMock
            .Setup(n => n())
            .ReturnsAsync(expectedResponse);
            
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(
            new[] { validator1.Object, validator2.Object }, 
            _loggerMock.Object);
            
        // Act
        var result = await behavior.HandleAsync(request, nextMock.Object);
        
        // Assert
        Assert.Same(expectedResponse, result);
        validator1.Verify(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()), Times.Once);
        validator2.Verify(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()), Times.Once);
        nextMock.Verify(n => n(), Times.Once);
    }
    
    #region Test Types
    
    public record TestRequest(string Name) : IRequest<TestResponse>;
    
    public record TestResponse(string Result);
    
    #endregion
}