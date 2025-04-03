namespace MediatorNet.Tests.Behaviors;

using System.Threading;
using Abstractions;
using MediatorNet.Behaviors;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tests for the CachingBehavior
/// </summary>
public class CachingBehaviorTests
{
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<ILogger<CachingBehavior<TestCacheableRequest, string>>> _loggerMock;
    private readonly Mock<ICacheEntry> _cacheEntryMock;
    
    public CachingBehaviorTests()
    {
        _cacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<CachingBehavior<TestCacheableRequest, string>>>();
        _cacheEntryMock = new Mock<ICacheEntry>();
        
        // Setup the cache entry mock to be returned by CreateEntry
        _cacheMock
            .Setup(m => m.CreateEntry(It.IsAny<object>()))
            .Returns(_cacheEntryMock.Object);
            
        // Set up properties that we want to track
        _cacheEntryMock.SetupProperty(e => e.Value);
        
        // We can't mock extension methods directly, so we'll just verify the cache entry was created
    }
    
    [Fact]
    public async Task HandleAsync_WhenResultNotCached_ShouldCallNextDelegateAndCacheResult()
    {
        // Arrange
        var request = new TestCacheableRequest("key1");
        object? cacheEntry = null;
        
        // Setup cache miss
        _cacheMock
            .Setup(m => m.TryGetValue(It.IsAny<string>(), out cacheEntry))
            .Returns(false);
        
        var nextMock = new Mock<RequestHandlerDelegate<string>>();
        nextMock
            .Setup(n => n())
            .ReturnsAsync("Cached Result");
        
        var behavior = new CachingBehavior<TestCacheableRequest, string>(
            _cacheMock.Object,
            _loggerMock.Object);
        
        // Act
        var result = await behavior.HandleAsync(request, nextMock.Object);
        
        // Assert
        Assert.Equal("Cached Result", result);
        nextMock.Verify(n => n(), Times.Once);
        _cacheMock.Verify(m => m.CreateEntry(It.Is<string>(s => s == "MediatorNet:Cache:TestCacheableRequest:key1")), Times.Once);
        
        // Verify the value was set on the cache entry
        Assert.Equal("Cached Result", _cacheEntryMock.Object.Value);
    }
    
    [Fact]
    public async Task HandleAsync_WhenResultCached_ShouldReturnCachedResultWithoutCallingNextDelegate()
    {
        // Arrange
        var request = new TestCacheableRequest("key1");
        object cachedValue = "Cached Result";
        
        // Setup cache hit
        _cacheMock
            .Setup(m => m.TryGetValue(It.Is<string>(s => s == "MediatorNet:Cache:TestCacheableRequest:key1"), out cachedValue))
            .Returns(true);
        
        var nextMock = new Mock<RequestHandlerDelegate<string>>();
        nextMock
            .Setup(n => n())
            .ReturnsAsync("New Result")
            .Verifiable();
        
        var behavior = new CachingBehavior<TestCacheableRequest, string>(
            _cacheMock.Object,
            _loggerMock.Object);
        
        // Act
        var result = await behavior.HandleAsync(request, nextMock.Object);
        
        // Assert
        Assert.Equal("Cached Result", result);
        nextMock.Verify(n => n(), Times.Never);
    }
    
    [Fact]
    public async Task HandleAsync_WithDifferentCacheKeys_ShouldStoreResultsInDifferentCacheEntries()
    {
        // Arrange
        var request1 = new TestCacheableRequest("key1");
        var request2 = new TestCacheableRequest("key2");
        object? cacheEntry = null;
        var cacheKeys = new List<string>();
        
        // Setup cache miss for all keys
        _cacheMock
            .Setup(m => m.TryGetValue(It.IsAny<string>(), out cacheEntry))
            .Returns(false);
        
        // Track cache keys in CreateEntry calls
        _cacheMock
            .Setup(m => m.CreateEntry(It.IsAny<object>()))
            .Callback<object>(key => cacheKeys.Add((string)key))
            .Returns(_cacheEntryMock.Object);
        
        var nextMock = new Mock<RequestHandlerDelegate<string>>();
        nextMock
            .Setup(n => n())
            .ReturnsAsync("Cached Result");
        
        var behavior = new CachingBehavior<TestCacheableRequest, string>(
            _cacheMock.Object,
            _loggerMock.Object);
        
        // Act
        await behavior.HandleAsync(request1, nextMock.Object);
        await behavior.HandleAsync(request2, nextMock.Object);
        
        // Assert
        Assert.Equal(2, cacheKeys.Count);
        Assert.Contains("MediatorNet:Cache:TestCacheableRequest:key1", cacheKeys);
        Assert.Contains("MediatorNet:Cache:TestCacheableRequest:key2", cacheKeys);
        nextMock.Verify(n => n(), Times.Exactly(2));
    }
    
    #region Test Types
    
    public record TestCacheableRequest(string CacheKey) : IRequest<string>, ICacheableRequest<string>
    {
        public TimeSpan CacheExpirationDuration => TimeSpan.FromMinutes(5);
        
        public CacheItemPriority CachePriority => CacheItemPriority.Normal;
    }
    
    #endregion
}