namespace MediatorNet.Behaviors;

using System.Collections.Concurrent;
using Abstractions;
using FluentValidation;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pipeline behavior that automatically validates requests using FluentValidation
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators = validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger = logger;
    
    // Cache of request types that have no validators to avoid repeated empty enumerations
    private static readonly ConcurrentDictionary<Type, bool> RequestsWithNoValidators = new();

    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        
        // Skip validation if this request type has no validators (using cache to avoid repeated checks)
        if (RequestsWithNoValidators.ContainsKey(requestType))
        {
            return await next();
        }
        
        var validators = _validators.ToArray();
        
        // If no validators exist for this request, cache this information and skip validation
        if (validators.Length == 0)
        {
            RequestsWithNoValidators.TryAdd(requestType, true);
            return await next();
        }

        // Execute all validators
        var validationContext = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(validationContext, cancellationToken)));
        
        // Combine all validation failures
        var validationFailures = validationResults
            .SelectMany(result => result.Errors)
            .Where(error => error is not null)
            .ToArray();
        
        // If validation passes, continue the pipeline
        if (validationFailures.Length == 0)
        {
            return await next();
        }

        // Log validation failures and throw exception
        _logger.LogWarning(
            "Validation failed for {RequestType} with {ErrorCount} errors",
            requestType.Name, 
            validationFailures.Length);
        
        foreach (var error in validationFailures)
        {
            _logger.LogDebug(
                "Validation error: Property: {PropertyName}, Error: {ErrorMessage}",
                error.PropertyName,
                error.ErrorMessage);
        }
        
        throw new ValidationException(validationFailures);
    }
}