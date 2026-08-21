using System.Text.Json;
using ProductManagement.Application.Common.Interfaces;
using StackExchange.Redis;

namespace ProductManagement.Infrastructure.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    public RedisCacheService(IConnectionMultiplexer redis) => _redis = redis;

    private IDatabase Db => _redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        var value = await Db.StringGetAsync(key);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>((string)value!);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class =>
        Db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);

    public Task RemoveAsync(string key, CancellationToken ct) => Db.KeyDeleteAsync(key);

    public Task<long> IncrementVersionAsync(string versionKey, CancellationToken ct) => Db.StringIncrementAsync(versionKey);
}
