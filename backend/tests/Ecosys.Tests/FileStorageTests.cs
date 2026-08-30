using System.Text;
using Ecosys.Infrastructure.Options;
using Ecosys.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

/// <summary>
/// EC-014 regression cover. The uploaded byte count used to be read with
/// <c>new FileInfo(path).Length</c> inside the <c>return</c> expression, while the destination
/// FileStream was still open and buffered, so every attachment was stored with FileSize 0.
/// </summary>
public sealed class FileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "ecosys-file-storage-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(14)]     // smaller than any stream buffer — the case that always reported 0
    [InlineData(25)]
    [InlineData(104)]
    [InlineData(100_000)]
    public async Task UploadAsync_ReportsTheNumberOfBytesActuallyWritten(int size)
    {
        var service = CreateService();
        var payload = new byte[size];
        Random.Shared.NextBytes(payload);

        await using var source = new MemoryStream(payload);
        var result = await service.UploadAsync(source, "qa.pdf", "application/pdf", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(size, result.FileSize);
        Assert.Equal(size, new FileInfo(ResolveFullPath(result.StoragePath)).Length);
    }

    [Fact]
    public async Task UploadAsync_WritesTheFileContentsIntact()
    {
        var service = CreateService();
        var payload = Encoding.UTF8.GetBytes("attachment contents");

        await using var source = new MemoryStream(payload);
        var result = await service.UploadAsync(source, "notes.txt", "text/plain", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(payload.Length, result.FileSize);
        Assert.Equal(payload, await File.ReadAllBytesAsync(ResolveFullPath(result.StoragePath)));

        await using var downloaded = await service.DownloadAsync(result.StoragePath, CancellationToken.None);
        using var buffer = new MemoryStream();
        await downloaded.CopyToAsync(buffer);
        Assert.Equal(payload, buffer.ToArray());
    }

    private LocalFileStorageService CreateService() =>
        new(Microsoft.Extensions.Options.Options.Create(new StorageOptions
        {
            Provider = "Local",
            LocalBasePath = _root,
            BaseUrl = "http://localhost:5000/files",
        }));

    private string ResolveFullPath(string storagePath) =>
        Path.Combine(_root, storagePath.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
