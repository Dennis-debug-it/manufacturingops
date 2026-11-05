using Ecosys.Infrastructure.Services;

namespace Ecosys.Api.Common;

/// <summary>
/// The checks every stored upload passes: an allowed type, an extension that matches it, a size
/// limit, and leading bytes that really are that type (EC-029), so a script named .jpg and posted
/// as image/jpeg is not stored as one.
/// </summary>
public static class UploadedFileRules
{
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string[]> AllowedExtensionsByMimeType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/png"] = [".png"],
        ["image/webp"] = [".webp"],
        ["application/pdf"] = [".pdf"],
        ["application/msword"] = [".doc"],
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
        ["application/vnd.ms-excel"] = [".xls"],
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [".xlsx"],
        ["text/csv"] = [".csv"]
    };

    /// <summary>The problem with the file, or null when it may be stored.</summary>
    public static async Task<string?> CheckAsync(IFormFile? file, long maxBytes, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return "No file was provided.";

        if (file.Length > maxBytes)
            return $"File exceeds the {maxBytes / (1024 * 1024)} MB size limit.";

        var mimeType = file.ContentType ?? "application/octet-stream";
        if (!AllowedExtensionsByMimeType.TryGetValue(mimeType, out var allowedExtensions))
            return $"File type '{mimeType}' is not permitted.";

        var fileExtension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(fileExtension) || !allowedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
            return $"File extension '{fileExtension}' does not match an allowed file type.";

        if (!await ContentMatchesDeclaredTypeAsync(file, mimeType, cancellationToken))
            return $"File contents do not match the declared type '{mimeType}'.";

        return null;
    }

    /// <summary>
    /// Samples the head of the upload and asks <see cref="FileContentSignature"/> whether those
    /// bytes are consistent with the declared type. A separate read stream is opened so the upload
    /// itself still starts at byte zero, whatever the form buffered the body into.
    /// </summary>
    private static async Task<bool> ContentMatchesDeclaredTypeAsync(IFormFile file, string mimeType, CancellationToken cancellationToken)
    {
        var buffer = new byte[FileContentSignature.SampleSizeBytes];
        await using var probe = file.OpenReadStream();
        var read = await probe.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        return FileContentSignature.Matches(mimeType, buffer.AsSpan(0, read));
    }
}
