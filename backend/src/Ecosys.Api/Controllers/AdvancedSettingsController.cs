using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class AdvancedSettingsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    ILicenseGuardService licenseGuardService,
    IPdfRenderer pdfRenderer,
    IWebHostEnvironment? environment = null) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("branding")]
    public async Task<ActionResult<BrandingSettingsResponse>> GetBranding(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        return Ok(MapBrandingResponse(tenant));
    }

    [HttpPut("branding")]
    public async Task<ActionResult<BrandingSettingsResponse>> UpdateBranding([FromBody] BrandingSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        tenant.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();

        if (!string.IsNullOrWhiteSpace(request.PrimaryColor) && IsValidHexColor(request.PrimaryColor))
            tenant.PrimaryColor = request.PrimaryColor.Trim();

        if (!string.IsNullOrWhiteSpace(request.SecondaryColor) && IsValidHexColor(request.SecondaryColor))
            tenant.SecondaryColor = request.SecondaryColor.Trim();

        tenant.ShowPoweredByEcosys = request.ShowPoweredByEcosys;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapBrandingResponse(tenant));
    }

    [HttpGet("documents")]
    public async Task<ActionResult<DocumentsSettingsResponse>> GetDocuments(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        var template = LoadReportTemplate(tenant);
        return Ok(new DocumentsSettingsResponse(
            MapTenantProfileResponse(tenant),
            MapBrandingResponse(tenant),
            template));
    }

    [HttpPut("documents")]
    public async Task<ActionResult<DocumentsSettingsResponse>> UpdateDocuments([FromBody] DocumentsSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        if (string.IsNullOrWhiteSpace(request.Profile.CompanyName) ||
            string.IsNullOrWhiteSpace(request.Profile.Email) ||
            string.IsNullOrWhiteSpace(request.Profile.Country))
        {
            throw new BusinessRuleException("Company name, email, and country are required.");
        }

        ApplyTenantProfile(tenant, request.Profile);
        ApplyBranding(tenant, request.Branding);
        tenant.ReportTemplateConfigJson = JsonSerializer.Serialize(NormalizeReportTemplate(request.Template));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new DocumentsSettingsResponse(
            MapTenantProfileResponse(tenant),
            MapBrandingResponse(tenant),
            LoadReportTemplate(tenant)));
    }

    private static BrandingSettingsResponse MapBrandingResponse(Tenant tenant)
    {
        var primaryColor = IsValidHexColor(tenant.PrimaryColor) ? tenant.PrimaryColor : "#0F4C81";
        var secondaryColor = IsValidHexColor(tenant.SecondaryColor) ? tenant.SecondaryColor : "#F4B942";

        return new BrandingSettingsResponse(
            string.IsNullOrWhiteSpace(tenant.LogoUrl) ? null : tenant.LogoUrl.Trim(),
            primaryColor,
            secondaryColor,
            tenant.ShowPoweredByEcosys);
    }

    [HttpGet("assignment-groups")]
    public async Task<ActionResult<IReadOnlyCollection<AssignmentGroupResponse>>> GetAssignmentGroups(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var groups = await dbContext.AssignmentGroups
            .Include(x => x.Branch)
            .Include(x => x.Members)
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(groups.Select(x => new AssignmentGroupResponse(x.Id, x.BranchId, x.Branch?.Name, x.Name, x.Description, x.IsActive, x.Members.Where(member => member.IsActive).Select(member => member.TechnicianId).ToList())).ToList());
    }

    [HttpPost("assignment-groups")]
    public async Task<ActionResult<AssignmentGroupResponse>> CreateAssignmentGroup([FromBody] UpsertAssignmentGroupRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var group = new AssignmentGroup
        {
            TenantId = TenantId,
            BranchId = request.BranchId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };

        dbContext.AssignmentGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncMembersAsync(group, request.TechnicianIds, cancellationToken);
        await dbContext.Entry(group).Reference(x => x.Branch).LoadAsync(cancellationToken);

        return Ok(new AssignmentGroupResponse(group.Id, group.BranchId, group.Branch?.Name, group.Name, group.Description, group.IsActive, request.TechnicianIds));
    }

    [HttpPut("assignment-groups/{id:guid}")]
    public async Task<ActionResult<AssignmentGroupResponse>> UpdateAssignmentGroup(Guid id, [FromBody] UpsertAssignmentGroupRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var group = await dbContext.AssignmentGroups.Include(x => x.Branch).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Assignment group was not found.");

        group.BranchId = request.BranchId;
        group.Name = request.Name.Trim();
        group.Description = request.Description?.Trim();
        group.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncMembersAsync(group, request.TechnicianIds, cancellationToken);
        await dbContext.Entry(group).Reference(x => x.Branch).LoadAsync(cancellationToken);

        return Ok(new AssignmentGroupResponse(group.Id, group.BranchId, group.Branch?.Name, group.Name, group.Description, group.IsActive, request.TechnicianIds));
    }

    [HttpGet("operations-policy")]
    public async Task<ActionResult<TenantOperationsPolicy>> GetOperationsPolicy(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var policy = await dbContext.TenantOperationsPolicies.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? new TenantOperationsPolicy { TenantId = TenantId };
        return Ok(policy);
    }

    [HttpPut("operations-policy")]
    public async Task<ActionResult<TenantOperationsPolicy>> UpdateOperationsPolicy([FromBody] TenantOperationsPolicyRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var policy = await dbContext.TenantOperationsPolicies.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (policy is null)
        {
            policy = new TenantOperationsPolicy { TenantId = TenantId };
            dbContext.TenantOperationsPolicies.Add(policy);
        }

        policy.RequireTechnicianSignature = request.RequireTechnicianSignature;
        policy.RequireClientSignature = request.RequireClientSignature;
        policy.AllowRemoteCompletion = request.AllowRemoteCompletion;
        policy.AutoSendServiceReportOnApproval = request.AutoSendServiceReportOnApproval;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(policy);
    }

    [HttpGet("security")]
    public async Task<ActionResult<TenantSecurityPolicy>> GetSecurity(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var policy = await dbContext.TenantSecurityPolicies.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? new TenantSecurityPolicy { TenantId = TenantId };
        return Ok(policy);
    }

    [HttpPut("security")]
    public async Task<ActionResult<TenantSecurityPolicy>> UpdateSecurity([FromBody] TenantSecurityPolicyRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var policy = await dbContext.TenantSecurityPolicies.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (policy is null)
        {
            policy = new TenantSecurityPolicy { TenantId = TenantId };
            dbContext.TenantSecurityPolicies.Add(policy);
        }

        policy.MinPasswordLength = request.MinPasswordLength;
        policy.RequireUppercase = request.RequireUppercase;
        policy.RequireLowercase = request.RequireLowercase;
        policy.RequireDigit = request.RequireDigit;
        policy.RequireSpecialCharacter = request.RequireSpecialCharacter;
        policy.PasswordRotationDays = request.PasswordRotationDays;
        policy.SessionTimeoutMinutes = request.SessionTimeoutMinutes;
        policy.RequireMfa = request.RequireMfa;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(policy);
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<NotificationSetting>> GetNotifications(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await dbContext.NotificationSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? new NotificationSetting { TenantId = TenantId };
        return Ok(settings);
    }

    [HttpPut("notifications")]
    public async Task<ActionResult<NotificationSetting>> UpdateNotifications([FromBody] NotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await dbContext.NotificationSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (settings is null)
        {
            settings = new NotificationSetting { TenantId = TenantId };
            dbContext.NotificationSettings.Add(settings);
        }

        settings.EmailAlertsEnabled = request.EmailAlertsEnabled;
        settings.SmsAlertsEnabled = request.SmsAlertsEnabled;
        settings.WorkOrderAssignmentAlerts = request.WorkOrderAssignmentAlerts;
        settings.LicenseExpiryAlerts = request.LicenseExpiryAlerts;
        settings.DailyDigestEnabled = request.DailyDigestEnabled;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpGet("monitoring")]
    public async Task<ActionResult<MonitoringSetting>> GetMonitoring(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await dbContext.MonitoringSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken)
            ?? new MonitoringSetting { TenantId = TenantId };
        return Ok(settings);
    }

    [HttpPut("monitoring")]
    public async Task<ActionResult<MonitoringSetting>> UpdateMonitoring([FromBody] MonitoringSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var settings = await dbContext.MonitoringSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (settings is null)
        {
            settings = new MonitoringSetting { TenantId = TenantId };
            dbContext.MonitoringSettings.Add(settings);
        }

        settings.ProviderName = request.ProviderName.Trim();
        settings.EndpointLabel = request.EndpointLabel?.Trim();
        settings.WebhookSecret = request.WebhookSecret?.Trim();
        settings.DefaultBranchId = request.DefaultBranchId;
        settings.DefaultPriority = request.DefaultPriority;
        settings.AutoCreateWorkOrders = request.AutoCreateWorkOrders;
        settings.IsEnabled = request.IsEnabled;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    // ── Report Template ───────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    [HttpGet("report-template")]
    public async Task<ActionResult<ReportTemplateConfig>> GetReportTemplate(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        if (string.IsNullOrWhiteSpace(tenant.ReportTemplateConfigJson))
            return Ok(new ReportTemplateConfig());

        var config = JsonSerializer.Deserialize<ReportTemplateConfig>(tenant.ReportTemplateConfigJson, _jsonOpts)
                     ?? new ReportTemplateConfig();
        return Ok(config);
    }

    [HttpPut("report-template")]
    public async Task<ActionResult<ReportTemplateConfig>> UpdateReportTemplate(
        [FromBody] ReportTemplateConfig request,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        // Normalise section order so it's always 0-based and gap-free
        for (var i = 0; i < request.Sections.Count; i++)
            request.Sections[i].Order = i;

        var normalized = NormalizeReportTemplate(request);
        tenant.ReportTemplateConfigJson = JsonSerializer.Serialize(normalized);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(normalized);
    }

    // ── Generic per-module settings (key-value JSON) ────────────────────────────

    private static readonly HashSet<string> ModuleSettingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "executive-dashboard", "inventory", "integrations",
    };

    [HttpGet("module/{moduleKey}")]
    public async Task<ActionResult<Dictionary<string, object?>>> GetModuleSettings(string moduleKey, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var key = ResolveModuleKey(moduleKey);

        var setting = await dbContext.TenantModuleSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ModuleKey == key, cancellationToken);

        if (setting is null || string.IsNullOrWhiteSpace(setting.ValueJson))
            return Ok(new Dictionary<string, object?>());

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(setting.ValueJson, _jsonOpts);
            return Ok(parsed ?? new Dictionary<string, object?>());
        }
        catch (JsonException)
        {
            return Ok(new Dictionary<string, object?>());
        }
    }

    [HttpPut("module/{moduleKey}")]
    public async Task<ActionResult<Dictionary<string, object?>>> SaveModuleSettings(
        string moduleKey, [FromBody] Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var key = ResolveModuleKey(moduleKey);

        var setting = await dbContext.TenantModuleSettings
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ModuleKey == key, cancellationToken);
        if (setting is null)
        {
            setting = new TenantModuleSetting { TenantId = TenantId, ModuleKey = key };
            dbContext.TenantModuleSettings.Add(setting);
        }

        setting.ValueJson = JsonSerializer.Serialize(values ?? new Dictionary<string, object?>());
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(values ?? new Dictionary<string, object?>());
    }

    private static string ResolveModuleKey(string moduleKey)
    {
        var key = ModuleSettingKeys.FirstOrDefault(k => string.Equals(k, moduleKey?.Trim(), StringComparison.OrdinalIgnoreCase));
        return key ?? throw new BusinessRuleException($"Unknown settings module '{moduleKey}'.");
    }

    // ── License ───────────────────────────────────────────────────────────────

    [HttpGet("license")]
    public async Task<ActionResult<TenantLicenseSnapshot>> GetLicense(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        return Ok(await licenseGuardService.GetSnapshotAsync(TenantId, cancellationToken));
    }

    private async Task<Tenant> GetTenantAsync(CancellationToken cancellationToken) =>
        await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
        ?? throw new NotFoundException("Tenant was not found.");

    private static TenantProfileResponse MapTenantProfileResponse(Tenant tenant) =>
        new(
            tenant.CompanyName,
            tenant.LegalName,
            tenant.TradingName,
            tenant.PostalAddress,
            tenant.PhysicalAddress,
            tenant.Website,
            tenant.VatNumber,
            tenant.RegistrationNumber,
            tenant.Email,
            tenant.Phone,
            tenant.Country,
            tenant.Industry,
            tenant.Address,
            tenant.LogoUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.ShowPoweredByEcosys);

    private static void ApplyTenantProfile(Tenant tenant, UpdateTenantProfileRequest request)
    {
        tenant.CompanyName = request.CompanyName.Trim();
        tenant.LegalName = request.LegalName is null ? tenant.LegalName : NormalizeOptional(request.LegalName);
        tenant.TradingName = request.TradingName is null ? tenant.TradingName : NormalizeOptional(request.TradingName);
        tenant.PostalAddress = request.PostalAddress is null ? tenant.PostalAddress : NormalizeOptional(request.PostalAddress);
        tenant.PhysicalAddress = request.PhysicalAddress is null ? tenant.PhysicalAddress : NormalizeOptional(request.PhysicalAddress);
        tenant.Website = request.Website is null ? tenant.Website : NormalizeOptional(request.Website);
        tenant.VatNumber = request.VatNumber is null ? tenant.VatNumber : NormalizeOptional(request.VatNumber);
        tenant.RegistrationNumber = request.RegistrationNumber is null ? tenant.RegistrationNumber : NormalizeOptional(request.RegistrationNumber);
        tenant.Email = request.Email.Trim().ToLowerInvariant();
        tenant.Phone = request.Phone?.Trim();
        tenant.Country = request.Country.Trim();
        tenant.Industry = request.Industry?.Trim();
        tenant.Address = request.Address is null ? tenant.Address : NormalizeOptional(request.Address);
        tenant.LogoUrl = request.LogoUrl?.Trim();
    }

    private static void ApplyBranding(Tenant tenant, BrandingSettingsRequest request)
    {
        tenant.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();

        if (!string.IsNullOrWhiteSpace(request.PrimaryColor) && IsValidHexColor(request.PrimaryColor))
            tenant.PrimaryColor = request.PrimaryColor.Trim();

        if (!string.IsNullOrWhiteSpace(request.SecondaryColor) && IsValidHexColor(request.SecondaryColor))
            tenant.SecondaryColor = request.SecondaryColor.Trim();

        tenant.ShowPoweredByEcosys = request.ShowPoweredByEcosys;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ReportTemplateConfig LoadReportTemplate(Tenant tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.ReportTemplateConfigJson))
            return new ReportTemplateConfig();

        return JsonSerializer.Deserialize<ReportTemplateConfig>(tenant.ReportTemplateConfigJson, _jsonOpts)
               ?? new ReportTemplateConfig();
    }

    private static ReportTemplateConfig NormalizeReportTemplate(ReportTemplateConfig request)
    {
        request.Sections ??= new List<ReportSection>();
        request.PageDensity = string.IsNullOrWhiteSpace(request.PageDensity) ? "Comfortable" : request.PageDensity.Trim();
        request.LayoutStyle = string.IsNullOrWhiteSpace(request.LayoutStyle) ? "Modern" : request.LayoutStyle.Trim();
        request.HeaderStyle = string.IsNullOrWhiteSpace(request.HeaderStyle) ? "Logo left, company details right" : request.HeaderStyle.Trim();
        request.FooterStyle = string.IsNullOrWhiteSpace(request.FooterStyle) ? "Contact footer" : request.FooterStyle.Trim();
        request.FontFamily = string.IsNullOrWhiteSpace(request.FontFamily) ? "Helvetica" : request.FontFamily.Trim();
        request.QuoteTerms = string.IsNullOrWhiteSpace(request.QuoteTerms) ? null : request.QuoteTerms.Trim();
        request.InvoiceTerms = string.IsNullOrWhiteSpace(request.InvoiceTerms) ? null : request.InvoiceTerms.Trim();
        request.WarrantyNote = string.IsNullOrWhiteSpace(request.WarrantyNote) ? null : request.WarrantyNote.Trim();
        request.LatePaymentNote = string.IsNullOrWhiteSpace(request.LatePaymentNote) ? null : request.LatePaymentNote.Trim();
        request.ServiceReportDisclaimer = string.IsNullOrWhiteSpace(request.ServiceReportDisclaimer) ? null : request.ServiceReportDisclaimer.Trim();
        request.FooterLegalText = string.IsNullOrWhiteSpace(request.FooterLegalText) ? null : request.FooterLegalText.Trim();
        request.BankName = string.IsNullOrWhiteSpace(request.BankName) ? null : request.BankName.Trim();
        request.AccountName = string.IsNullOrWhiteSpace(request.AccountName) ? null : request.AccountName.Trim();
        request.AccountNumber = string.IsNullOrWhiteSpace(request.AccountNumber) ? null : request.AccountNumber.Trim();
        request.Branch = string.IsNullOrWhiteSpace(request.Branch) ? null : request.Branch.Trim();
        request.Swift = string.IsNullOrWhiteSpace(request.Swift) ? null : request.Swift.Trim();
        request.Paybill = string.IsNullOrWhiteSpace(request.Paybill) ? null : request.Paybill.Trim();
        request.PaymentInstructions = string.IsNullOrWhiteSpace(request.PaymentInstructions) ? null : request.PaymentInstructions.Trim();
        request.DefaultCurrency = string.IsNullOrWhiteSpace(request.DefaultCurrency) ? "KES" : request.DefaultCurrency.Trim().ToUpperInvariant();
        request.ShowStampArea = request.ShowStampArea;

        // ── Design-system fields ────────────────────────────────────────────
        request.LogoWidth = Math.Clamp(request.LogoWidth, 24, 160);
        request.CompanyNameFontSize = Math.Clamp(request.CompanyNameFontSize, 10, 32);
        request.HeaderBackgroundColor = IsValidHexColor(request.HeaderBackgroundColor) ? request.HeaderBackgroundColor!.Trim() : null;
        request.BodyFontSize = Math.Clamp(request.BodyFontSize, 7, 14);
        request.LineSpacing = Math.Clamp(request.LineSpacing, 1.0, 2.0);
        request.HeaderLayout = NormalizeOption(request.HeaderLayout, ["Minimal", "Standard", "Corporate", "Custom"], "Standard");
        request.IntroText = NormalizeOptional(request.IntroText);
        request.WatermarkText = NormalizeOptional(request.WatermarkText);
        request.WatermarkPosition = NormalizeOption(request.WatermarkPosition, ["Diagonal", "Top", "Bottom"], "Diagonal");
        request.WatermarkOpacity = Math.Clamp(request.WatermarkOpacity, 5, 60);
        request.WatermarkColor = IsValidHexColor(request.WatermarkColor) ? request.WatermarkColor!.Trim() : null;
        request.PageNumberPosition = NormalizeOption(request.PageNumberPosition, ["Left", "Center", "Right"], "Right");
        request.DateFormat = string.IsNullOrWhiteSpace(request.DateFormat) ? "dd MMM yyyy" : request.DateFormat.Trim();
        request.CurrencyDisplay = NormalizeOption(request.CurrencyDisplay, ["Code", "Symbol"], "Code");
        request.SignatureFields = (request.SignatureFields ?? [])
            .Select(f => f?.Trim() ?? string.Empty)
            .Where(f => f.Length > 0)
            .Take(4)
            .ToList();
        if (request.SignatureFields.Count == 0)
            request.SignatureFields = ["Authorised Signature"];
        request.SignaturePosition = NormalizeOption(request.SignaturePosition, ["Left", "Center", "Right", "SpaceBetween"], "SpaceBetween");
        request.PhotoLayout = NormalizeOption(request.PhotoLayout, ["Inline", "Appendix"], "Inline");
        request.MaxPhotosPerPage = Math.Clamp(request.MaxPhotosPerPage, 1, 12);
        return request;
    }

    private static bool IsValidHexColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith('#') && (value.Length == 7 || value.Length == 4) &&
        value[1..].All(c => "0123456789abcdefABCDEF".Contains(c));

    private static string NormalizeOption(string? value, string[] allowed, string fallback) =>
        allowed.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? fallback;

    // ── Per-document-type design system ─────────────────────────────────────────

    private static readonly string[] DocumentTypeSlugs =
        ["invoice", "quotation", "receipt", "statement", "service-report", "purchase-order"];

    [HttpGet("document-designs")]
    public async Task<ActionResult<DocumentDesignsResponse>> GetDocumentDesigns(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var tenant = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        var shared = LoadReportTemplate(tenant);
        var overrides = PdfTemplateHelper.ParseDesigns(tenant.DocumentDesignsJson);

        var documents = DocumentTypeSlugs.ToDictionary(
            slug => slug,
            slug => new DocumentDesignEntry(
                overrides.ContainsKey(slug),
                overrides.TryGetValue(slug, out var cfg) && cfg is not null ? cfg : shared),
            StringComparer.OrdinalIgnoreCase);

        return Ok(new DocumentDesignsResponse(shared, documents));
    }

    [HttpPut("document-designs/{documentType}")]
    public async Task<ActionResult<DocumentDesignEntry>> UpdateDocumentDesign(
        string documentType,
        [FromBody] ReportTemplateConfig request,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var slug = ResolveSlug(documentType);

        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        for (var i = 0; i < request.Sections.Count; i++)
            request.Sections[i].Order = i;

        var normalized = NormalizeReportTemplate(request);
        var overrides = PdfTemplateHelper.ParseDesigns(tenant.DocumentDesignsJson);
        overrides[slug] = normalized;
        tenant.DocumentDesignsJson = JsonSerializer.Serialize(overrides);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new DocumentDesignEntry(true, normalized));
    }

    [HttpPost("document-designs/{documentType}/reset")]
    public async Task<ActionResult<DocumentDesignEntry>> ResetDocumentDesign(
        string documentType,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var slug = ResolveSlug(documentType);

        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        var overrides = PdfTemplateHelper.ParseDesigns(tenant.DocumentDesignsJson);
        if (overrides.Remove(slug))
        {
            tenant.DocumentDesignsJson = overrides.Count == 0 ? null : JsonSerializer.Serialize(overrides);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // After reset the type falls back to the shared base config.
        return Ok(new DocumentDesignEntry(false, LoadReportTemplate(tenant)));
    }

    [HttpGet("document-designs/{documentType}/sample-pdf")]
    public async Task<IActionResult> GetDocumentDesignSample(
        string documentType,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var slug = ResolveSlug(documentType);

        var tenant = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        var config = PdfTemplateHelper.ResolveForDocument(tenant.ReportTemplateConfigJson, tenant.DocumentDesignsJson, slug)
                     ?? new ReportTemplateConfig();

        var bytes = SampleDocumentBuilder.Render(slug, tenant, config, pdfRenderer,
            PdfTemplateHelper.CompanyDetails(tenant, environment));
        return File(bytes, "application/pdf", $"sample-{slug}.pdf");
    }

    private static string ResolveSlug(string documentType)
    {
        var slug = DocumentTypeSlugs.FirstOrDefault(s => string.Equals(s, documentType?.Trim(), StringComparison.OrdinalIgnoreCase));
        return slug ?? throw new BusinessRuleException($"Unknown document type '{documentType}'.");
    }

    private async Task SyncMembersAsync(AssignmentGroup group, IReadOnlyCollection<Guid> technicianIds, CancellationToken cancellationToken)
    {
        var members = await dbContext.AssignmentGroupMembers.Where(x => x.TenantId == TenantId && x.AssignmentGroupId == group.Id).ToListAsync(cancellationToken);
        dbContext.AssignmentGroupMembers.RemoveRange(members);
        dbContext.AssignmentGroupMembers.AddRange(technicianIds.Distinct().Select(id => new AssignmentGroupMember
        {
            TenantId = TenantId,
            AssignmentGroupId = group.Id,
            TechnicianId = id,
            IsActive = true,
            AddedAt = DateTime.UtcNow
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record BrandingSettingsRequest(string? LogoUrl, string? PrimaryColor, string? SecondaryColor, bool ShowPoweredByEcosys);
public sealed record BrandingSettingsResponse(string? LogoUrl, string PrimaryColor, string SecondaryColor, bool ShowPoweredByEcosys);
public sealed record DocumentsSettingsRequest(UpdateTenantProfileRequest Profile, BrandingSettingsRequest Branding, ReportTemplateConfig Template);
public sealed record DocumentsSettingsResponse(TenantProfileResponse Profile, BrandingSettingsResponse Branding, ReportTemplateConfig Template);
/// <summary>One document type's effective config plus whether it has an explicit override (vs. inheriting the shared base).</summary>
public sealed record DocumentDesignEntry(bool IsCustomised, ReportTemplateConfig Config);
public sealed record DocumentDesignsResponse(ReportTemplateConfig Shared, Dictionary<string, DocumentDesignEntry> Documents);
public sealed record UpsertAssignmentGroupRequest(Guid? BranchId, string Name, string? Description, bool IsActive, IReadOnlyCollection<Guid> TechnicianIds);
public sealed record AssignmentGroupResponse(Guid Id, Guid? BranchId, string? BranchName, string Name, string? Description, bool IsActive, IReadOnlyCollection<Guid> TechnicianIds);
public sealed record TenantSecurityPolicyRequest(int MinPasswordLength, bool RequireUppercase, bool RequireLowercase, bool RequireDigit, bool RequireSpecialCharacter, int PasswordRotationDays, int SessionTimeoutMinutes, bool RequireMfa);
public sealed record NotificationSettingsRequest(bool EmailAlertsEnabled, bool SmsAlertsEnabled, bool WorkOrderAssignmentAlerts, bool LicenseExpiryAlerts, bool DailyDigestEnabled);
public sealed record MonitoringSettingsRequest(string ProviderName, string? EndpointLabel, string? WebhookSecret, Guid? DefaultBranchId, string DefaultPriority, bool AutoCreateWorkOrders, bool IsEnabled);
public sealed record TenantOperationsPolicyRequest(bool RequireTechnicianSignature, bool RequireClientSignature, bool AllowRemoteCompletion, bool AutoSendServiceReportOnApproval = false);

