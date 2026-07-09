using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/contracts")]
public sealed class ContractsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IContractAccessService contractAccessService,
    IContractLifecycleService contractLifecycleService,
    IContractPricingService contractPricingService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ContractListItemResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] string? renewalRisk,
        [FromQuery] Guid? ownerUserId,
        [FromQuery] Guid? clientId,
        [FromQuery] ContractType? contractType,
        [FromQuery] int? expiringWithinDays,
        CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewContracts();
        var today = DateTime.UtcNow.Date;

        var contracts = await dbContext.Contracts
            .Include(x => x.Client)
            .Include(x => x.AccountManagerUser)
            .Where(x => x.TenantId == TenantId && !x.IsArchived)
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken);

        var filtered = contracts
            .Where(x => !clientId.HasValue || x.ClientId == clientId.Value)
            .Where(x => !ownerUserId.HasValue || x.AccountManagerUserId == ownerUserId.Value || x.RenewalOwnerUserId == ownerUserId.Value)
            .Where(x => !contractType.HasValue || x.ContractType == contractType.Value)
            .Where(x => string.IsNullOrWhiteSpace(status) || string.Equals(x.Status.ToString(), status, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(renewalRisk) || string.Equals(x.RenewalRisk.ToString(), renewalRisk, StringComparison.OrdinalIgnoreCase))
            .Where(x => !expiringWithinDays.HasValue || (x.EndDate.Date - today).Days <= expiringWithinDays.Value)
            .Select(x => MapListItem(x, contractAccessService.CanViewPricing()))
            .ToList();

        return Ok(filtered);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContractDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewContracts();
        var contract = await LoadContractAsync(id, cancellationToken);
        return Ok(await MapDetailAsync(contract, contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ContractDetailResponse>> Create([FromBody] UpsertContractRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageContracts();
        await EnsureReferencedEntitiesAsync(request, cancellationToken);

        var contract = new Contract
        {
            TenantId = TenantId,
            ContractNumber = string.IsNullOrWhiteSpace(request.ContractNumber) ? $"CNT-{DateTime.UtcNow:yyyyMMddHHmmss}" : request.ContractNumber.Trim(),
            ClientId = request.ClientId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IncludedServices = request.IncludedServices?.Trim(),
            ExcludedServices = request.ExcludedServices?.Trim(),
            Notes = request.Notes?.Trim(),
            RenewalOwnerUserId = request.RenewalOwnerUserId,
            AccountManagerUserId = request.AccountManagerUserId,
            InternalApproverUserId = request.InternalApproverUserId,
        };
        ApplyContractPolicy(contract, request);
        contract.RenewalRisk = contractLifecycleService.CalculateRenewalRisk(contract.EndDate);

        dbContext.Contracts.Add(contract);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SyncContractChildrenAsync(contract, request, cancellationToken);
        await AddDefaultReminderRulesIfNeededAsync(contract, cancellationToken);
        await dbContext.ContractLifecycleEvents.AddAsync(new ContractLifecycleEvent
        {
            TenantId = TenantId,
            ContractId = contract.Id,
            EventType = ContractLifecycleEventType.Created,
            NewStatus = contract.Status,
            Note = "Contract created."
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "contract.created", nameof(Contract), contract.Id.ToString(), $"Created contract '{contract.ContractNumber}'.", cancellationToken);
        var persisted = await LoadContractAsync(contract.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = contract.Id }, await MapDetailAsync(persisted, contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContractDetailResponse>> Update(Guid id, [FromBody] UpsertContractRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageContracts();
        await EnsureReferencedEntitiesAsync(request, cancellationToken);

        var contract = await dbContext.Contracts.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contract was not found.");

        contract.ContractNumber = request.ContractNumber.Trim();
        contract.ClientId = request.ClientId;
        contract.Name = request.Name.Trim();
        contract.Description = request.Description?.Trim();
        contract.IncludedServices = request.IncludedServices?.Trim();
        contract.ExcludedServices = request.ExcludedServices?.Trim();
        contract.Notes = request.Notes?.Trim();
        contract.RenewalOwnerUserId = request.RenewalOwnerUserId;
        contract.AccountManagerUserId = request.AccountManagerUserId;
        contract.InternalApproverUserId = request.InternalApproverUserId;
        ApplyContractPolicy(contract, request);
        contract.RenewalRisk = contractLifecycleService.CalculateRenewalRisk(contract.EndDate);

        await SyncContractChildrenAsync(contract, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "contract.updated", nameof(Contract), contract.Id.ToString(), $"Updated contract '{contract.ContractNumber}'.", cancellationToken);
        var persisted = await LoadContractAsync(contract.Id, cancellationToken);
        return Ok(await MapDetailAsync(persisted, contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageContracts();
        var contract = await dbContext.Contracts.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contract was not found.");
        contract.IsArchived = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/lifecycle")]
    public async Task<ActionResult<IReadOnlyCollection<ContractLifecycleEventResponse>>> GetLifecycle(Guid id, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewContracts();
        await EnsureContractExistsAsync(id, cancellationToken);
        var items = await contractLifecycleService.GetLifecycleAsync(TenantId, id, cancellationToken);
        return Ok(items.Select(MapLifecycleEvent).ToList());
    }

    [HttpPost("{id:guid}/submit-approval")]
    public async Task<ActionResult<ContractDetailResponse>> SubmitApproval(Guid id, [FromBody] ContractActionRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageLifecycle();
        await contractLifecycleService.SubmitForApprovalAsync(TenantId, id, UserId, request.Note, cancellationToken);
        return Ok(await MapDetailAsync(await LoadContractAsync(id, cancellationToken), contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ContractDetailResponse>> Approve(Guid id, [FromBody] ContractActionRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageLifecycle();
        await contractLifecycleService.ApproveAsync(TenantId, id, UserId, request.Note, cancellationToken);
        return Ok(await MapDetailAsync(await LoadContractAsync(id, cancellationToken), contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<ContractDetailResponse>> Activate(Guid id, [FromBody] ContractActionRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageLifecycle();
        await contractLifecycleService.ActivateAsync(TenantId, id, UserId, request.Note, cancellationToken);
        return Ok(await MapDetailAsync(await LoadContractAsync(id, cancellationToken), contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPost("{id:guid}/start-renewal")]
    public async Task<ActionResult<ContractDetailResponse>> StartRenewal(Guid id, [FromBody] ContractActionRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageRenewals();
        await contractLifecycleService.StartRenewalAsync(TenantId, id, UserId, request.Note, cancellationToken);
        return Ok(await MapDetailAsync(await LoadContractAsync(id, cancellationToken), contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPost("{id:guid}/renew")]
    public async Task<ActionResult<ContractRenewalResultResponse>> Renew(Guid id, [FromBody] ContractRenewRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageRenewals();
        var result = await contractLifecycleService.RenewAsync(
            TenantId,
            id,
            UserId,
            new ContractRenewalOptions(
                request.StartDate,
                request.EndDate,
                request.DurationMonths,
                request.ActivateNewContract,
                request.SubmitForApproval,
                request.Name,
                request.ContractValue,
                request.Notes),
            cancellationToken);

        return Ok(new ContractRenewalResultResponse(
            result.OldContract.Id,
            result.NewContract.Id,
            result.OldContract.ContractNumber,
            result.NewContract.ContractNumber,
            result.OldContract.Status,
            result.NewContract.Status));
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<ContractDetailResponse>> Suspend(Guid id, [FromBody] ContractActionRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageLifecycle();
        await contractLifecycleService.SuspendAsync(TenantId, id, UserId, request.Reason ?? request.Note, cancellationToken);
        return Ok(await MapDetailAsync(await LoadContractAsync(id, cancellationToken), contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<ContractDetailResponse>> Resume(Guid id, [FromBody] ContractActionRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageLifecycle();
        await contractLifecycleService.ResumeAsync(TenantId, id, UserId, request.Note, cancellationToken);
        return Ok(await MapDetailAsync(await LoadContractAsync(id, cancellationToken), contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPost("{id:guid}/terminate")]
    public async Task<ActionResult<ContractDetailResponse>> Terminate(Guid id, [FromBody] TerminateContractRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageLifecycle();
        await contractLifecycleService.TerminateAsync(TenantId, id, UserId, request.TerminationDate, request.Reason, cancellationToken);
        return Ok(await MapDetailAsync(await LoadContractAsync(id, cancellationToken), contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ContractDetailResponse>> Cancel(Guid id, [FromBody] ContractActionRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageLifecycle();
        await contractLifecycleService.CancelAsync(TenantId, id, UserId, request.Reason ?? request.Note, cancellationToken);
        return Ok(await MapDetailAsync(await LoadContractAsync(id, cancellationToken), contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpPost("{id:guid}/recalculate-lifecycle")]
    public async Task<ActionResult<ContractDetailResponse>> RecalculateLifecycle(Guid id, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageLifecycle();
        await contractLifecycleService.RecalculateLifecycleAsync(TenantId, id, UserId, cancellationToken);
        return Ok(await MapDetailAsync(await LoadContractAsync(id, cancellationToken), contractAccessService.CanViewPricing(), cancellationToken));
    }

    [HttpGet("{id:guid}/reminder-rules")]
    public async Task<ActionResult<IReadOnlyCollection<ContractReminderRuleResponse>>> GetReminderRules(Guid id, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewContracts();
        await EnsureContractExistsAsync(id, cancellationToken);
        var items = await dbContext.ContractReminderRules
            .Where(x => x.TenantId == TenantId && x.ContractId == id)
            .OrderBy(x => x.DaysBeforeExpiry)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(MapReminderRule).ToList());
    }

    [HttpPost("{id:guid}/reminder-rules")]
    public async Task<ActionResult<ContractReminderRuleResponse>> CreateReminderRule(Guid id, [FromBody] UpsertContractReminderRuleRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageContracts();
        await EnsureContractExistsAsync(id, cancellationToken);
        var rule = new ContractReminderRule
        {
            TenantId = TenantId,
            ContractId = id,
            DaysBeforeExpiry = request.DaysBeforeExpiry,
            NotifyAccountManager = request.NotifyAccountManager,
            NotifyRenewalOwner = request.NotifyRenewalOwner,
            NotifySupervisor = request.NotifySupervisor,
            NotifyFinance = request.NotifyFinance,
            NotifyTenantAdmin = request.NotifyTenantAdmin,
            EmailEnabled = request.EmailEnabled,
            InAppEnabled = request.InAppEnabled,
            EscalationEnabled = request.EscalationEnabled,
            EscalationAfterDays = request.EscalationAfterDays,
            IsActive = request.IsActive
        };
        dbContext.ContractReminderRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapReminderRule(rule));
    }

    [HttpPut("{id:guid}/reminder-rules/{ruleId:guid}")]
    public async Task<ActionResult<ContractReminderRuleResponse>> UpdateReminderRule(Guid id, Guid ruleId, [FromBody] UpsertContractReminderRuleRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageContracts();
        var rule = await dbContext.ContractReminderRules.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ContractId == id && x.Id == ruleId, cancellationToken)
            ?? throw new NotFoundException("Reminder rule was not found.");
        rule.DaysBeforeExpiry = request.DaysBeforeExpiry;
        rule.NotifyAccountManager = request.NotifyAccountManager;
        rule.NotifyRenewalOwner = request.NotifyRenewalOwner;
        rule.NotifySupervisor = request.NotifySupervisor;
        rule.NotifyFinance = request.NotifyFinance;
        rule.NotifyTenantAdmin = request.NotifyTenantAdmin;
        rule.EmailEnabled = request.EmailEnabled;
        rule.InAppEnabled = request.InAppEnabled;
        rule.EscalationEnabled = request.EscalationEnabled;
        rule.EscalationAfterDays = request.EscalationAfterDays;
        rule.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapReminderRule(rule));
    }

    [HttpGet("{contractId:guid}/rate-cards")]
    public async Task<ActionResult<IReadOnlyCollection<ContractRateCardResponse>>> GetRateCards(Guid contractId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewPricing();
        await EnsureContractExistsAsync(contractId, cancellationToken);
        var items = await dbContext.ContractRateCards
            .Include(x => x.Rules)
            .Where(x => x.TenantId == TenantId && x.ContractId == contractId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(MapRateCard).ToList());
    }

    [HttpPost("{contractId:guid}/rate-cards")]
    public async Task<ActionResult<ContractRateCardResponse>> CreateRateCard(Guid contractId, [FromBody] UpsertContractRateCardRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageRates();
        await EnsureContractExistsAsync(contractId, cancellationToken);

        if (request.IsDefault)
        {
            var defaults = await dbContext.ContractRateCards.Where(x => x.TenantId == TenantId && x.ContractId == contractId && x.IsDefault).ToListAsync(cancellationToken);
            foreach (var current in defaults) current.IsDefault = false;
        }

        var card = new ContractRateCard
        {
            TenantId = TenantId,
            ContractId = contractId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Currency = request.Currency.Trim(),
            IsDefault = request.IsDefault,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            IsActive = request.IsActive
        };
        dbContext.ContractRateCards.Add(card);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapRateCard(card));
    }

    [HttpGet("{contractId:guid}/rate-cards/{rateCardId:guid}")]
    public async Task<ActionResult<ContractRateCardResponse>> GetRateCard(Guid contractId, Guid rateCardId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewPricing();
        var card = await dbContext.ContractRateCards
            .Include(x => x.Rules)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ContractId == contractId && x.Id == rateCardId, cancellationToken)
            ?? throw new NotFoundException("Rate card was not found.");
        return Ok(MapRateCard(card));
    }

    [HttpPut("{contractId:guid}/rate-cards/{rateCardId:guid}")]
    public async Task<ActionResult<ContractRateCardResponse>> UpdateRateCard(Guid contractId, Guid rateCardId, [FromBody] UpsertContractRateCardRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageRates();
        var card = await dbContext.ContractRateCards
            .Include(x => x.Rules)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ContractId == contractId && x.Id == rateCardId, cancellationToken)
            ?? throw new NotFoundException("Rate card was not found.");

        if (request.IsDefault)
        {
            var defaults = await dbContext.ContractRateCards.Where(x => x.TenantId == TenantId && x.ContractId == contractId && x.IsDefault && x.Id != rateCardId).ToListAsync(cancellationToken);
            foreach (var current in defaults) current.IsDefault = false;
        }

        card.Name = request.Name.Trim();
        card.Description = request.Description?.Trim();
        card.Currency = request.Currency.Trim();
        card.IsDefault = request.IsDefault;
        card.EffectiveFrom = request.EffectiveFrom;
        card.EffectiveTo = request.EffectiveTo;
        card.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapRateCard(card));
    }

    [HttpPost("{contractId:guid}/rate-cards/{rateCardId:guid}/rules")]
    public async Task<ActionResult<ContractRateRuleResponse>> CreateRateRule(Guid contractId, Guid rateCardId, [FromBody] UpsertContractRateRuleRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageRates();
        await EnsureRateCardExistsAsync(contractId, rateCardId, cancellationToken);
        var rule = new ContractRateRule
        {
            TenantId = TenantId,
            RateCardId = rateCardId,
            WorkOrderTypeId = request.WorkOrderTypeId,
            WorkOrderTypeName = request.WorkOrderTypeName?.Trim(),
            AssetCategoryId = request.AssetCategoryId,
            AssetCategoryName = request.AssetCategoryName?.Trim(),
            SiteId = request.SiteId,
            Priority = request.Priority?.Trim(),
            BillingModel = request.BillingModel,
            CalloutFee = request.CalloutFee,
            FixedVisitFee = request.FixedVisitFee,
            LabourHourlyRate = request.LabourHourlyRate,
            OvertimeHourlyRate = request.OvertimeHourlyRate,
            WeekendHourlyRate = request.WeekendHourlyRate,
            PublicHolidayHourlyRate = request.PublicHolidayHourlyRate,
            EmergencyMultiplier = request.EmergencyMultiplier,
            TravelCharge = request.TravelCharge,
            MaterialMarkupPercent = request.MaterialMarkupPercent,
            DiscountPercent = request.DiscountPercent,
            MinimumBillableHours = request.MinimumBillableHours,
            IncludedLabourHours = request.IncludedLabourHours,
            VatApplicable = request.VatApplicable,
            Notes = request.Notes?.Trim(),
            IsActive = request.IsActive
        };
        dbContext.ContractRateRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapRateRule(rule));
    }

    [HttpPut("{contractId:guid}/rate-cards/{rateCardId:guid}/rules/{ruleId:guid}")]
    public async Task<ActionResult<ContractRateRuleResponse>> UpdateRateRule(Guid contractId, Guid rateCardId, Guid ruleId, [FromBody] UpsertContractRateRuleRequest request, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageRates();
        var rule = await dbContext.ContractRateRules.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.RateCardId == rateCardId && x.Id == ruleId, cancellationToken)
            ?? throw new NotFoundException("Rate rule was not found.");

        rule.WorkOrderTypeId = request.WorkOrderTypeId;
        rule.WorkOrderTypeName = request.WorkOrderTypeName?.Trim();
        rule.AssetCategoryId = request.AssetCategoryId;
        rule.AssetCategoryName = request.AssetCategoryName?.Trim();
        rule.SiteId = request.SiteId;
        rule.Priority = request.Priority?.Trim();
        rule.BillingModel = request.BillingModel;
        rule.CalloutFee = request.CalloutFee;
        rule.FixedVisitFee = request.FixedVisitFee;
        rule.LabourHourlyRate = request.LabourHourlyRate;
        rule.OvertimeHourlyRate = request.OvertimeHourlyRate;
        rule.WeekendHourlyRate = request.WeekendHourlyRate;
        rule.PublicHolidayHourlyRate = request.PublicHolidayHourlyRate;
        rule.EmergencyMultiplier = request.EmergencyMultiplier;
        rule.TravelCharge = request.TravelCharge;
        rule.MaterialMarkupPercent = request.MaterialMarkupPercent;
        rule.DiscountPercent = request.DiscountPercent;
        rule.MinimumBillableHours = request.MinimumBillableHours;
        rule.IncludedLabourHours = request.IncludedLabourHours;
        rule.VatApplicable = request.VatApplicable;
        rule.Notes = request.Notes?.Trim();
        rule.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapRateRule(rule));
    }

    [HttpDelete("{contractId:guid}/rate-cards/{rateCardId:guid}/rules/{ruleId:guid}")]
    public async Task<IActionResult> ArchiveRateRule(Guid contractId, Guid rateCardId, Guid ruleId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageRates();
        var rule = await dbContext.ContractRateRules.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.RateCardId == rateCardId && x.Id == ruleId, cancellationToken)
            ?? throw new NotFoundException("Rate rule was not found.");
        rule.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<Contract> LoadContractAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Contracts
            .Include(x => x.Client)
            .Include(x => x.RenewalOwnerUser)
            .Include(x => x.AccountManagerUser)
            .Include(x => x.InternalApproverUser)
            .Include(x => x.Sites).ThenInclude(x => x.Site)
            .Include(x => x.Assets).ThenInclude(x => x.Asset)
            .Include(x => x.AssetCategories).ThenInclude(x => x.AssetCategory)
            .Include(x => x.WorkOrderTypes)
            .Include(x => x.LifecycleEvents).ThenInclude(x => x.CreatedByUser)
            .Include(x => x.ReminderRules)
            .Include(x => x.RateCards).ThenInclude(x => x.Rules)
            .Include(x => x.PmEntitlements).ThenInclude(x => x.Asset)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && !x.IsArchived, cancellationToken)
        ?? throw new NotFoundException("Contract was not found.");

    private async Task EnsureReferencedEntitiesAsync(UpsertContractRequest request, CancellationToken cancellationToken)
    {
        var clientExists = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.Id == request.ClientId, cancellationToken);
        if (!clientExists) throw new BusinessRuleException("Client was not found.");

        if (request.SiteIds.Count > 0)
        {
            var count = await dbContext.Sites.CountAsync(x => x.TenantId == TenantId && x.ClientId == request.ClientId && request.SiteIds.Contains(x.Id), cancellationToken);
            if (count != request.SiteIds.Count) throw new BusinessRuleException("One or more covered sites are invalid for this client.");
        }

        if (request.AssetIds.Count > 0)
        {
            var count = await dbContext.Assets.CountAsync(x => x.TenantId == TenantId && x.ClientId == request.ClientId && request.AssetIds.Contains(x.Id), cancellationToken);
            if (count != request.AssetIds.Count) throw new BusinessRuleException("One or more covered assets are invalid for this client.");
        }

        var categoryIds = request.AssetCategories.Where(x => x.AssetCategoryId.HasValue).Select(x => x.AssetCategoryId!.Value).Distinct().ToList();
        if (categoryIds.Count > 0)
        {
            var count = await dbContext.AssetCategories.CountAsync(x => x.TenantId == TenantId && categoryIds.Contains(x.Id), cancellationToken);
            if (count != categoryIds.Count) throw new BusinessRuleException("One or more covered asset categories are invalid.");
        }
    }

    private void ApplyContractPolicy(Contract contract, UpsertContractRequest request)
    {
        contract.ContractType = request.ContractType;
        contract.Status = request.Status;
        contract.StartDate = request.StartDate.Date;
        contract.EndDate = request.EndDate.Date;
        contract.DurationMonths = request.DurationMonths;
        contract.BillingModel = request.BillingModel;
        contract.Currency = request.Currency.Trim();
        contract.ContractValue = request.ContractValue;
        contract.RenewalNoticeDays = request.RenewalNoticeDays;
        contract.RenewalBehavior = NormalizeRenewalBehavior(request.RenewalBehavior, request.AutoExpireEnabled);
        contract.AutoExpireEnabled = string.Equals(contract.RenewalBehavior, "AutoExpire", StringComparison.OrdinalIgnoreCase);
        contract.GracePeriodPolicy = NormalizeGracePeriodPolicy(
            request.GracePeriodPolicy,
            request.AllowPreferentialRatesDuringGracePeriod,
            request.AllowSlaDuringGracePeriod,
            request.GracePeriodAllowsSla);
        contract.GracePeriodDays = request.GracePeriodDays;
        contract.AllowPreferentialRatesDuringGracePeriod = contract.GracePeriodPolicy is "PreferentialRates" or "Both";
        contract.AllowSlaDuringGracePeriod = contract.GracePeriodPolicy is "Sla" or "Both";
        contract.GracePeriodAllowsSla = contract.AllowSlaDuringGracePeriod;
        contract.PauseSlaOnHold = request.PauseSlaOnHold;
        contract.EscalationEmail = request.EscalationEmail?.Trim();
        contract.WorkingHoursPolicy = request.WorkingHoursPolicy?.Trim();

        var slaMatrix = BuildSlaMatrix(request);
        contract.SlaMatrixJson = JsonSerializer.Serialize(slaMatrix, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var firstRow = slaMatrix.FirstOrDefault();
        contract.PriorityLevel = firstRow?.Priority?.Trim();
        contract.ResponseTimeHours = firstRow?.ResponseTimeHours;
        contract.ResolutionTimeHours = firstRow?.ResolutionTimeHours;
    }

    private static string NormalizeRenewalBehavior(string? renewalBehavior, bool autoExpireEnabled)
    {
        if (!string.IsNullOrWhiteSpace(renewalBehavior))
        {
            return renewalBehavior.Trim();
        }

        return autoExpireEnabled ? "AutoExpire" : "ManualRenewalRequired";
    }

    private static string NormalizeGracePeriodPolicy(
        string? gracePeriodPolicy,
        bool allowPreferentialRatesDuringGracePeriod,
        bool allowSlaDuringGracePeriod,
        bool gracePeriodAllowsSla)
    {
        if (!string.IsNullOrWhiteSpace(gracePeriodPolicy))
        {
            return gracePeriodPolicy.Trim();
        }

        if (allowPreferentialRatesDuringGracePeriod && (allowSlaDuringGracePeriod || gracePeriodAllowsSla))
        {
            return "Both";
        }

        if (allowPreferentialRatesDuringGracePeriod)
        {
            return "PreferentialRates";
        }

        if (allowSlaDuringGracePeriod || gracePeriodAllowsSla)
        {
            return "Sla";
        }

        return "None";
    }

    private static List<ContractSlaRowRequest> BuildSlaMatrix(UpsertContractRequest request)
    {
        var rows = request.SlaMatrix?
            .Select(row => new ContractSlaRowRequest(
                row.Priority?.Trim() ?? "Standard",
                row.ResponseTimeHours,
                row.ResolutionTimeHours))
            .Where(row => !string.IsNullOrWhiteSpace(row.Priority))
            .ToList() ?? [];

        if (rows.Count > 0)
        {
            return rows;
        }

        return [
            new ContractSlaRowRequest(
                string.IsNullOrWhiteSpace(request.PriorityLevel) ? "Standard" : request.PriorityLevel.Trim(),
                request.ResponseTimeHours,
                request.ResolutionTimeHours),
        ];
    }

    private async Task SyncContractChildrenAsync(Contract contract, UpsertContractRequest request, CancellationToken cancellationToken)
    {
        var sites = await dbContext.ContractSites.Where(x => x.TenantId == TenantId && x.ContractId == contract.Id).ToListAsync(cancellationToken);
        var assets = await dbContext.ContractAssets.Where(x => x.TenantId == TenantId && x.ContractId == contract.Id).ToListAsync(cancellationToken);
        var categories = await dbContext.ContractAssetCategories.Where(x => x.TenantId == TenantId && x.ContractId == contract.Id).ToListAsync(cancellationToken);
        var workOrderTypes = await dbContext.ContractWorkOrderTypes.Where(x => x.TenantId == TenantId && x.ContractId == contract.Id).ToListAsync(cancellationToken);
        var entitlements = await dbContext.ContractPmEntitlements.Where(x => x.TenantId == TenantId && x.ContractId == contract.Id).ToListAsync(cancellationToken);
        var assetCategoryIds = request.AssetCategories.Where(x => x.AssetCategoryId.HasValue).Select(x => x.AssetCategoryId!.Value).Distinct().ToList();
        var assetCategoryLookup = assetCategoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.AssetCategories
                .Where(x => x.TenantId == TenantId && assetCategoryIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        dbContext.ContractSites.RemoveRange(sites);
        dbContext.ContractAssets.RemoveRange(assets);
        dbContext.ContractAssetCategories.RemoveRange(categories);
        dbContext.ContractWorkOrderTypes.RemoveRange(workOrderTypes);
        dbContext.ContractPmEntitlements.RemoveRange(entitlements);

        dbContext.ContractSites.AddRange(request.SiteIds.Select(siteId => new ContractSite
        {
            TenantId = TenantId,
            ContractId = contract.Id,
            SiteId = siteId
        }));
        dbContext.ContractAssets.AddRange(request.AssetIds.Select(assetId => new ContractAsset
        {
            TenantId = TenantId,
            ContractId = contract.Id,
            AssetId = assetId
        }));
        dbContext.ContractAssetCategories.AddRange(request.AssetCategories.Select(item => new ContractAssetCategory
        {
            TenantId = TenantId,
            ContractId = contract.Id,
            AssetCategoryId = item.AssetCategoryId,
            AssetCategoryName = item.AssetCategoryId.HasValue
                ? assetCategoryLookup.GetValueOrDefault(item.AssetCategoryId.Value, item.AssetCategoryName?.Trim() ?? string.Empty)
                : item.AssetCategoryName?.Trim()
        }));
        dbContext.ContractWorkOrderTypes.AddRange(request.WorkOrderTypes.Select(item => new ContractWorkOrderType
        {
            TenantId = TenantId,
            ContractId = contract.Id,
            WorkOrderTypeId = item.WorkOrderTypeId,
            WorkOrderTypeName = item.WorkOrderTypeName?.Trim()
        }));
        dbContext.ContractPmEntitlements.AddRange(request.PmEntitlements.Select(item => new ContractPmEntitlement
        {
            TenantId = TenantId,
            ContractId = contract.Id,
            AssetId = item.AssetId,
            AssetCategory = item.AssetCategory?.Trim(),
            WorkOrderType = item.WorkOrderType.Trim(),
            VisitsPerPeriod = item.VisitsPerPeriod,
            PeriodType = item.PeriodType,
            IncludedVisits = item.IncludedVisits,
            UsedVisits = item.UsedVisits,
            RemainingVisits = Math.Max(0, item.IncludedVisits - item.UsedVisits),
            EffectiveFrom = item.EffectiveFrom.Date,
            EffectiveTo = item.EffectiveTo.Date,
            IsActive = item.IsActive,
            Notes = item.Notes?.Trim()
        }));
    }

    private async Task AddDefaultReminderRulesIfNeededAsync(Contract contract, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ContractReminderRules.AnyAsync(x => x.TenantId == TenantId && x.ContractId == contract.Id, cancellationToken);
        if (existing)
        {
            return;
        }

        foreach (var days in new[] { 90, 60, 30, 14, 7, 0 })
        {
            dbContext.ContractReminderRules.Add(new ContractReminderRule
            {
                TenantId = TenantId,
                ContractId = contract.Id,
                DaysBeforeExpiry = days,
                NotifyAccountManager = true,
                NotifyRenewalOwner = true,
                NotifyTenantAdmin = true,
                EmailEnabled = true,
                InAppEnabled = false,
                IsActive = true
            });
        }
    }

    private async Task EnsureContractExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Contracts.AnyAsync(x => x.TenantId == TenantId && x.Id == id && !x.IsArchived, cancellationToken);
        if (!exists) throw new NotFoundException("Contract was not found.");
    }

    private async Task EnsureRateCardExistsAsync(Guid contractId, Guid rateCardId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.ContractRateCards.AnyAsync(x => x.TenantId == TenantId && x.ContractId == contractId && x.Id == rateCardId, cancellationToken);
        if (!exists) throw new NotFoundException("Rate card was not found.");
    }

    private async Task<ContractDetailResponse> MapDetailAsync(Contract contract, bool includePricing, CancellationToken cancellationToken)
    {
        var workOrders = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.ContractId == contract.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new ContractWorkOrderSummaryResponse(x.Id, x.WorkOrderNumber, x.Title, x.Status, x.CoverageStatus, x.BillingModel, x.EstimatedCharge, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var renewals = await dbContext.ContractRenewals
            .Where(x => x.TenantId == TenantId && (x.OriginalContractId == contract.Id || x.RenewedContractId == contract.Id))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ContractRenewalSummaryResponse(x.Id, x.OriginalContractId, x.RenewedContractId, x.RenewalStatus, x.OwnerUserId, x.RenewalStartedAt, x.RenewalCompletedAt, x.Notes))
            .ToListAsync(cancellationToken);

        return new ContractDetailResponse(
            contract.Id,
            contract.ContractNumber,
            contract.ClientId,
            contract.Client?.ClientName,
            contract.Name,
            contract.ContractType,
            contract.Status,
            contract.StartDate,
            contract.EndDate,
            contract.DurationMonths,
            contract.BillingModel,
            contract.Currency,
            includePricing ? contract.ContractValue : null,
            contract.RenewalNoticeDays,
            contract.RenewalBehavior,
            contract.Description,
            contract.IncludedServices,
            contract.ExcludedServices,
            contract.Notes,
            contract.ActivatedAt,
            contract.SubmittedForApprovalAt,
            contract.ApprovedAt,
            contract.ExpiryCheckedAt,
            contract.RenewalOwnerUserId,
            contract.RenewalOwnerUser?.FullName,
            contract.AccountManagerUserId,
            contract.AccountManagerUser?.FullName,
            contract.InternalApproverUserId,
            contract.InternalApproverUser?.FullName,
            contract.RenewalStartedAt,
            contract.RenewedFromContractId,
            contract.RenewedToContractId,
            contract.TerminationDate,
            contract.TerminationReason,
            contract.SuspensionReason,
            contract.CancellationReason,
            contract.LastReminderSentAt,
            contract.NextReminderAt,
            contract.AutoExpireEnabled,
            contract.RenewalRisk,
            contract.GracePeriodDays,
            contract.AllowPreferentialRatesDuringGracePeriod,
            contract.AllowSlaDuringGracePeriod,
            contract.GracePeriodPolicy,
            contract.ResponseTimeHours,
            contract.ResolutionTimeHours,
            contract.WorkingHoursPolicy,
            contract.PauseSlaOnHold,
            contract.EscalationEmail,
            contract.PriorityLevel,
            contract.GracePeriodAllowsSla,
            MapSlaMatrix(contract),
            contract.Sites.Select(x => new ContractSiteResponse(x.SiteId, x.Site?.SiteCode, x.Site?.SiteName)).ToList(),
            contract.Assets.Select(x => new ContractAssetResponse(x.AssetId, x.Asset?.AssetCode, x.Asset?.AssetName)).ToList(),
            contract.AssetCategories.Select(x => new ContractAssetCategoryResponse(x.AssetCategoryId, x.AssetCategory?.Name ?? x.AssetCategoryName)).ToList(),
            contract.WorkOrderTypes.Select(x => new ContractWorkOrderTypeResponse(x.WorkOrderTypeId, x.WorkOrderTypeName)).ToList(),
            contract.PmEntitlements.Select(MapPmEntitlement).ToList(),
            contract.ReminderRules.OrderBy(x => x.DaysBeforeExpiry).Select(MapReminderRule).ToList(),
            includePricing ? contract.RateCards.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).Select(MapRateCard).ToList() : [],
            contract.LifecycleEvents.OrderBy(x => x.CreatedAt).Select(MapLifecycleEvent).ToList(),
            renewals,
            workOrders);
    }

    private static ContractListItemResponse MapListItem(Contract contract, bool includePricing) =>
        new(
            contract.Id,
            contract.ContractNumber,
            contract.ClientId,
            contract.Client?.ClientName,
            contract.Name,
            contract.ContractType,
            contract.Status,
            contract.StartDate,
            contract.EndDate,
            (contract.EndDate.Date - DateTime.UtcNow.Date).Days,
            contract.RenewalRisk,
            contract.BillingModel,
            contract.AccountManagerUserId ?? contract.RenewalOwnerUserId,
            contract.AccountManagerUser?.FullName ?? contract.RenewalOwnerUser?.FullName,
            includePricing ? contract.ContractValue : null);

    private static ContractReminderRuleResponse MapReminderRule(ContractReminderRule item) =>
        new(item.Id, item.ContractId, item.DaysBeforeExpiry, item.NotifyAccountManager, item.NotifyRenewalOwner, item.NotifySupervisor, item.NotifyFinance, item.NotifyTenantAdmin, item.EmailEnabled, item.InAppEnabled, item.EscalationEnabled, item.EscalationAfterDays, item.IsActive);

    private static ContractRateCardResponse MapRateCard(ContractRateCard item) =>
        new(item.Id, item.ContractId, item.Name, item.Description, item.Currency, item.IsDefault, item.EffectiveFrom, item.EffectiveTo, item.IsActive, item.Rules.OrderByDescending(x => x.SiteId.HasValue).ThenBy(x => x.CreatedAt).Select(MapRateRule).ToList());

    private static ContractRateRuleResponse MapRateRule(ContractRateRule item) =>
        new(item.Id, item.RateCardId, item.WorkOrderTypeId, item.WorkOrderTypeName, item.AssetCategoryId, item.AssetCategoryName, item.SiteId, item.Priority, item.BillingModel, item.CalloutFee, item.FixedVisitFee, item.LabourHourlyRate, item.OvertimeHourlyRate, item.WeekendHourlyRate, item.PublicHolidayHourlyRate, item.EmergencyMultiplier, item.TravelCharge, item.MaterialMarkupPercent, item.DiscountPercent, item.MinimumBillableHours, item.IncludedLabourHours, item.VatApplicable, item.Notes, item.IsActive);

    private static ContractPmEntitlementResponse MapPmEntitlement(ContractPmEntitlement item) =>
        new(item.Id, item.AssetId, item.Asset?.AssetName, item.AssetCategory, item.WorkOrderType, item.VisitsPerPeriod, item.PeriodType, item.IncludedVisits, item.UsedVisits, item.RemainingVisits, item.EffectiveFrom, item.EffectiveTo, item.IsActive, item.Notes);

    private static IReadOnlyCollection<ContractSlaRowResponse> MapSlaMatrix(Contract contract)
    {
        if (string.IsNullOrWhiteSpace(contract.SlaMatrixJson))
        {
            var legacyRow = new ContractSlaRowResponse(contract.PriorityLevel ?? "Standard", contract.ResponseTimeHours, contract.ResolutionTimeHours);
            return [legacyRow];
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<ContractSlaRowResponse>>(contract.SlaMatrixJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return rows is { Count: > 0 } ? rows : [new ContractSlaRowResponse(contract.PriorityLevel ?? "Standard", contract.ResponseTimeHours, contract.ResolutionTimeHours)];
        }
        catch
        {
            return [new ContractSlaRowResponse(contract.PriorityLevel ?? "Standard", contract.ResponseTimeHours, contract.ResolutionTimeHours)];
        }
    }

    private static ContractLifecycleEventResponse MapLifecycleEvent(ContractLifecycleEvent item) =>
        new(item.Id, item.EventType, item.OldStatus, item.NewStatus, item.Note, item.Reason, item.CreatedByUserId, item.CreatedByUser?.FullName, item.CreatedAt);
}

[ApiController]
[Authorize]
public sealed class ContractReferenceController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IContractAccessService contractAccessService,
    IContractOperationalService contractOperationalService,
    IContractPricingService contractPricingService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("/api/clients/{clientId:guid}/contracts")]
    public async Task<ActionResult<IReadOnlyCollection<ContractListItemResponse>>> GetClientContracts(Guid clientId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewContracts();
        var items = await dbContext.Contracts.Include(x => x.Client).Include(x => x.AccountManagerUser)
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId && !x.IsArchived)
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => new ContractListItemResponse(x.Id, x.ContractNumber, x.ClientId, x.Client?.ClientName, x.Name, x.ContractType, x.Status, x.StartDate, x.EndDate, (x.EndDate.Date - DateTime.UtcNow.Date).Days, x.RenewalRisk, x.BillingModel, x.AccountManagerUserId ?? x.RenewalOwnerUserId, x.AccountManagerUser?.FullName ?? x.RenewalOwnerUser?.FullName, contractAccessService.CanViewPricing() ? x.ContractValue : null)).ToList());
    }

    [HttpGet("/api/sites/{siteId:guid}/contracts")]
    public async Task<ActionResult<IReadOnlyCollection<ContractListItemResponse>>> GetSiteContracts(Guid siteId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewContracts();
        var items = await dbContext.Contracts.Include(x => x.Client).Include(x => x.AccountManagerUser).Include(x => x.Sites)
            .Where(x => x.TenantId == TenantId && !x.IsArchived && x.Sites.Any(site => site.SiteId == siteId))
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => new ContractListItemResponse(x.Id, x.ContractNumber, x.ClientId, x.Client?.ClientName, x.Name, x.ContractType, x.Status, x.StartDate, x.EndDate, (x.EndDate.Date - DateTime.UtcNow.Date).Days, x.RenewalRisk, x.BillingModel, x.AccountManagerUserId ?? x.RenewalOwnerUserId, x.AccountManagerUser?.FullName ?? x.RenewalOwnerUser?.FullName, contractAccessService.CanViewPricing() ? x.ContractValue : null)).ToList());
    }

    [HttpGet("/api/assets/{assetId:guid}/contracts")]
    public async Task<ActionResult<IReadOnlyCollection<ContractListItemResponse>>> GetAssetContracts(Guid assetId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewContracts();
        var items = await dbContext.Contracts.Include(x => x.Client).Include(x => x.AccountManagerUser).Include(x => x.Assets)
            .Where(x => x.TenantId == TenantId && !x.IsArchived && x.Assets.Any(asset => asset.AssetId == assetId))
            .OrderBy(x => x.EndDate)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => new ContractListItemResponse(x.Id, x.ContractNumber, x.ClientId, x.Client?.ClientName, x.Name, x.ContractType, x.Status, x.StartDate, x.EndDate, (x.EndDate.Date - DateTime.UtcNow.Date).Days, x.RenewalRisk, x.BillingModel, x.AccountManagerUserId ?? x.RenewalOwnerUserId, x.AccountManagerUser?.FullName ?? x.RenewalOwnerUser?.FullName, contractAccessService.CanViewPricing() ? x.ContractValue : null)).ToList());
    }

    [HttpGet("/api/workorders/{workOrderId:guid}/pricing")]
    public async Task<ActionResult<WorkOrderPricingResponse>> GetWorkOrderPricing(Guid workOrderId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanViewPricing();
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");
        var pricing = await contractPricingService.EvaluateAsync(TenantId, workOrder.ClientId, workOrder.SiteId, workOrder.AssetId, workOrder.WorkType, workOrder.Priority, DateTime.UtcNow, workOrder.EstimatedDuration, workOrder.EstimatedMaterialCost, true, cancellationToken);
        return Ok(MapPricing(pricing));
    }

    [HttpPost("/api/workorders/{workOrderId:guid}/recalculate-pricing")]
    public async Task<ActionResult<WorkOrderPricingResponse>> RecalculateWorkOrderPricing(Guid workOrderId, CancellationToken cancellationToken)
    {
        contractAccessService.EnsureCanManageRates();
        var snapshot = await contractOperationalService.RecalculateWorkOrderAsync(TenantId, workOrderId, cancellationToken);
        return Ok(MapPricing(snapshot.Pricing));
    }

    private static WorkOrderPricingResponse MapPricing(ContractPricingEvaluation pricing) =>
        new(pricing.ContractId, pricing.ContractRateCardId, pricing.ContractRateCardName, pricing.ContractRateRuleId, pricing.BillingModel, pricing.CoverageStatus, pricing.AppliedRateSource, pricing.CalloutFee, pricing.FixedVisitFee, pricing.LabourHourlyRate, pricing.OvertimeHourlyRate, pricing.WeekendHourlyRate, pricing.PublicHolidayHourlyRate, pricing.EmergencyMultiplier, pricing.TravelCharge, pricing.MaterialMarkupPercent, pricing.DiscountPercent, pricing.MinimumBillableHours, pricing.IncludedLabourHours, pricing.VatApplicable, pricing.EstimatedCharge, pricing.Warnings);
}

public sealed record UpsertContractRequest(
    string ContractNumber,
    Guid ClientId,
    string Name,
    ContractType ContractType,
    ContractStatus Status,
    DateTime StartDate,
    DateTime EndDate,
    int? DurationMonths,
    BillingModel BillingModel,
    string Currency,
    decimal? ContractValue,
    int RenewalNoticeDays,
    string? RenewalBehavior,
    string? Description,
    string? IncludedServices,
    string? ExcludedServices,
    string? Notes,
    Guid? RenewalOwnerUserId,
    Guid? AccountManagerUserId,
    Guid? InternalApproverUserId,
    bool AutoExpireEnabled,
    int GracePeriodDays,
    bool AllowPreferentialRatesDuringGracePeriod,
    bool AllowSlaDuringGracePeriod,
    string? GracePeriodPolicy,
    decimal? ResponseTimeHours,
    decimal? ResolutionTimeHours,
    string? WorkingHoursPolicy,
    bool PauseSlaOnHold,
    string? EscalationEmail,
    string? PriorityLevel,
    bool GracePeriodAllowsSla,
    IReadOnlyCollection<ContractSlaRowRequest>? SlaMatrix,
    IReadOnlyCollection<Guid> SiteIds,
    IReadOnlyCollection<Guid> AssetIds,
    IReadOnlyCollection<ContractAssetCategoryRequest> AssetCategories,
    IReadOnlyCollection<ContractWorkOrderTypeRequest> WorkOrderTypes,
    IReadOnlyCollection<UpsertContractPmEntitlementRequest> PmEntitlements);

public sealed record ContractAssetCategoryRequest(Guid? AssetCategoryId, string? AssetCategoryName);
public sealed record ContractWorkOrderTypeRequest(Guid? WorkOrderTypeId, string? WorkOrderTypeName);
public sealed record ContractSlaRowRequest(string Priority, decimal? ResponseTimeHours, decimal? ResolutionTimeHours);

public sealed record UpsertContractPmEntitlementRequest(
    Guid? AssetId,
    string? AssetCategory,
    string WorkOrderType,
    int VisitsPerPeriod,
    PeriodType PeriodType,
    int IncludedVisits,
    int UsedVisits,
    DateTime EffectiveFrom,
    DateTime EffectiveTo,
    bool IsActive,
    string? Notes);

public sealed record ContractListItemResponse(
    Guid Id,
    string ContractNumber,
    Guid ClientId,
    string? ClientName,
    string Name,
    ContractType ContractType,
    ContractStatus Status,
    DateTime StartDate,
    DateTime EndDate,
    int DaysToExpiry,
    RenewalRisk RenewalRisk,
    BillingModel BillingModel,
    Guid? OwnerUserId,
    string? OwnerName,
    decimal? ContractValue);

public sealed record ContractDetailResponse(
    Guid Id,
    string ContractNumber,
    Guid ClientId,
    string? ClientName,
    string Name,
    ContractType ContractType,
    ContractStatus Status,
    DateTime StartDate,
    DateTime EndDate,
    int? DurationMonths,
    BillingModel BillingModel,
    string Currency,
    decimal? ContractValue,
    int RenewalNoticeDays,
    string RenewalBehavior,
    string? Description,
    string? IncludedServices,
    string? ExcludedServices,
    string? Notes,
    DateTime? ActivatedAt,
    DateTime? SubmittedForApprovalAt,
    DateTime? ApprovedAt,
    DateTime? ExpiryCheckedAt,
    Guid? RenewalOwnerUserId,
    string? RenewalOwnerName,
    Guid? AccountManagerUserId,
    string? AccountManagerName,
    Guid? InternalApproverUserId,
    string? InternalApproverName,
    DateTime? RenewalStartedAt,
    Guid? RenewedFromContractId,
    Guid? RenewedToContractId,
    DateTime? TerminationDate,
    string? TerminationReason,
    string? SuspensionReason,
    string? CancellationReason,
    DateTime? LastReminderSentAt,
    DateTime? NextReminderAt,
    bool AutoExpireEnabled,
    RenewalRisk RenewalRisk,
    int GracePeriodDays,
    bool AllowPreferentialRatesDuringGracePeriod,
    bool AllowSlaDuringGracePeriod,
    string GracePeriodPolicy,
    decimal? ResponseTimeHours,
    decimal? ResolutionTimeHours,
    string? WorkingHoursPolicy,
    bool PauseSlaOnHold,
    string? EscalationEmail,
    string? PriorityLevel,
    bool GracePeriodAllowsSla,
    IReadOnlyCollection<ContractSlaRowResponse> SlaMatrix,
    IReadOnlyCollection<ContractSiteResponse> Sites,
    IReadOnlyCollection<ContractAssetResponse> Assets,
    IReadOnlyCollection<ContractAssetCategoryResponse> AssetCategories,
    IReadOnlyCollection<ContractWorkOrderTypeResponse> WorkOrderTypes,
    IReadOnlyCollection<ContractPmEntitlementResponse> PmEntitlements,
    IReadOnlyCollection<ContractReminderRuleResponse> ReminderRules,
    IReadOnlyCollection<ContractRateCardResponse> RateCards,
    IReadOnlyCollection<ContractLifecycleEventResponse> LifecycleEvents,
    IReadOnlyCollection<ContractRenewalSummaryResponse> Renewals,
    IReadOnlyCollection<ContractWorkOrderSummaryResponse> WorkOrders);

public sealed record ContractSiteResponse(Guid SiteId, string? SiteCode, string? SiteName);
public sealed record ContractAssetResponse(Guid AssetId, string? AssetCode, string? AssetName);
public sealed record ContractAssetCategoryResponse(Guid? AssetCategoryId, string? AssetCategoryName);
public sealed record ContractWorkOrderTypeResponse(Guid? WorkOrderTypeId, string? WorkOrderTypeName);
public sealed record ContractSlaRowResponse(string Priority, decimal? ResponseTimeHours, decimal? ResolutionTimeHours);
public sealed record ContractPmEntitlementResponse(Guid Id, Guid? AssetId, string? AssetName, string? AssetCategory, string WorkOrderType, int VisitsPerPeriod, PeriodType PeriodType, int IncludedVisits, int UsedVisits, int RemainingVisits, DateTime EffectiveFrom, DateTime EffectiveTo, bool IsActive, string? Notes);
public sealed record ContractReminderRuleResponse(Guid Id, Guid? ContractId, int DaysBeforeExpiry, bool NotifyAccountManager, bool NotifyRenewalOwner, bool NotifySupervisor, bool NotifyFinance, bool NotifyTenantAdmin, bool EmailEnabled, bool InAppEnabled, bool EscalationEnabled, int? EscalationAfterDays, bool IsActive);
public sealed record ContractRateCardResponse(Guid Id, Guid ContractId, string Name, string? Description, string Currency, bool IsDefault, DateTime? EffectiveFrom, DateTime? EffectiveTo, bool IsActive, IReadOnlyCollection<ContractRateRuleResponse> Rules);
public sealed record ContractRateRuleResponse(Guid Id, Guid RateCardId, Guid? WorkOrderTypeId, string? WorkOrderTypeName, Guid? AssetCategoryId, string? AssetCategoryName, Guid? SiteId, string? Priority, BillingModel BillingModel, decimal? CalloutFee, decimal? FixedVisitFee, decimal? LabourHourlyRate, decimal? OvertimeHourlyRate, decimal? WeekendHourlyRate, decimal? PublicHolidayHourlyRate, decimal? EmergencyMultiplier, decimal? TravelCharge, decimal? MaterialMarkupPercent, decimal? DiscountPercent, decimal? MinimumBillableHours, decimal? IncludedLabourHours, bool VatApplicable, string? Notes, bool IsActive);
public sealed record ContractLifecycleEventResponse(Guid Id, ContractLifecycleEventType EventType, ContractStatus? OldStatus, ContractStatus? NewStatus, string? Note, string? Reason, Guid? CreatedByUserId, string? CreatedByUserName, DateTime CreatedAt);
public sealed record ContractRenewalSummaryResponse(Guid Id, Guid OriginalContractId, Guid? RenewedContractId, ContractRenewalStatus RenewalStatus, Guid? OwnerUserId, DateTime? RenewalStartedAt, DateTime? RenewalCompletedAt, string? Notes);
public sealed record ContractWorkOrderSummaryResponse(Guid Id, string WorkOrderNumber, string Title, string Status, string? CoverageStatus, string? BillingModel, decimal? EstimatedCharge, DateTime CreatedAt);

public sealed record UpsertContractReminderRuleRequest(
    int DaysBeforeExpiry,
    bool NotifyAccountManager,
    bool NotifyRenewalOwner,
    bool NotifySupervisor,
    bool NotifyFinance,
    bool NotifyTenantAdmin,
    bool EmailEnabled,
    bool InAppEnabled,
    bool EscalationEnabled,
    int? EscalationAfterDays,
    bool IsActive);

public sealed record UpsertContractRateCardRequest(
    string Name,
    string? Description,
    string Currency,
    bool IsDefault,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive);

public sealed record UpsertContractRateRuleRequest(
    Guid? WorkOrderTypeId,
    string? WorkOrderTypeName,
    Guid? AssetCategoryId,
    string? AssetCategoryName,
    Guid? SiteId,
    string? Priority,
    BillingModel BillingModel,
    decimal? CalloutFee,
    decimal? FixedVisitFee,
    decimal? LabourHourlyRate,
    decimal? OvertimeHourlyRate,
    decimal? WeekendHourlyRate,
    decimal? PublicHolidayHourlyRate,
    decimal? EmergencyMultiplier,
    decimal? TravelCharge,
    decimal? MaterialMarkupPercent,
    decimal? DiscountPercent,
    decimal? MinimumBillableHours,
    decimal? IncludedLabourHours,
    bool VatApplicable,
    string? Notes,
    bool IsActive);

public sealed record ContractActionRequest(string? Note, string? Reason);
public sealed record TerminateContractRequest(DateTime? TerminationDate, string? Reason);
public sealed record ContractRenewRequest(DateTime? StartDate, DateTime? EndDate, int? DurationMonths, bool ActivateNewContract, bool SubmitForApproval, string? Name, decimal? ContractValue, string? Notes);
public sealed record ContractRenewalResultResponse(Guid OldContractId, Guid NewContractId, string OldContractNumber, string NewContractNumber, ContractStatus OldStatus, ContractStatus NewStatus);
public sealed record WorkOrderPricingResponse(Guid? ContractId, Guid? RateCardId, string? RateCardName, Guid? RateRuleId, BillingModel BillingModel, CoverageStatus CoverageStatus, string AppliedRateSource, decimal CalloutFee, decimal FixedVisitFee, decimal LabourHourlyRate, decimal OvertimeHourlyRate, decimal WeekendHourlyRate, decimal PublicHolidayHourlyRate, decimal EmergencyMultiplier, decimal TravelCharge, decimal MaterialMarkupPercent, decimal DiscountPercent, decimal MinimumBillableHours, decimal IncludedLabourHours, bool VatApplicable, decimal EstimatedCharge, IReadOnlyCollection<string> Warnings);
