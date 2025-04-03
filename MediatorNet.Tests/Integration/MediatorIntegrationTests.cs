namespace MediatorNet.Tests.Integration;

using System.Runtime.CompilerServices;
using System.Threading;
using Abstractions;
using MediatorNet.Behaviors;
using FluentValidation;
using Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Integration tests that demonstrate MediatorNet functionality in realistic scenarios
/// </summary>
public class MediatorIntegrationTests
{
    // [Fact]
    // public async Task CompleteWorkflow_WithValidationAndLogging_ShouldExecuteSuccessfully()
    // {
    //     // Arrange
    //     var services = new ServiceCollection();
    //     
    //     // Configure logging
    //     services.AddLogging(builder => builder.AddDebug());
    //     
    //     // Add our repository first with test data
    //     var repository = new InMemoryProductRepository();
    //     services.AddSingleton<IProductRepository>(repository);
    //     
    //     // Register the notification tracker 
    //     var tracker = new NotificationTracker();
    //     services.AddSingleton(tracker);
    //     
    //     // Register validator
    //     services.AddSingleton<IValidator<CreateProductCommand>, CreateProductCommandValidator>();
    //     
    //     // Add MediatorNet with the validation behavior specifically registered
    //     services.AddMediatorNet(options =>
    //     {
    //         // Explicitly include validation behavior
    //         options.AddBehavior(typeof(ValidationBehavior<,>));
    //     });
    //     
    //     // Explicitly register handlers for clarity
    //     services.AddTransient<IRequestHandler<CreateProductCommand>, CreateProductCommandHandler>();
    //     services.AddTransient<IRequestHandler<GetProductByNameQuery, Product>, GetProductByNameQueryHandler>();
    //     services.AddTransient<INotificationHandler<ProductCreatedNotification>>(
    //         _ => new EmailNotificationHandler(tracker));
    //     services.AddTransient<INotificationHandler<ProductCreatedNotification>>(
    //         _ => new LogNotificationHandler(tracker));
    //     
    //     var serviceProvider = services.BuildServiceProvider();
    //     var mediator = serviceProvider.GetRequiredService<IMediator>();
    //     
    //     // Act - Create a product
    //     var createCommand = new CreateProductCommand("Test Product", 19.99m);
    //     await mediator.SendAsync(createCommand);
    //     
    //     // Query for the product we just created
    //     var getProductQuery = new GetProductByNameQuery("Test Product");
    //     var product = await mediator.SendAsync(getProductQuery);
    //     
    //     // Create an invalid product (should throw validation exception)
    //     var invalidCommand = new CreateProductCommand("", -5.0m);
    //     
    //     // Assert
    //     Assert.NotNull(product);
    //     Assert.Equal("Test Product", product.Name);
    //     Assert.Equal(19.99m, product.Price);
    //     
    //     // Test that validation works
    //     var validationException = await Assert.ThrowsAsync<ValidationException>(() => 
    //         mediator.SendAsync(invalidCommand));
    //         
    //     // Check the validation error details
    //     Assert.Contains(validationException.Errors, e => e.PropertyName == "Name");
    //     Assert.Contains(validationException.Errors, e => e.PropertyName == "Price");
    // }
    
    [Fact]
    public async Task StreamingWorkflow_ShouldStreamResultsIncrementally()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Create repository with test data
        var repository = new InMemoryProductRepository();
        await repository.CreateProductAsync(new Product { Id = 1, Name = "Product 1", Price = 10.0m });
        await repository.CreateProductAsync(new Product { Id = 2, Name = "Product 2", Price = 20.0m });
        await repository.CreateProductAsync(new Product { Id = 3, Name = "Product 3", Price = 30.0m });
        
        // Add repository to services 
        services.AddSingleton<IProductRepository>(repository);
        
        // Add MediatorNet
        services.AddMediatorNet();
        
        // Explicitly register the stream handler
        services.AddTransient<IStreamRequestHandler<GetAllProductsStreamQuery, Product>, GetAllProductsStreamQueryHandler>();
        
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        
        // Act - Get stream of products
        var streamRequest = new GetAllProductsStreamQuery();
        var productStream = mediator.CreateStream(streamRequest);
        
        // Assert
        int count = 0;
        decimal totalPrice = 0;
        
        await foreach (var product in productStream)
        {
            count++;
            totalPrice += product.Price;
        }
        
        Assert.Equal(3, count);
        Assert.Equal(60.0m, totalPrice);
    }
    
    [Fact]
    public async Task NotificationWorkflow_ShouldInvokeAllHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Configure logging
        services.AddLogging(builder => builder.AddDebug());
        
        // Add notification tracker singleton
        var tracker = new NotificationTracker();
        services.AddSingleton(tracker); // Use the instance directly
        
        // Add MediatorNet
        services.AddMediatorNet();
        
        // Explicitly register handlers with our tracker instance
        services.AddTransient<INotificationHandler<ProductCreatedNotification>>(
            _ => new EmailNotificationHandler(tracker));
        services.AddTransient<INotificationHandler<ProductCreatedNotification>>(
            _ => new LogNotificationHandler(tracker));
        
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        
        // Act - Publish a notification
        var notification = new ProductCreatedNotification("Test Product");
        await mediator.PublishAsync(notification);
        
        // Assert - Both handlers should have been called
        Assert.Equal(2, tracker.HandlerCallCount);
        Assert.Contains("Email", tracker.HandlersCalled);
        Assert.Contains("Log", tracker.HandlersCalled);
    }

    #region Test Classes

    // Domain Model
    public record Product
    {
        public int Id { get; init; }
        public required string Name { get; init; }
        public decimal Price { get; init; }
    }

    // Repository Interface
    public interface IProductRepository
    {
        ValueTask<Product?> GetProductByNameAsync(string name, CancellationToken cancellationToken = default);
        ValueTask CreateProductAsync(Product product, CancellationToken cancellationToken = default);
        IAsyncEnumerable<Product> GetAllProductsAsync(CancellationToken cancellationToken = default);
    }

    // Repository Implementation
    public class InMemoryProductRepository : IProductRepository
    {
        private readonly List<Product> _products = [];
        private int _nextId = 1;

        public async ValueTask<Product?> GetProductByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async
            return _products.FirstOrDefault(p => p.Name == name);
        }

        public ValueTask CreateProductAsync(Product product, CancellationToken cancellationToken = default)
        {
            // If Id is 0, assign a new Id
            if (product.Id == 0)
            {
                var newProduct = product with { Id = _nextId++ };
                _products.Add(newProduct);
            }
            else
            {
                _products.Add(product);
            }
            
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Product> GetAllProductsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var product in _products)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(1, cancellationToken); // Simulate async work
                yield return product;
            }
        }
    }

    // Command
    public record CreateProductCommand(string Name, decimal Price) : IRequest;

    // Command Validator
    public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Product name cannot be empty");
            RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than zero");
        }
    }

    // Command Handler
    public class CreateProductCommandHandler(IProductRepository repository, IMediator mediator) 
        : IRequestHandler<CreateProductCommand>
    {
        private readonly IProductRepository _repository = repository;
        private readonly IMediator _mediator = mediator;

        public async ValueTask HandleAsync(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var product = new Product
            {
                Name = request.Name,
                Price = request.Price
            };

            await _repository.CreateProductAsync(product, cancellationToken);
            
            // Publish notification that product was created
            await _mediator.PublishAsync(new ProductCreatedNotification(request.Name), cancellationToken);
        }
    }

    // Query
    public record GetProductByNameQuery(string Name) : IRequest<Product>;

    // Query Handler
    public class GetProductByNameQueryHandler(IProductRepository repository) 
        : IRequestHandler<GetProductByNameQuery, Product>
    {
        private readonly IProductRepository _repository = repository;

        public async ValueTask<Product> HandleAsync(GetProductByNameQuery request, CancellationToken cancellationToken)
        {
            var product = await _repository.GetProductByNameAsync(request.Name, cancellationToken);
            
            if (product == null)
            {
                throw new InvalidOperationException($"Product with name '{request.Name}' not found");
            }
            
            return product;
        }
    }

    // Stream Query
    public record GetAllProductsStreamQuery : IStreamRequest<Product>;

    // Stream Query Handler
    public class GetAllProductsStreamQueryHandler(IProductRepository repository) 
        : IStreamRequestHandler<GetAllProductsStreamQuery, Product>
    {
        private readonly IProductRepository _repository = repository;

        public IAsyncEnumerable<Product> HandleAsync(
            GetAllProductsStreamQuery request, 
            CancellationToken cancellationToken)
        {
            return _repository.GetAllProductsAsync(cancellationToken);
        }
    }

    // Notification
    public record ProductCreatedNotification(string ProductName) : INotification;

    // Notification Tracker (for testing)
    public class NotificationTracker
    {
        public int HandlerCallCount { get; private set; }
        public List<string> HandlersCalled { get; } = [];

        public void TrackHandlerCall(string handlerName)
        {
            HandlerCallCount++;
            HandlersCalled.Add(handlerName);
        }
    }

    // Notification Handlers
    public class EmailNotificationHandler(NotificationTracker tracker) 
        : INotificationHandler<ProductCreatedNotification>
    {
        private readonly NotificationTracker _tracker = tracker;

        public ValueTask HandleAsync(ProductCreatedNotification notification, CancellationToken cancellationToken)
        {
            // In a real application, this would send an email
            _tracker.TrackHandlerCall("Email");
            return ValueTask.CompletedTask;
        }
    }

    public class LogNotificationHandler(NotificationTracker tracker) 
        : INotificationHandler<ProductCreatedNotification>
    {
        private readonly NotificationTracker _tracker = tracker;

        public ValueTask HandleAsync(ProductCreatedNotification notification, CancellationToken cancellationToken)
        {
            // In a real application, this would log the event
            _tracker.TrackHandlerCall("Log");
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}