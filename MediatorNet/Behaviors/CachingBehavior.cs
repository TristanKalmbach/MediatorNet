namespace MediatorNet.Behaviors;

using System.Collections.Concurrent;
using Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pipeline behavior that caches responses for requests
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public sealed class CachingBehavior<TRequest, TResponse>(
    IMemoryCache cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> 
    where TRequest : ICacheableRequest<TResponse>
    where TResponse : notnull
{
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger = logger;
    
    // Track types that have been logged as not cacheable to avoid excessive logging
    private static readonly ConcurrentDictionary<Type, bool> LoggedUncacheableTypes = new();

    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        
        // Generate cache key
        var cacheKey = $"MediatorNet:Cache:{requestType.Name}:{request.CacheKey}";
        
        // Check if the response is in the cache
        if (_cache.TryGetValue(cacheKey, out TResponse? cachedResponse))
        {
            _logger.LogDebug("Cache hit for {RequestType} with key {CacheKey}", requestType.Name, request.CacheKey);
            return cachedResponse!;
        }
        
        // Execute the request if not cached
        _logger.LogDebug("Cache miss for {RequestType} with key {CacheKey}", requestType.Name, request.CacheKey);
        var response = await next();
        
        // Cache the response with the specified cache options
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = request.CacheExpirationDuration,
            Priority = request.CachePriority
        };
        
        _cache.Set(cacheKey, response, cacheOptions);
        _logger.LogDebug(
            "Cached response for {RequestType} with key {CacheKey} for {Duration}", 
            requestType.Name, 
            request.CacheKey, 
            request.CacheExpirationDuration);
            
        return response;
    }
}