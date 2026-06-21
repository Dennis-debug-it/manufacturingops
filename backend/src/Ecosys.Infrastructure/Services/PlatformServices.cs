using Ecosys.Infrastructure.Persistence;
using Ecosys.Platform.Contracts.Audit;
using Ecosys.Platform.Contracts.Notifications;
using Ecosys.Platform.Contracts.Numbering;
using Ecosys.Platform.Contracts.Overview;
using Ecosys.Platform.Contracts.Settings;
using Ecosys.Platform.Contracts.Tenants;
using Ecosys.Platform.Entities;
using Ecosys.Platform.Enums;
using Ecosys.ServiceOps.Entities;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

internal sealed class TenantManagementService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext,
    IAuditService auditService) : ITenantManagementService
{
    public async Task<IReadOnlyCollection<TenantResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        AccessGuard.EnsureSuperAdmin(tenantContext);

        return await dbContext.Tenants
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new TenantResponse
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Plan = x.Plan,
                SubscriptionStatus = x.SubscriptionStatus,
                IsActive = x.IsActive,
                CreatedUtc = x.CreatedUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        AccessGuard.EnsureSuperAdmin(tenantContext);

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            throw new BusinessRuleException("Tenant name and code are required.");
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await dbContext.Tenants.AnyAsync(x => x.Code == code, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException($"Tenant code '{code}' already exists.");
        }

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Code = code,
            Plan = request.Plan.Trim(),
            SubscriptionStatus = SubscriptionStatus.Active,
            IsActive = true
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.TenantSettings.Add(new TenantSetting
        {
            TenantId = tenant.Id,
            CompanyName = tenant.Name
        });

        dbContext.EmailSettings.Add(new EmailSetting
        {
            TenantId = tenant.Id,
            Host = "localhost",
            SenderName = tenant.Name,
            SenderAddress = $"noreply@{tenant.Code.ToLowerInvariant()}.local"
        });

        dbContext.NumberSequences.AddRange(
            new NumberSequence { TenantId = tenant.Id, EntityType = "WorkOrder", Prefix = "WO", Padding = 6 },
            new NumberSequence { TenantId = tenant.Id, EntityType = "MaterialRequest", Prefix = "MR", Padding = 6 },
            new NumberSequence { TenantId = tenant.Id, EntityType = "Report", Prefix = "REP", Padding = 6 });

        dbContext.WorkOrderTypes.AddRange(
            CreateWorkOrderType(tenant.Id, "PM"),
            CreateWorkOrderType(tenant.Id, "Corrective"),
            CreateWorkOrderType(tenant.Id, "Installation"),
            CreateWorkOrderType(tenant.Id, "Inspection"),
            CreateWorkOrderType(tenant.Id, "ServiceRequest"));

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.Id,
            tenantContext.UserId,
            "Platform",
            "CreateTenant",
            nameof(Tenant),
            tenant.Id,
            $"Tenant {tenant.Code} created.",
            cancellationToken);

        return new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Code = tenant.Code,
            Plan = tenant.Plan,
            SubscriptionStatus = tenant.SubscriptionStatus,
            IsActive = tenant.IsActive,
            CreatedUtc = tenant.CreatedUtc
        };
    }

    public Task<TenantResponse> ActivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetTenantStateAsync(id, true, SubscriptionStatus.Active, cancellationToken);

    public Task<TenantResponse> SuspendAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetTenantStateAsync(id, false, SubscriptionStatus.Suspended, cancellationToken);

    private async Task<TenantResponse> SetTenantStateAsync(Guid id, bool isActive, SubscriptionStatus subscriptionStatus, CancellationToken cancellationToken)
    {
        AccessGuard.EnsureSuperAdmin(tenantContext);

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        tenant.IsActive = isActive;
        tenant.SubscriptionStatus = subscriptionStatus;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.Id,
            tenantContext.UserId,
            "Platform",
            isActive ? "ActivateTenant" : "SuspendTenant",
            nameof(Tenant),
            tenant.Id,
            $"Tenant {tenant.Code} {(isActive ? "activated" : "suspended")}.",
            cancellationToken);

        return new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Code = tenant.Code,
            Plan = tenant.Plan,
            SubscriptionStatus = tenant.SubscriptionStatus,
            IsActive = tenant.IsActive,
            CreatedUtc = tenant.CreatedUtc
        };
    }

    private static WorkOrderType CreateWorkOrderType(Guid tenantId, string code) =>
        new()
        {
            TenantId = tenantId,
            Code = code.ToUpperInvariant(),
            Name = code
        };
}

internal sealed class PlatformOverviewService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext) : IPlatformOverviewService
{
    public async Task<PlatformOverviewResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        AccessGuard.EnsureSuperAdmin(tenantContext);

        return new PlatformOverviewResponse
        {
            TotalTenants = await dbContext.Tenants.CountAsync(cancellationToken),
            ActiveTenants = await dbContext.Tenants.CountAsync(x => x.IsActive, cancellationToken),
            TotalUsers = await dbContext.Users.IgnoreQueryFilters().CountAsync(x => x.TenantId != PlatformConstants.RootTenantId, cancellationToken),
            TotalWorkOrdersCount = await dbContext.WorkOrders.IgnoreQueryFilters().CountAsync(cancellationToken),
            SystemHealth = await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthStatus.Healthy.ToString()
                : HealthStatus.Unhealthy.ToString()
        };
    }
}

internal sealed class TenantSettingsService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext,
    IAuditService auditService) : ITenantSettingsService
{
    public async Task<TenantSettingsResponse> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        AccessGuard.EnsureTenantAccess(tenantContext, tenantId);

        var settings = await dbContext.TenantSettings
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings were not found.");

        return Map(settings);
    }

    public async Task<TenantSettingsResponse> UpsertAsync(Guid tenantId, TenantSettingsRequest request, CancellationToken cancellationToken = default)
    {
        AccessGuard.EnsureTenantAccess(tenantContext, tenantId);

        var settings = await dbContext.TenantSettings
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (settings is null)
        {
            settings = new TenantSetting { TenantId = tenantId };
            dbContext.TenantSettings.Add(settings);
        }

        settings.CompanyName = request.CompanyName.Trim();
        settings.LogoUrl = AccessGuard.Normalize(request.LogoUrl);
        settings.PrimaryColor = request.PrimaryColor.Trim();
        settings.SecondaryColor = request.SecondaryColor.Trim();
        settings.AccentColor = request.AccentColor.Trim();
        settings.EmailSenderName = request.EmailSenderName.Trim();
        settings.EmailSenderAddress = request.EmailSenderAddress.Trim();
        settings.ShowPoweredBy = request.ShowPoweredBy;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenantId,
            tenantContext.UserId,
            "Settings",
            "UpdateTenantSettings",
            nameof(TenantSetting),
            settings.Id,
            "Tenant settings updated.",
            cancellationToken);

        return Map(settings);
    }

    private static TenantSettingsResponse Map(TenantSetting settings) =>
        new()
        {
            TenantId = settings.TenantId,
            CompanyName = settings.CompanyName,
            LogoUrl = settings.LogoUrl,
            PrimaryColor = settings.PrimaryColor,
            SecondaryColor = settings.SecondaryColor,
            AccentColor = settings.AccentColor,
            EmailSenderName = settings.EmailSenderName,
            EmailSenderAddress = settings.EmailSenderAddress,
            ShowPoweredBy = settings.ShowPoweredBy
        };
}

internal sealed class NumberSequenceService(EcosysDbContext dbContext) : INumberSequenceService
{
    public async Task<string> GenerateAsync(Guid tenantId, string entityType, CancellationToken cancellationToken = default)
    {
        var sequence = await dbContext.NumberSequences
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EntityType == entityType, cancellationToken)
            ?? throw new NotFoundException($"Number sequence for '{entityType}' was not found.");

        ResetSequenceIfNeeded(sequence);
        sequence.CurrentNumber += 1;
        await dbContext.SaveChangesAsync(cancellationToken);

        return $"{sequence.Prefix}{sequence.CurrentNumber.ToString().PadLeft(sequence.Padding, '0')}";
    }

    private static void ResetSequenceIfNeeded(NumberSequence sequence)
    {
        var now = DateTime.UtcNow;
        var lastReset = sequence.LastResetUtc ?? DateTime.MinValue;
        var needsReset = sequence.ResetFrequency switch
        {
            NumberResetFrequency.Daily => lastReset.Date < now.Date,
            NumberResetFrequency.Monthly => lastReset.Year != now.Year || lastReset.Month != now.Month,
            NumberResetFrequency.Yearly => lastReset.Year != now.Year,
            _ => false
        };

        if (needsReset)
        {
            sequence.CurrentNumber = 0;
            sequence.LastResetUtc = now;
        }
    }
}

internal sealed class NotificationService(
    EcosysDbContext dbContext,
    IEmailSender emailSender,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task QueueAsync(Guid tenantId, Guid? userId, string subject, string message, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Subject = subject,
            Message = message
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!userId.HasValue)
        {
            return;
        }

        var user = await dbContext.Users.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);
        var settings = await dbContext.EmailSettings.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (user is null || settings is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        try
        {
            var delivery = new EmailDeliverySettings(
                EmailDeliveryMode.Smtp,
                settings.Host,
                settings.Port,
                settings.Username,
                settings.Password,
                settings.SenderName,
                settings.SenderAddress,
                null,
                settings.UseSsl,
                settings.UseSsl ? EmailSecureMode.StartTls : EmailSecureMode.None,
                null,
                null,
                null,
                30,
                0);

            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    subject,
                    message,
                    settings.SenderName,
                    settings.SenderAddress),
                delivery,
                cancellationToken);
            notification.IsSent = true;
            notification.SentUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send notification email for tenant {TenantId}.", tenantId);
        }
    }
}

internal sealed class AuditService(EcosysDbContext dbContext) : IAuditService
{
    public async Task WriteAsync(Guid tenantId, Guid? userId, string category, string action, string entityType, Guid? entityId, string? details, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            Category = category,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

internal static class AccessGuard
{
    public static void EnsureSuperAdmin(ITenantContext tenantContext)
    {
        if (!tenantContext.IsSuperAdmin)
        {
            throw new ForbiddenException("SuperAdmin access is required.");
        }
    }

    public static void EnsureTenantAccess(ITenantContext tenantContext, Guid tenantId)
    {
        if (tenantContext.IsSuperAdmin)
        {
            return;
        }

        if (tenantContext.TenantId != tenantId)
        {
            throw new ForbiddenException("Access to the requested tenant is forbidden.");
        }
    }

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
