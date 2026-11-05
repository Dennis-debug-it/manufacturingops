using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record OperatorAssetView(Guid TenantId, string CompanyName, Guid AssetId, string AssetCode, string AssetName, string? Location, string? OpenJobNumber, string? OpenJobStatus);

public sealed record OperatorReport(string Description, string ReporterName, bool MachineStopped, string? Phone = null);

public sealed record OperatorReportResult(string WorkOrderNumber, bool AddedToOpenJob);

public sealed record OperatorPhoto(Stream Content, string FileName, string MimeType);

/// <summary>
/// Fault reports from operators who scan an asset's label without logging in. The label link
/// carries the tenant's secret report key; a wrong key or tag gets the same "not found" so neither
/// can be probed. A report on a machine that already has an open reported fault is added to that
/// job instead of opening a duplicate.
/// </summary>
public interface IOperatorReportService
{
    Task<OperatorAssetView> GetAssetAsync(string key, string tag, CancellationToken cancellationToken = default);

    Task<OperatorReportResult> SubmitAsync(string key, string tag, OperatorReport report, OperatorPhoto? photo, CancellationToken cancellationToken = default);
}

internal sealed class OperatorReportService(
    AppDbContext dbContext,
    IFactoryWorkOrderService workOrderService,
    IAssetTagNumberService tagNumberService,
    IFileStorageService fileStorageService,
    IPasswordHasher<User> passwordHasher,
    INotificationDispatcher? notificationDispatcher = null) : IOperatorReportService
{
    public const string NotificationKey = "factory-job.reported";

    /// <summary>More reports than this on one asset within an hour are refused: someone is misusing the link.</summary>
    internal const int MaxReportsPerAssetPerHour = 5;

    /// <summary>A reported fault this recent and still open absorbs new reports on the same machine.</summary>
    private static readonly TimeSpan MergeWindow = TimeSpan.FromHours(24);

    public async Task<OperatorAssetView> GetAssetAsync(string key, string tag, CancellationToken cancellationToken = default)
    {
        var (settings, asset, tenant) = await ResolveAsync(key, tag, cancellationToken);
        var open = await FindOpenReportAsync(settings.TenantId, asset.Id, cancellationToken);
        var location = asset.OrgNodeId.HasValue
            ? await dbContext.OrgNodes.Where(x => x.Id == asset.OrgNodeId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        return new OperatorAssetView(settings.TenantId, tenant.CompanyName, asset.Id, asset.AssetCode, asset.AssetName, location, open?.WorkOrderNumber, open?.Status);
    }

    public async Task<OperatorReportResult> SubmitAsync(string key, string tag, OperatorReport report, OperatorPhoto? photo, CancellationToken cancellationToken = default)
    {
        var description = report.Description?.Trim();
        var reporter = report.ReporterName?.Trim();
        if (string.IsNullOrWhiteSpace(description) || description.Length > 1000)
        {
            throw new BusinessRuleException("Describe the fault (up to 1000 characters).");
        }

        if (string.IsNullOrWhiteSpace(reporter) || reporter.Length > 100)
        {
            throw new BusinessRuleException("Give your name.");
        }

        var (settings, asset, _) = await ResolveAsync(key, tag, cancellationToken);
        var hourAgo = DateTime.UtcNow.AddHours(-1);
        var recent = await dbContext.WorkOrderEvents.CountAsync(x => x.TenantId == settings.TenantId && x.EventType == "OperatorReport"
            && x.CreatedAt >= hourAgo && x.WorkOrder!.AssetId == asset.Id, cancellationToken);
        if (recent >= MaxReportsPerAssetPerHour)
        {
            throw new BusinessRuleException("This machine has had several reports in the last hour. Tell your supervisor directly.");
        }

        var operatorUser = await GetOrCreateOperatorUserAsync(settings.TenantId, cancellationToken);
        var who = string.IsNullOrWhiteSpace(report.Phone) ? reporter : $"{reporter} ({report.Phone.Trim()})";
        var open = await FindOpenReportAsync(settings.TenantId, asset.Id, cancellationToken);
        WorkOrder workOrder;
        var merged = open is not null;
        if (open is not null)
        {
            workOrder = open;
        }
        else
        {
            var title = description.Length <= 80 ? description : $"{description[..77]}...";
            workOrder = await workOrderService.CreateAsync(settings.TenantId, new FactoryActor(null, who), new CreateFactoryWorkOrder(
                title,
                FactoryWorkTypes.Corrective,
                report.MachineStopped || asset.Criticality == AssetCriticality.A ? "High" : "Medium",
                AssetId: asset.Id,
                Description: description,
                IsBreakdown: report.MachineStopped,
                Source: FactoryRequestSources.Operator,
                RequestedByName: who), cancellationToken);
        }

        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = settings.TenantId,
            WorkOrderId = workOrder.Id,
            EventType = "OperatorReport",
            Status = workOrder.Status,
            Message = $"{(report.MachineStopped ? "Machine stopped. " : string.Empty)}{description}",
            Note = who,
            OccurredAt = DateTime.UtcNow
        });

        if (photo is not null)
        {
            var stored = await fileStorageService.UploadAsync(photo.Content, photo.FileName, photo.MimeType, settings.TenantId, cancellationToken);
            var attachment = new Attachment
            {
                TenantId = settings.TenantId,
                EntityType = "WorkOrder",
                EntityId = workOrder.Id,
                FileName = photo.FileName,
                FileSize = stored.FileSize,
                MimeType = photo.MimeType,
                StoragePath = stored.StoragePath,
                UploadedByUserId = operatorUser.Id
            };
            attachment.PublicUrl = $"/api/attachments/{attachment.Id}/download";
            dbContext.Attachments.Add(attachment);
            dbContext.WorkOrderPhotoEvidence.Add(new WorkOrderPhotoEvidence
            {
                TenantId = settings.TenantId,
                WorkOrderId = workOrder.Id,
                AttachmentId = attachment.Id,
                Caption = $"Reported by {reporter}",
                Category = "Before",
                UploadedByUserId = operatorUser.Id
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyAsync(workOrder, asset, who, merged, cancellationToken);
        return new OperatorReportResult(workOrder.WorkOrderNumber, merged);
    }

    private async Task<(FactorySettings Settings, Asset Asset, Tenant Tenant)> ResolveAsync(string key, string tag, CancellationToken cancellationToken)
    {
        const string notFound = "This label link is not valid. Ask your supervisor to report the fault.";
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(tag) || key.Length > 64)
        {
            throw new NotFoundException(notFound);
        }

        var settings = await dbContext.FactorySettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperatorReportKey == key.Trim() && x.OperatorReportingEnabled, cancellationToken)
            ?? throw new NotFoundException(notFound);
        var tenant = await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == settings.TenantId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException(notFound);

        var number = tagNumberService.NormalizeCode(tag);
        var assetId = await dbContext.AssetTags
            .Where(x => x.TenantId == settings.TenantId && x.TagNumber == number)
            .Select(x => (Guid?)x.AssetId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(notFound);
        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == settings.TenantId && x.Id == assetId && x.Status != "Inactive", cancellationToken)
            ?? throw new NotFoundException(notFound);
        return (settings, asset, tenant);
    }

    private Task<WorkOrder?> FindOpenReportAsync(Guid tenantId, Guid assetId, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow - MergeWindow;
        string[] done = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];
        return dbContext.WorkOrders
            .Where(x => x.TenantId == tenantId && x.AssetId == assetId && x.ClientId == null && !done.Contains(x.Status)
                && (x.IsBreakdown || x.RequestSource == FactoryRequestSources.Operator) && x.CreatedAt >= since)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>An inactive account that owns operators' uploads; it can't sign in.</summary>
    private async Task<User> GetOrCreateOperatorUserAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var email = $"operator-reports.{tenantId:N}@system.ecosys.invalid";
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Email == email, cancellationToken);
        if (user is not null)
        {
            return user;
        }

        user = new User
        {
            TenantId = tenantId,
            FullName = "Operator fault reports",
            Email = email,
            HasEmail = false,
            Role = AppRoles.Viewer,
            IsActive = false,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        dbContext.Users.Add(user);
        return user;
    }

    private async Task NotifyAsync(WorkOrder workOrder, Asset asset, string who, bool merged, CancellationToken cancellationToken)
    {
        if (notificationDispatcher is null)
        {
            return;
        }

        try
        {
            var recipients = await FactoryRecipients.SupervisorsAsync(dbContext, workOrder.TenantId, workOrder.OrgNodeId, cancellationToken);
            if (recipients.Count == 0)
            {
                return;
            }

            await notificationDispatcher.NotifyAsync(new NotificationRequest(
                workOrder.TenantId,
                NotificationKey,
                recipients,
                workOrder.IsBreakdown ? $"Breakdown reported: {asset.AssetName}" : $"Fault reported: {asset.AssetName}",
                $"{who}{(merged ? $" added to {workOrder.WorkOrderNumber}" : $" raised {workOrder.WorkOrderNumber}")}: {workOrder.Title}",
                $"/maintenance/jobs/{workOrder.Id}",
                nameof(WorkOrder),
                workOrder.Id,
                new Dictionary<string, string?>
                {
                    ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                    ["AssetName"] = asset.AssetName,
                    ["AssetCode"] = asset.AssetCode,
                    ["ReportedBy"] = who,
                    ["Description"] = workOrder.Description ?? workOrder.Title,
                },
                workOrder.IsBreakdown ? NotificationSeverities.Critical : NotificationSeverities.Warning), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed notification must not lose the report.
        }
    }
}
