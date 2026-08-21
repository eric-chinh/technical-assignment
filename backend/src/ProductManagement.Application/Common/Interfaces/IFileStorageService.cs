namespace ProductManagement.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, long productId, CancellationToken ct);
    Task DeleteAsync(string url, CancellationToken ct);
}
