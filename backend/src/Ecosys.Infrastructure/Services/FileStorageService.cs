using Ecosys.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public record UploadResult(string StoragePath, string PublicUrl, long FileSize);

public interface IFileStorageService
{
    Task<UploadResult> UploadAsync(Stream stream, string fileName, string mimeType, Guid tenantId, CancellationToken ct);
    Task<Stream> DownloadAsync(string storagePath, CancellationToken ct);
    Task DeleteAsync(string storagePath, CancellationToken ct);
    string GetPublicUrl(string storagePath);
}

public sealed class LocalFileStorageService(IOptions<StorageOptions> options) : IFileStorageService
{
    private readonly StorageOptions _options = options.Value;

    public async Task<UploadResult> UploadAsync(Stream stream, string fileName, string mimeType, Guid tenantId, CancellationToken ct)
    {
        var sanitized = SanitizeFileName(fileName);
        var now = DateTime.UtcNow;
        var relativePath = Path.Combine(
            tenantId.ToString(),
            now.Year.ToString(),
            now.Month.ToString("D2"),
            $"{Guid.NewGuid()}_{sanitized}");

        var fullPath = Path.Combine(_options.LocalBasePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await stream.CopyToAsync(file, ct);

        var storagePath = relativePath.Replace('\\', '/');
        return new UploadResult(storagePath, GetPublicUrl(storagePath), new FileInfo(fullPath).Length);
    }

    public Task<Stream> DownloadAsync(string storagePath, CancellationToken ct)
    {
        var fullPath = ResolveFullPath(storagePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Attachment not found.", fullPath);
        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct)
    {
        var fullPath = ResolveFullPath(storagePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string storagePath) =>
        $"{_options.BaseUrl.TrimEnd('/')}/{storagePath.TrimStart('/')}";

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return $"{safe}{ext}";
    }

    private string ResolveFullPath(string storagePath)
    {
        var basePath = Path.GetFullPath(_options.LocalBasePath);
        var combinedPath = Path.GetFullPath(Path.Combine(basePath, storagePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!combinedPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attachment path is invalid.");
        }

        return combinedPath;
    }
}
