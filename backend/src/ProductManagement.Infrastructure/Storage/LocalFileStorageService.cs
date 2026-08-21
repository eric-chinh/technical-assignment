using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, long productId, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var productDir = Path.Combine(_rootPath, "products", productId.ToString());
        Directory.CreateDirectory(productDir);

        var filePath = Path.Combine(productDir, uniqueFileName);
        await using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream, ct);

        return $"/uploads/products/{productId}/{uniqueFileName}";
    }

    public Task DeleteAsync(string url, CancellationToken ct)
    {
        var relativePath = url.TrimStart('/').Replace("uploads/", string.Empty, StringComparison.Ordinal);
        var filePath = Path.Combine(_rootPath, relativePath);
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }
}
