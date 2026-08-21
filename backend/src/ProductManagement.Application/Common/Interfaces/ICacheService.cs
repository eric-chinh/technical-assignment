namespace ProductManagement.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
    Task RemoveAsync(string key, CancellationToken ct);
    Task<long> GetVersionAsync(string versionKey, CancellationToken ct);
    Task<long> IncrementVersionAsync(string versionKey, CancellationToken ct);
}
