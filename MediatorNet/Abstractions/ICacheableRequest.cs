namespace MediatorNet.Abstractions;

using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Interface for requests that can be cached
/// </summary>
/// <typeparam name="TResponse">Response type</typeparam>
public interface ICacheableRequest<out TResponse> : IRequest<TResponse>
{
    /// <summary>
    /// Gets the cache key for this request
    /// </summary>
    string CacheKey { get; }
    
    /// <summary>
    /// Gets the expiration duration for the cached response
    /// </summary>
    TimeSpan CacheExpirationDuration { get; }
    
    /// <summary>
    /// Gets the cache priority for this request
    /// </summary>
    CacheItemPriority CachePriority => CacheItemPriority.Normal;
}