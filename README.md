# MediatorNet

MediatorNet is a high-performance, feature-rich mediator pattern implementation for .NET applications. It provides a clean way to decouple components in your application by enabling commands, queries, and events to be processed through a central mediator.

## Features

- **High Performance**: Uses `ValueTask` for optimal async/await performance
- **Command and Query Processing**: Clear separation of commands (operations that change state) and queries (operations that return data)
- **Notification Publishing**: Publish notifications/events to multiple handlers
- **Pipeline Behaviors**: Extensible pipeline for cross-cutting concerns
  - **Performance Logging**: Automatically log request performance metrics
  - **Validation**: Automatic request validation using FluentValidation
  - **Caching**: Transparent response caching for queries
- **Streaming Support**: Process large result sets as streams with `IAsyncEnumerable<T>`
- **Dependency Injection Integration**: Seamless integration with Microsoft.Extensions.DependencyInjection

## Installation

```bash
dotnet add package MediatorNet
```

## Basic Usage

### 1. Define Commands and Queries

Commands (requests with no return value):

```csharp
public record CreateUserCommand(string Username, string Email) : IRequest;
```

Queries (requests that return data):

```csharp
public record GetUserByIdQuery(int UserId) : IRequest<UserDto>;
```

### 2. Implement Handlers

Command handler:

```csharp
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand>
{
    private readonly IUserRepository _repository;

    public CreateUserCommandHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask HandleAsync(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Username = request.Username,
            Email = request.Email
        };

        await _repository.CreateUserAsync(user, cancellationToken);
    }
}
```

Query handler:

```csharp
public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly IUserRepository _repository;

    public GetUserByIdQueryHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<UserDto> HandleAsync(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetUserByIdAsync(request.UserId, cancellationToken);
        
        return user is null 
            ? throw new NotFoundException($"User with ID {request.UserId} not found") 
            : new UserDto(user.Id, user.Username, user.Email);
    }
}
```

### 3. Configure Services

```csharp
// Program.cs or Startup.cs
services.AddMediatorNet(typeof(Program).Assembly)
    .AddPerformanceLogging()
    .AddValidation(typeof(Program).Assembly)
    .AddCaching();
```

### 4. Use the Mediator

```csharp
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserDto dto, CancellationToken cancellationToken)
    {
        await _mediator.SendAsync(new CreateUserCommand(dto.Username, dto.Email), cancellationToken);
        return Ok();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _mediator.SendAsync(new GetUserByIdQuery(id), cancellationToken);
            return Ok(user);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
```

## Advanced Features

### Notifications (Events)

Define a notification:

```csharp
public record UserCreatedNotification(int UserId, string Username) : INotification;
```

Implement notification handlers (can have multiple):

```csharp
public class EmailNotificationHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly IEmailService _emailService;

    public EmailNotificationHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async ValueTask HandleAsync(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        await _emailService.SendWelcomeEmailAsync(notification.UserId, notification.Username, cancellationToken);
    }
}

public class AuditLogHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly IAuditLogger _auditLogger;

    public AuditLogHandler(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public async ValueTask HandleAsync(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        await _auditLogger.LogAsync($"User created: {notification.UserId} - {notification.Username}", cancellationToken);
    }
}
```

Publish notifications:

```csharp
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand>
{
    private readonly IUserRepository _repository;
    private readonly IMediator _mediator;

    public CreateUserCommandHandler(IUserRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async ValueTask HandleAsync(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Username = request.Username,
            Email = request.Email
        };

        var userId = await _repository.CreateUserAsync(user, cancellationToken);
        
        // Publish notification after user creation
        await _mediator.PublishAsync(new UserCreatedNotification(userId, request.Username), cancellationToken);
    }
}
```

### Streaming Support

For large result sets, use streaming requests to process items as they arrive:

```csharp
// Define a streaming request
public record GetAllUsersStreamRequest : IStreamRequest<UserDto>;

// Implement a streaming handler
public class GetAllUsersStreamRequestHandler : IStreamRequestHandler<GetAllUsersStreamRequest, UserDto>
{
    private readonly IUserRepository _repository;

    public GetAllUsersStreamRequestHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async IAsyncEnumerable<UserDto> HandleAsync(
        GetAllUsersStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var user in _repository.GetAllUsersStreamAsync(cancellationToken))
        {
            yield return new UserDto(user.Id, user.Username, user.Email);
        }
    }
}

// Use the streaming API in your controller
[HttpGet]
public IActionResult GetAllUsers(CancellationToken cancellationToken)
{
    var usersStream = _mediator.CreateStream(new GetAllUsersStreamRequest(), cancellationToken);
    
    // Return as a stream response in ASP.NET Core
    return new StreamResult<UserDto>(usersStream);
}
```

### Pipeline Behaviors

#### Performance Logging

Automatically added with `.AddPerformanceLogging()`. Logs request execution time and warns on slow requests.

#### Validation

Add FluentValidation validators:

```csharp
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
```

#### Request Caching

Implement cacheable requests:

```csharp
public record GetUserByIdQuery(int UserId) : IRequest<UserDto>, ICacheableRequest<UserDto>
{
    public string CacheKey => $"User-{UserId}";
    public TimeSpan CacheExpirationDuration => TimeSpan.FromMinutes(10);
}
```

## Comparison with MediatR

| Feature | MediatorNet | MediatR |
|---------|------------|---------|
| Performance | Uses `ValueTask` for improved performance | Uses `Task` |
| Pipeline | Supports behaviors with same functionality | Supports behaviors |
| Validators | Integrated with FluentValidation | Requires separate implementation |
| Caching | Built-in support for caching responses | Requires custom implementation |
| Streaming | Native streaming support with `IAsyncEnumerable` | Limited streaming support |
| DI Integration | Streamlined registration with explicit methods | Basic registration |
| Async | First-class async support | Async support |
| Return Types | Explicit handling of void returns with Unit type | Uses Unit type |

## License

This project is licensed under the MIT License - see the LICENSE file for details.