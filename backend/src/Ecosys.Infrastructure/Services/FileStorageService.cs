using Ecosys.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public record UploadResult(string StoragePath, string PublicUrl, long FileSize);

/// <summary>
/// Checks that the leading bytes of an upload actually look like the content type the client
/// declared (EC-029). The declared type is attacker-controlled — a file of script bytes named
/// <c>polyglot.jpg</c> and posted as <c>image/jpeg</c> passed every allowlist check — so the bytes
/// get the final word.
/// <para>
/// This is defence in depth, not the only defence: attachments are already served with
/// <c>Content-Disposition: attachment</c> and <c>X-Content-Type-Options: nosniff</c>, and a
/// cross-tenant download is a 404. So the goal is to stop a payload whose bytes contradict its
/// declared type, not to prove a file is a well-formed document — that would need a parser per
/// format and would reject legitimate files.
/// </para>
/// <para>
/// Fail-closed by design: a content type this class does not know how to fingerprint returns
/// <c>false</c>. Callers must gate on their own allowlist first and keep the two in step.
/// </para>
/// </summary>
public static class FileContentSignature
{
    /// <summary>
    /// How many leading bytes the caller should hand to <see cref="Matches"/>. Every binary
    /// signature below fits in 12 bytes; the rest is context for the text probe used by CSV.
    /// </summary>
    public const int SampleSizeBytes = 512;

    /// <summary>
    /// True when <paramref name="header"/> — the first <see cref="SampleSizeBytes"/> bytes (or the
    /// whole file, if shorter) — is consistent with <paramref name="mimeType"/>.
    /// </summary>
    public static bool Matches(string? mimeType, ReadOnlySpan<byte> header)
    {
        if (string.IsNullOrWhiteSpace(mimeType) || header.IsEmpty)
        {
            return false;
        }

        return mimeType.Trim().ToLowerInvariant() switch
        {
            // SOI marker, then the first byte of the following marker — always 0xFF.
            "image/jpeg" => header.StartsWith(JpegSignature),

            "image/png" => header.StartsWith(PngSignature),

            // RIFF container: "RIFF" <4-byte length> "WEBP".
            "image/webp" => header.Length >= 12
                && header.StartsWith("RIFF"u8)
                && header[8..12].SequenceEqual("WEBP"u8),

            "application/pdf" => header.StartsWith("%PDF-"u8),

            // Legacy Office binaries are OLE2 compound documents; the container is the same for
            // .doc and .xls, so this proves "an Office binary", not which one. The extension check
            // in the controller already pins that down, and a .doc renamed .xls is not a threat.
            "application/msword" or "application/vnd.ms-excel" => header.StartsWith(Ole2Signature),

            // OOXML files are ZIP archives. PK\x03\x04 is a local file header; the other two are the
            // empty and spanned-archive markers, accepted so an odd-but-real producer is not rejected.
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" =>
                    header.StartsWith(ZipLocalFileHeader)
                    || header.StartsWith(ZipEmptyArchive)
                    || header.StartsWith(ZipSpannedArchive),

            // CSV has no magic number, so the strongest honest statement is "this is text".
            "text/csv" => LooksLikeText(header),

            _ => false
        };
    }

    // Each signature is a typed property rather than an inline collection expression: an untyped
    // `[0xFF, 0xD8, 0xFF]` infers ReadOnlySpan<int> (integer literals default to int) and will not
    // bind to StartsWith on a ReadOnlySpan<byte>. The explicit return type pins the element type.
    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> Ole2Signature => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static ReadOnlySpan<byte> ZipLocalFileHeader => [0x50, 0x4B, 0x03, 0x04];
    private static ReadOnlySpan<byte> ZipEmptyArchive => [0x50, 0x4B, 0x05, 0x06];
    private static ReadOnlySpan<byte> ZipSpannedArchive => [0x50, 0x4B, 0x07, 0x08];
    private static ReadOnlySpan<byte> Utf16LeBom => [0xFF, 0xFE];
    private static ReadOnlySpan<byte> Utf16BeBom => [0xFE, 0xFF];
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// A text probe for the one allowlisted format that carries no signature. Binary payloads —
    /// images, archives, PE/ELF executables, OLE2 documents — all put NUL or other C0 control bytes
    /// in their first few hundred bytes, while text of any encoding does not. A UTF-16 byte-order
    /// mark is accepted up front precisely because UTF-16 text is full of NULs.
    /// </summary>
    private static bool LooksLikeText(ReadOnlySpan<byte> header)
    {
        // UTF-16 LE/BE BOM — accept, the NUL-free rule below cannot apply.
        if (header.StartsWith(Utf16LeBom) || header.StartsWith(Utf16BeBom))
        {
            return true;
        }

        // Skip a UTF-8 BOM; Excel writes one on CSV export.
        var body = header.StartsWith(Utf8Bom) ? header[3..] : header;

        foreach (var value in body)
        {
            var isAllowedControl = value is 0x09 or 0x0A or 0x0D; // tab, LF, CR
            if ((value < 0x20 && !isAllowedControl) || value == 0x7F)
            {
                return false;
            }
        }

        return true;
    }
}

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

        // EC-014: the byte count must come from the write itself. Reading `new FileInfo(path).Length`
        // inside the `return` expression stat-ed the file while the FileStream was still open and its
        // buffer unflushed, so the OS reported 0 and every attachment was persisted with FileSize 0.
        // The destination stream's position after the copy is the number of bytes written, so no
        // filesystem stat is needed at all. The explicit scope also guarantees the handle is closed
        // (and the bytes durable) before the result is returned.
        long bytesWritten;
        await using (var file = File.Create(fullPath))
        {
            await stream.CopyToAsync(file, ct);
            await file.FlushAsync(ct);
            bytesWritten = file.Position;
        }

        var storagePath = relativePath.Replace('\\', '/');
        return new UploadResult(storagePath, GetPublicUrl(storagePath), bytesWritten);
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
