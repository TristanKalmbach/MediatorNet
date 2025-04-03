namespace MediatorNet.Behaviors;

using System.Diagnostics;
using Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pipeline behavior that logs performance metrics for requests
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public sealed class PerformanceLoggingBehavior<TRequest, TResponse>(ILogger<PerformanceLoggingBehavior<TRequest, TResponse>> logger) 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceLoggingBehavior<TRequest, TResponse>> _logger = logger;
    
    // Threshold in milliseconds above which we consider a request slow
    private const int SlowRequestThresholdMs = 500;

    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var requestType = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Call the next handler in the pipeline
            var response = await next();
            
            stopwatch.Stop();
            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            
            // Log slow requests as warnings
            if (elapsedMilliseconds > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "Slow request detected: {RequestType} ({ElapsedMilliseconds} ms)",
                    requestType,
                    elapsedMilliseconds);
            }
            else
            {
                _logger.LogInformation(
                    "Request: {RequestType} ({ElapsedMilliseconds} ms)",
                    requestType,
                    elapsedMilliseconds);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Error handling {RequestType} after {ElapsedMilliseconds} ms",
                requestType,
                stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
}