using System.Text;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class AssetDocumentTests
{
    [Fact]
    public async Task Document_KeepsEveryVersion_AndDownloadsTheOneAskedFor()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (context, asset) = await SeedAsync(db);
        var storage = new MemoryStorage();
        var documents = Controller(db, context, storage);

        var created = Ok(await documents.Create(asset.Id, new CreateAssetDocumentForm
        {
            Title = "Dryer O&M manual",
            DocumentType = "manual",
            File = Pdf("manual-2018.pdf", "first edition"),
            Notes = "As supplied",
        }, CancellationToken.None));
        Assert.Equal(1, created.CurrentVersion);
        Assert.Equal(AssetDocumentTypes.Manual, created.DocumentType);

        var updated = Ok(await documents.AddVersion(created.Id, new AddDocumentVersionForm { File = Pdf("manual-2024.pdf", "second edition"), Notes = "Revised burner section" }, CancellationToken.None));
        Assert.Equal(2, updated.CurrentVersion);
        Assert.Equal([2, 1], updated.Versions.Select(x => x.VersionNumber).ToArray());
        Assert.Equal("manual-2024.pdf", updated.Versions.First().FileName);

        var current = Assert.IsType<FileStreamResult>(await documents.Download(created.Id, null, CancellationToken.None));
        Assert.Equal("manual-2024.pdf", current.FileDownloadName);
        var first = Assert.IsType<FileStreamResult>(await documents.Download(created.Id, 1, CancellationToken.None));
        Assert.Contains("first edition", await new StreamReader(first.FileStream).ReadToEndAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => documents.Download(created.Id, 9, CancellationToken.None));

        var listed = Assert.Single(Ok(await documents.List(asset.Id, false, CancellationToken.None)));
        Assert.Equal(2, listed.Versions.Count);
    }

    [Fact]
    public async Task Upload_IsCheckedLikeEveryOtherFile()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (context, asset) = await SeedAsync(db);
        var documents = Controller(db, context, new MemoryStorage());

        // A script posted as a PDF is refused on its content, not just its name.
        var fake = new FormFile(new MemoryStream(Encoding.ASCII.GetBytes("<script>alert(1)</script>")), 0, 25, "file", "manual.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
        await Assert.ThrowsAsync<BusinessRuleException>(() => documents.Create(asset.Id, new CreateAssetDocumentForm { Title = "Bad", DocumentType = "Manual", File = fake }, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => documents.Create(asset.Id, new CreateAssetDocumentForm { Title = "No file", DocumentType = "Manual" }, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => documents.Create(asset.Id, new CreateAssetDocumentForm { Title = "Odd type", DocumentType = "Poem", File = Pdf("a.pdf", "x") }, CancellationToken.None));
        Assert.Empty(await db.AssetDocuments.ToListAsync());
    }

    [Fact]
    public async Task Certificate_ShowsWhenItHasExpired_AndArchivedDocumentsAreHidden()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (context, asset) = await SeedAsync(db);
        var documents = Controller(db, context, new MemoryStorage());

        var certificate = Ok(await documents.Create(asset.Id, new CreateAssetDocumentForm
        {
            Title = "Boiler inspection certificate",
            DocumentType = "Certificate",
            ExpiryDate = DateTime.UtcNow.Date.AddDays(-1),
            File = Pdf("cert.pdf", "DOSH"),
        }, CancellationToken.None));
        Assert.True(certificate.IsExpired);

        Ok(await documents.Archive(certificate.Id, CancellationToken.None));
        Assert.Empty(Ok(await documents.List(asset.Id, false, CancellationToken.None)));
        Assert.Single(Ok(await documents.List(asset.Id, true, CancellationToken.None)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => documents.AddVersion(certificate.Id, new AddDocumentVersionForm { File = Pdf("cert2.pdf", "new") }, CancellationToken.None));
    }

    private static async Task<(FactoryTenantContext Context, Asset Asset)> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var factory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var asset = new Asset { TenantId = tenant.Id, AssetName = "Fluid bed dryer", AssetCode = "FBD-01", OrgNodeId = factory.Id };
        db.OrgNodes.Add(factory);
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return (new FactoryTenantContext(tenant.Id, admin.Id), asset);
    }

    private static IFormFile Pdf(string name, string text)
    {
        var bytes = Encoding.ASCII.GetBytes($"%PDF-1.7\n{text}\n%%EOF");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static AssetDocumentsController Controller(AppDbContext db, FactoryTenantContext context, IFileStorageService storage) =>
        FactoryAssetRegisterTests.WithHttp(new AssetDocumentsController(db, context, FactoryTestSupport.Access(context),
            FactoryTestSupport.Scope(db, context), storage, FactoryTestSupport.Audit(db)));
}
