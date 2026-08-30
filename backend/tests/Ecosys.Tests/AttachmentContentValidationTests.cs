using System.Text;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// EC-029 cover. The upload allowlist used to trust the client-declared content type, so a file of
/// script bytes named <c>polyglot.jpg</c> and posted as <c>image/jpeg</c> was stored as a JPEG.
/// The leading bytes now have to agree with the declared type.
/// </summary>
public sealed class AttachmentContentValidationTests
{
    // Minimal but genuine headers for every type on the controller's allowlist. Only the leading
    // bytes are load-bearing — the validator fingerprints the container, it does not parse the file.
    public static TheoryData<string, string, byte[]> AllowlistedPayloads() => new()
    {
        { "photo.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, .. "JFIF"u8, 0x00] },
        { "photo.jpeg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xDB, 0x00, 0x43] },
        { "diagram.png", "image/png", [0x89, .. "PNG"u8, 0x0D, 0x0A, 0x1A, 0x0A, .. "IHDR"u8] },
        { "diagram.webp", "image/webp", [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBP"u8, .. "VP8 "u8] },
        // "%PDF-1.7" then the binary comment line real writers emit.
        { "manual.pdf", "application/pdf", [.. "%PDF-1.7\n"u8, 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A] },
        { "report.doc", "application/msword", Ole2Header },
        { "sheet.xls", "application/vnd.ms-excel", Ole2Header },
        {
            "report.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00]
        },
        {
            "sheet.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00]
        },
        { "assets.csv", "text/csv", [.. "code,name\r\nAST-1,Generator 01\r\n"u8] },
        // Excel writes a UTF-8 BOM on CSV export.
        { "assets-bom.csv", "text/csv", [0xEF, 0xBB, 0xBF, .. "code,name\r\nAST-1,Generator\r\n"u8] },
    };

    // Payloads whose bytes contradict the declared type. Each one passes the MIME allowlist and the
    // extension check, so before EC-029 every one of these was accepted.
    public static TheoryData<string, string, byte[]> ContradictoryPayloads() => new()
    {
        // The verified reproduction: script bytes wearing a .jpg name and an image/jpeg declaration.
        { "polyglot.jpg", "image/jpeg", [.. "<script>alert(1)</script>"u8] },
        { "polyglot.png", "image/png", [.. "<script>alert(1)</script>"u8] },
        // Real image bytes, but the wrong image type — PNG bytes declared as JPEG.
        { "mislabelled.jpg", "image/jpeg", [0x89, .. "PNG"u8, 0x0D, 0x0A, 0x1A, 0x0A] },
        // RIFF container that is not WebP (a WAV).
        { "audio.webp", "image/webp", [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVE"u8] },
        // HTML posing as a PDF.
        { "invoice.pdf", "application/pdf", [.. "<html><body>hello</body></html>"u8] },
        // An OOXML declaration over a plain-text body (no ZIP local file header).
        {
            "notes.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [.. "not really a docx"u8]
        },
        // Legacy Office declaration over ZIP bytes (a .docx renamed .doc).
        { "notes.doc", "application/msword", [0x50, 0x4B, 0x03, 0x04] },
        // A Windows executable renamed to .csv: MZ header, and NUL bytes throughout.
        { "payload.csv", "text/csv", [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00] },
        // A PNG renamed .csv.
        { "image.csv", "text/csv", [0x89, .. "PNG"u8, 0x0D, 0x0A, 0x1A, 0x0A] },
    };

    private static byte[] Ole2Header => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00, 0x00, 0x00];

    [Theory]
    [MemberData(nameof(AllowlistedPayloads))]
    public async Task Upload_AcceptsGenuineFilesOfEveryAllowlistedType(string fileName, string mimeType, byte[] content)
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedWorkOrderAsync(dbContext);
        var storage = new RecordingFileStorageService();
        var controller = CreateController(dbContext, fixture.TenantId, fixture.UserId, storage);

        var result = await controller.Upload(
            "WorkOrder",
            fixture.WorkOrderId.ToString(),
            CreateFormFile(fileName, mimeType, content),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AttachmentDto>(ok.Value);
        Assert.Equal(fileName, dto.FileName);
        Assert.Equal(mimeType, dto.MimeType);

        // The whole payload must still reach storage — the validator only peeks at the head.
        Assert.Equal(content, Assert.Single(storage.Uploads).Content);
        Assert.Single(await dbContext.Attachments.ToListAsync());
    }

    [Theory]
    [MemberData(nameof(ContradictoryPayloads))]
    public async Task Upload_RejectsPayloadWhoseBytesContradictTheDeclaredType(string fileName, string mimeType, byte[] content)
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedWorkOrderAsync(dbContext);
        var storage = new RecordingFileStorageService();
        var controller = CreateController(dbContext, fixture.TenantId, fixture.UserId, storage);

        var result = await controller.Upload(
            "WorkOrder",
            fixture.WorkOrderId.ToString(),
            CreateFormFile(fileName, mimeType, content),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = badRequest.Value?.ToString() ?? string.Empty;
        Assert.Contains("do not match", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VALIDATION_ERROR", body, StringComparison.Ordinal);

        // Nothing may be written on either side of the boundary.
        Assert.Empty(storage.Uploads);
        Assert.Empty(await dbContext.Attachments.ToListAsync());
    }

    [Fact]
    public async Task Upload_RejectsContradictoryPayload_BeforeRevealingWhetherTheEntityExists()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedWorkOrderAsync(dbContext);
        var controller = CreateController(dbContext, fixture.TenantId, fixture.UserId, new RecordingFileStorageService());

        // An entity id that does not exist. A NotFoundException here would tell an attacker that the
        // payload check runs after the lookup, so the content check has to come first.
        var result = await controller.Upload(
            "WorkOrder",
            Guid.NewGuid().ToString(),
            CreateFormFile("polyglot.jpg", "image/jpeg", Encoding.UTF8.GetBytes("<script>alert(1)</script>")),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Matches_IsFailClosedForTypesItCannotFingerprint()
    {
        // The controller's allowlist is the gate; anything it has not vetted must not slip through
        // the validator just because no rule matched it.
        Assert.False(FileContentSignature.Matches("image/svg+xml", "<svg/>"u8));
        Assert.False(FileContentSignature.Matches("text/html", "<html>"u8));
        Assert.False(FileContentSignature.Matches("application/octet-stream", [0xFF, 0xD8, 0xFF]));
        Assert.False(FileContentSignature.Matches(null, [0xFF, 0xD8, 0xFF]));
        Assert.False(FileContentSignature.Matches("image/jpeg", ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Matches_IsCaseInsensitiveOnTheDeclaredType()
    {
        Assert.True(FileContentSignature.Matches("IMAGE/JPEG", [0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.True(FileContentSignature.Matches(" application/pdf ", "%PDF-1.4"u8));
    }

    [Fact]
    public void Matches_AcceptsUtf16CsvExportsButNotBinaryNulls()
    {
        // UTF-16 text is full of NUL bytes, so the BOM has to be honoured before the text probe.
        Assert.True(FileContentSignature.Matches("text/csv", [0xFF, 0xFE, 0x63, 0x00, 0x6F, 0x00]));
        Assert.True(FileContentSignature.Matches("text/csv", [0xFE, 0xFF, 0x00, 0x63, 0x00, 0x6F]));
        Assert.False(FileContentSignature.Matches("text/csv", [0x63, 0x6F, 0x00, 0x64, 0x65]));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-attachment-content-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<(Guid TenantId, Guid UserId, Guid WorkOrderId)> SeedWorkOrderAsync(AppDbContext dbContext)
    {
        var tenantId = Guid.NewGuid();
        var workOrder = new WorkOrder
        {
            TenantId = tenantId,
            ClientId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            WorkOrderNumber = $"WO-{Guid.NewGuid():N}"[..10],
            Title = "Generator fault",
            Priority = "High",
            Status = "Open",
            AssignmentType = "Unassigned",
            IsPreventiveMaintenance = false
        };

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();

        return (tenantId, Guid.NewGuid(), workOrder.Id);
    }

    private static AttachmentsController CreateController(
        AppDbContext dbContext,
        Guid tenantId,
        Guid userId,
        IFileStorageService fileStorageService) =>
        new(dbContext, new FakeTenantContext(tenantId, userId), fileStorageService);

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "test@acme.test";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => "Admin";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }

    private sealed class RecordingFileStorageService : IFileStorageService
    {
        public List<(string FileName, string MimeType, byte[] Content)> Uploads { get; } = [];

        public Task<UploadResult> UploadAsync(Stream stream, string fileName, string mimeType, Guid tenantId, CancellationToken ct)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var content = memory.ToArray();
            Uploads.Add((fileName, mimeType, content));

            var path = $"{tenantId}/{Guid.NewGuid():N}-{fileName}";
            return Task.FromResult(new UploadResult(path, $"https://files.test/{path}", content.Length));
        }

        public Task<Stream> DownloadAsync(string storagePath, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(string storagePath, CancellationToken ct) => Task.CompletedTask;

        public string GetPublicUrl(string storagePath) => $"https://files.test/{storagePath}";
    }
}
