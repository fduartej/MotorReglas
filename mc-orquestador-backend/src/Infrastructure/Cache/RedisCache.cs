using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Orchestrator.Infrastructure.Cache;

public class RedisCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase? _database;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RedisCache> _logger;
    private readonly bool _isRedisAvailable;

    public RedisCache(IConnectionMultiplexer redis, IMemoryCache memoryCache, ILogger<RedisCache> logger)
    {
        _redis = redis;
        _memoryCache = memoryCache;
        _logger = logger;

        try
        {
            _database = redis.GetDatabase();
            _isRedisAvailable = redis.IsConnected;
            if (!_isRedisAvailable)
            {
                _logger.LogWarning("Redis is not connected. Using in-memory cache fallback.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis is not available. Using in-memory cache fallback.");
            _isRedisAvailable = false;
            _database = null;
        }
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            if (_isRedisAvailable && _database != null)
            {
                var value = await _database.StringGetAsync(key);
                if (!value.HasValue)
                {
                    _logger.LogDebug("Redis cache miss for key: {Key}", key);
                    return null;
                }

                var result = JsonSerializer.Deserialize<T>(value!);
                _logger.LogDebug("Redis cache hit for key: {Key}", key);
                return result;
            }
            else
            {
                // Fallback to memory cache
                if (_memoryCache.TryGetValue(key, out var cachedValue) && cachedValue is T result)
                {
                    _logger.LogDebug("Memory cache hit for key: {Key}", key);
                    return result;
                }
                
                _logger.LogDebug("Memory cache miss for key: {Key}", key);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
            return null;
        }
    }

    public async Task<object?> GetAsync(string key)
    {
        try
        {
            if (_database == null)
            {
                _logger.LogDebug("Redis not available for key: {Key}", key);
                return null;
            }
            
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }

            var result = JsonSerializer.Deserialize<object>(value!);
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            if (_isRedisAvailable && _database != null)
            {
                var serializedValue = JsonSerializer.Serialize(value);
                await _database.StringSetAsync(key, serializedValue, expiry);
                _logger.LogDebug("Cached value in Redis for key: {Key} with expiry: {Expiry}", key, expiry);
            }
            else
            {
                // Fallback to memory cache
                var options = new MemoryCacheEntryOptions();
                if (expiry.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiry.Value;
                }
                _memoryCache.Set(key, value, options);
                _logger.LogDebug("Cached value in memory for key: {Key} with expiry: {Expiry}", key, expiry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
        }
    }

    public async Task SetAsync(string key, object value, TimeSpan? expiry = null)
    {
        try
        {
            if (_isRedisAvailable && _database != null)
            {
                var serializedValue = JsonSerializer.Serialize(value);
                await _database.StringSetAsync(key, serializedValue, expiry);
                _logger.LogDebug("Cached value in Redis for key: {Key} with expiry: {Expiry}", key, expiry);
            }
            else
            {
                // Fallback to memory cache
                var options = new MemoryCacheEntryOptions();
                if (expiry.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiry.Value;
                }
                _memoryCache.Set(key, value, options);
                _logger.LogDebug("Cached value in memory for key: {Key} with expiry: {Expiry}", key, expiry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            if (_database == null)
            {
                _logger.LogDebug("Redis not available for removing key: {Key}", key);
                return;
            }
            
            await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Removed cache key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key: {Key}", key);
        }
    }

    public string BuildCacheKey(string template, Dictionary<string, object> inputs)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        var result = template;
        foreach (var input in inputs)
        {
            result = result.Replace($"{{{input.Key}}}", input.Value?.ToString() ?? "");
        }

        _logger.LogDebug("Built cache key: {Key} from template: {Template}", result, template);
        return result;
    }

    public async Task<(object? Data, bool FromCache)> GetOrSetAsync(
        string cacheKey,
        Func<Task<object>> dataFactory,
        TimeSpan? expiry = null)
    {
        try
        {
            // Try to get from cache first
            var cachedData = await GetAsync(cacheKey);
            if (cachedData != null)
            {
                return (cachedData, true);
            }

            // Get from data factory
            var data = await dataFactory();
            
            // Cache the result
            if (data != null)
            {
                await SetAsync(cacheKey, data, expiry);
            }

            return (data, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrSetAsync for key: {Key}", cacheKey);
            
            // Fallback to direct data factory call
            var data = await dataFactory();
            return (data, false);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            if (_database == null)
            {
                _logger.LogDebug("Redis not available for checking key exists: {Key}", key);
                return false;
            }
            
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if key exists: {Key}", key);
            return false;
        }
    }

    public async Task<TimeSpan?> GetTimeToLiveAsync(string key)
    {
        try
        {
            if (_database == null)
            {
                _logger.LogDebug("Redis not available for getting TTL for key: {Key}", key);
                return null;
            }
            
            return await _database.KeyTimeToLiveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TTL for key: {Key}", key);
            return null;
        }
    }

    public async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
    {
        try
        {
            if (_database == null)
            {
                _logger.LogDebug("Redis not available for setting expiry for key: {Key}", key);
                return false;
            }
            
            return await _database.KeyExpireAsync(key, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting expiry for key: {Key}", key);
            return false;
        }
    }
}
