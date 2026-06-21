using System.Text;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class AttachmentFlowTests
{
    [Fact]
    public async Task Upload_ValidAttachment_PersistsAndReturnsAuthorizedDownloadUrl()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var fileStorage = new FakeFileStorageService();
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.User.Id, fileStorage);

        var file = CreateFormFile("manual.pdf", "application/pdf", "pdf-content");
        var result = await controller.Upload("WorkOrder", fixture.WorkOrder.Id.ToString(), file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AttachmentDto>(ok.Value);
        Assert.Equal("manual.pdf", dto.FileName);
        Assert.Equal($"/api/attachments/{dto.Id}/download", dto.PublicUrl);

        var stored = await dbContext.Attachments.SingleAsync();
        Assert.Equal(fixture.Tenant.Id, stored.TenantId);
        Assert.Equal("WorkOrder", stored.EntityType);
        Assert.StartsWith($"/api/attachments/{stored.Id}/download", stored.PublicUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Upload_RejectsDangerousExtensionEvenWhenMimeTypeLooksValid()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.User.Id, new FakeFileStorageService());

        var file = CreateFormFile("payload.exe", "application/pdf", "not-really-a-pdf");
        var result = await controller.Upload("WorkOrder", fixture.WorkOrder.Id.ToString(), file, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("extension", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await dbContext.Attachments.ToListAsync());
    }

    [Fact]
    public async Task Upload_RejectsFilesOverFiveMegabytes()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.User.Id, new FakeFileStorageService());

        var file = CreateBinaryFormFile("large.pdf", "application/pdf", new byte[5 * 1024 * 1024 + 1]);
        var result = await controller.Upload("WorkOrder", fixture.WorkOrder.Id.ToString(), file, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("5 MB", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await dbContext.Attachments.ToListAsync());
    }

    [Fact]
    public async Task ListByEntity_ReturnsOnlyTenantScopedAttachments()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        dbContext.Attachments.Add(new Attachment
        {
            TenantId = fixture.Tenant.Id,
            EntityType = "WorkOrder",
            EntityId = fixture.WorkOrder.Id,
            FileName = "job-card.pdf",
            FileSize = 42,
            MimeType = "application/pdf",
            StoragePath = "tenant/job-card.pdf",
            PublicUrl = string.Empty,
            UploadedByUserId = fixture.User.Id
        });
        dbContext.Attachments.Add(new Attachment
        {
            TenantId = Guid.NewGuid(),
            EntityType = "WorkOrder",
            EntityId = fixture.WorkOrder.Id,
            FileName = "other-tenant.pdf",
            FileSize = 42,
            MimeType = "application/pdf",
            StoragePath = "other/job-card.pdf",
            PublicUrl = string.Empty,
            UploadedByUserId = Guid.NewGuid()
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.User.Id, new FakeFileStorageService());
        var result = await controller.GetByEntity("WorkOrder", fixture.WorkOrder.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var attachments = Assert.IsAssignableFrom<IReadOnlyCollection<AttachmentDto>>(ok.Value);
        var attachment = Assert.Single(attachments);
        Assert.Equal("job-card.pdf", attachment.FileName);
    }

    [Fact]
    public async Task Delete_RemovesAttachment_WhenUserIsUploader()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var fileStorage = new FakeFileStorageService();
        fileStorage.Store("tenant/manual.pdf", Encoding.UTF8.GetBytes("manual"));

        var attachment = new Attachment
        {
            TenantId = fixture.Tenant.Id,
            EntityType = "WorkOrder",
            EntityId = fixture.WorkOrder.Id,
            FileName = "manual.pdf",
            FileSize = 6,
            MimeType = "application/pdf",
            StoragePath = "tenant/manual.pdf",
            PublicUrl = string.Empty,
            UploadedByUserId = fixture.User.Id
        };
        dbContext.Attachments.Add(attachment);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, fixture.Tenant.Id, fixture.User.Id, fileStorage);
        var result = await controller.Delete(attachment.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await dbContext.Attachments.ToListAsync());
        Assert.False(fileStorage.Exists("tenant/manual.pdf"));
    }

    [Fact]
    public async Task CrossTenantAccess_IsBlockedForDownload()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedFixtureAsync(dbContext);
        var fileStorage = new FakeFileStorageService();
        fileStorage.Store("tenant/manual.pdf", Encoding.UTF8.GetBytes("manual"));

        var attachment = new Attachment
        {
            TenantId = fixture.Tenant.Id,
            EntityType = "WorkOrder",
            EntityId = fixture.WorkOrder.Id,
            FileName = "manual.pdf",
            FileSize = 6,
            MimeType = "application/pdf",
            StoragePath = "tenant/manual.pdf",
            PublicUrl = string.Empty,
            UploadedByUserId = fixture.User.Id
        };
        dbContext.Attachments.Add(attachment);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, Guid.NewGuid(), Guid.NewGuid(), fileStorage);
        await Assert.ThrowsAsync<NotFoundException>(() => controller.Download(attachment.Id, CancellationToken.None));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-attachments-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<FixtureData> SeedFixtureAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Acme Manufacturing",
            Email = "ops@acme.test",
            Country = "Kenya",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            ShowPoweredByEcosys = true
        };

        var user = new User
        {
            TenantId = tenant.Id,
            FullName = "Alice Admin",
            Email = $"alice-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            IsActive = true,
            HasAllBranchAccess = true
        };

        var client = new Client
        {
            TenantId = tenant.Id,
            ClientName = "Acme Manufacturing",
            Email = "client@acme.test",
            IsActive = true
        };

        var site = new Site
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteCode = $"SITE-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Main Plant",
            SiteType = "Factory",
            Status = "Active"
        };

        var asset = new Asset
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetCode = $"ASSET-{Guid.NewGuid():N}".Substring(0, 10),
            AssetName = "Generator 01",
            AssetType = "Generator",
            Status = "Active"
        };

        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id,
            ClientId = client.Id,
            SiteId = site.Id,
            AssetId = asset.Id,
            WorkOrderNumber = $"WO-{Guid.NewGuid():N}".Substring(0, 10),
            Title = "Generator fault",
            Priority = "High",
            Status = "Open",
            AssignmentType = "Unassigned",
            IsPreventiveMaintenance = false
        };

        dbContext.AddRange(tenant, user, client, site, asset, workOrder);
        await dbContext.SaveChangesAsync();
        return new FixtureData(tenant, user, client, site, asset, workOrder);
    }

    private static AttachmentsController CreateController(
        AppDbContext dbContext,
        Guid tenantId,
        Guid userId,
        IFileStorageService fileStorageService)
    {
        return new AttachmentsController(
            dbContext,
            new FakeTenantContext(tenantId, userId),
            fileStorageService);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, string content) =>
        CreateBinaryFormFile(fileName, contentType, Encoding.UTF8.GetBytes(content));

    private static IFormFile CreateBinaryFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed record FixtureData(
        Tenant Tenant,
        User User,
        Client Client,
        Site Site,
        Asset Asset,
        WorkOrder WorkOrder);

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

    private sealed class FakeFileStorageService : IFileStorageService
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        public Task<UploadResult> UploadAsync(Stream stream, string fileName, string mimeType, Guid tenantId, CancellationToken ct)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var path = $"{tenantId}/{Guid.NewGuid():N}-{fileName}";
            _files[path] = memory.ToArray();
            return Task.FromResult(new UploadResult(path, $"https://files.test/{path}", memory.Length));
        }

        public Task<Stream> DownloadAsync(string storagePath, CancellationToken ct)
        {
            var bytes = _files.TryGetValue(storagePath, out var stored) ? stored : [];
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        public Task DeleteAsync(string storagePath, CancellationToken ct)
        {
            _files.Remove(storagePath);
            return Task.CompletedTask;
        }

        public string GetPublicUrl(string storagePath) => $"https://files.test/{storagePath}";

        public void Store(string path, byte[] content) => _files[path] = content;

        public bool Exists(string path) => _files.ContainsKey(path);
    }
}
