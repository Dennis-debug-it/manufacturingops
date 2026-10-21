using System.Globalization;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The money side of a project (Release 2): office-set daily rates and the wage sheet, budget and
/// cost entries, the section 7 financials, valuations, the advance and retention releases. Every
/// route needs the Finance module as well as Projects. Reading needs projects.financials.view and
/// changing needs projects.financials.manage.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}")]
public sealed class ProjectCommercialController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IProjectAccessService projectAccessService,
    IProjectSettingsService projectSettingsService,
    IProjectFinancialsService financialsService,
    IProjectInvoiceService projectInvoiceService,
    IDocumentNumberingService documentNumberingService,
    ITenantTimeZoneService tenantTimeZoneService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    // ── Billing terms ─────────────────────────────────────────────────────────

    [HttpGet("billing-terms")]
    public async Task<ActionResult<ProjectBillingTermsResponse>> GetBillingTerms(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await LoadForReadAsync(projectId, cancellationToken);
        return Ok(await MapTermsAsync(project, cancellationToken));
    }

    /// <summary>
    /// Retention and advance terms from the contract. They're fixed once a valuation is certified,
    /// because every certificate after that is worked out from them.
    /// </summary>
    [HttpPut("billing-terms")]
    public async Task<ActionResult<ProjectBillingTermsResponse>> UpdateBillingTerms(Guid projectId, [FromBody] UpdateBillingTermsRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        if (request.RetentionPercent is < 0 or > 100 || request.AdvanceRecoveryPercent is < 0 or > 100 || request.RetentionReleaseAtCompletionPercent is < 0 or > 100)
            throw new BusinessRuleException("Percentages must be between 0 and 100.");
        if (request.RetentionCapAmount is < 0 || request.AdvancePaymentAmount is < 0)
            throw new BusinessRuleException("Amounts cannot be negative.");
        if (request.DefectsLiabilityMonths is < 0 or > 120)
            throw new BusinessRuleException("The defects liability period must be between 0 and 120 months.");
        if (request.AdvancePaymentAmount is > 0 && request.AdvanceRecoveryPercent is not > 0)
            throw new BusinessRuleException("Set the recovery percentage for the advance.");

        var certified = await dbContext.ProjectValuations.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == projectId
            && (x.Status == ProjectValuationStatuses.Certified || x.Status == ProjectValuationStatuses.Invoiced), cancellationToken);
        var calculationTermsChanged = project.RetentionPercent != request.RetentionPercent || project.RetentionCapAmount != request.RetentionCapAmount
            || project.AdvancePaymentAmount != request.AdvancePaymentAmount || project.AdvanceRecoveryPercent != request.AdvanceRecoveryPercent;
        if (certified && calculationTermsChanged)
            throw new BusinessRuleException("Retention and advance terms are fixed once a valuation is certified.");
        if (project.AdvanceInvoiceId.HasValue && project.AdvancePaymentAmount != request.AdvancePaymentAmount
            && await InvoiceIsLiveAsync(project.AdvanceInvoiceId.Value, cancellationToken))
            throw new BusinessRuleException("The advance is already invoiced. Void that invoice before changing the amount.");

        project.RetentionPercent = request.RetentionPercent is > 0 ? request.RetentionPercent : null;
        project.RetentionCapAmount = request.RetentionCapAmount is > 0 ? request.RetentionCapAmount : null;
        project.RetentionReleaseAtCompletionPercent = request.RetentionReleaseAtCompletionPercent ?? 50;
        project.DefectsLiabilityMonths = request.DefectsLiabilityMonths;
        project.AdvancePaymentAmount = request.AdvancePaymentAmount is > 0 ? request.AdvancePaymentAmount : null;
        project.AdvanceRecoveryPercent = request.AdvancePaymentAmount is > 0 ? request.AdvanceRecoveryPercent : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.billing-terms.updated", nameof(Project), project.Id.ToString(),
            $"Billing terms updated on {project.ProjectNumber}: retention {project.RetentionPercent ?? 0}%, advance {project.Currency} {project.AdvancePaymentAmount ?? 0:N2}.", cancellationToken);
        return Ok(await MapTermsAsync(project, cancellationToken));
    }

    // ── Daily rates and wage sheet ─────────────────────────────────────────────

    /// <summary>The roster with each worker's current rate and the days not yet costed. Office only.</summary>
    [HttpGet("worker-rates")]
    public async Task<ActionResult<IReadOnlyCollection<WorkerRatesResponse>>> ListWorkerRates(Guid projectId, CancellationToken cancellationToken)
    {
        await LoadForReadAsync(projectId, cancellationToken);
        var today = await TodayAsync(cancellationToken);
        var workers = await dbContext.ProjectWorkers.Where(x => x.TenantId == TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.FullName).ToListAsync(cancellationToken);
        var workerIds = workers.Select(x => x.Id).ToList();
        var rates = await dbContext.ProjectWorkerRates.Where(x => x.TenantId == TenantId && workerIds.Contains(x.ProjectWorkerId)).ToListAsync(cancellationToken);
        var muster = await dbContext.ProjectMusterEntries.Where(x => x.TenantId == TenantId && x.ProjectId == projectId).ToListAsync(cancellationToken);
        var labour = ProjectLabour.Summarize(muster, rates).ToDictionary(x => x.WorkerId);

        return Ok(workers.Select(worker =>
        {
            var workerRates = rates.Where(x => x.ProjectWorkerId == worker.Id).OrderByDescending(x => x.EffectiveFrom).ToList();
            var summary = labour.GetValueOrDefault(worker.Id);
            return new WorkerRatesResponse(worker.Id, worker.FullName, worker.Phone, worker.Trade, worker.EngagementType, worker.IsActive,
                ProjectLabour.RateOn(workerRates, today), summary?.ManDays ?? 0, summary?.UncostedDays ?? 0, summary?.Cost ?? 0,
                workerRates.Select(MapRate).ToList());
        }).ToList());
    }

    /// <summary>
    /// Sets a worker's daily rate from a date. Days on or after it are costed at this rate until the
    /// next one; earlier days keep their rate. Setting a rate on a date that already has one replaces it.
    /// </summary>
    [HttpPost("workers/{workerId:guid}/rates")]
    public async Task<ActionResult<WorkerRateResponse>> SetWorkerRate(Guid projectId, Guid workerId, [FromBody] SetWorkerRateRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        var worker = await dbContext.ProjectWorkers.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == workerId, cancellationToken)
            ?? throw new NotFoundException("Worker not found on this project.");
        if (request.DailyRate <= 0)
            throw new BusinessRuleException("Daily rate must be greater than zero.");

        var rate = await dbContext.ProjectWorkerRates.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectWorkerId == workerId && x.EffectiveFrom == request.EffectiveFrom, cancellationToken);
        if (rate is null)
        {
            rate = new ProjectWorkerRate { TenantId = TenantId, ProjectWorkerId = workerId, EffectiveFrom = request.EffectiveFrom };
            dbContext.ProjectWorkerRates.Add(rate);
        }
        rate.DailyRate = Math.Round(request.DailyRate, 2);
        rate.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        rate.SetByUserId = UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.worker-rate.set", nameof(ProjectWorker), worker.Id.ToString(),
            $"{worker.FullName} on {project.ProjectNumber}: {project.Currency} {rate.DailyRate:N2} a day from {rate.EffectiveFrom:yyyy-MM-dd}.", cancellationToken);
        return Ok(MapRate(rate));
    }

    /// <summary>What each worker is owed for a date range: man-days at the rate in effect on each day. Ecosys records it; it doesn't pay anyone.</summary>
    [HttpGet("wage-sheet")]
    public async Task<IActionResult> GetWageSheet(Guid projectId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var project = await LoadForReadAsync(projectId, cancellationToken);
        if (to < from)
            throw new BusinessRuleException("The end date must be on or after the start date.");
        if (to.DayNumber - from.DayNumber > 92)
            throw new BusinessRuleException("A wage sheet covers at most three months.");

        var entries = await dbContext.ProjectMusterEntries
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId && x.WorkDate >= from && x.WorkDate <= to)
            .ToListAsync(cancellationToken);
        var workerIds = entries.Select(x => x.ProjectWorkerId).Distinct().ToList();
        var workers = await dbContext.ProjectWorkers.Where(x => x.TenantId == TenantId && workerIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var rates = await dbContext.ProjectWorkerRates.Where(x => x.TenantId == TenantId && workerIds.Contains(x.ProjectWorkerId)).ToListAsync(cancellationToken);
        var labour = ProjectLabour.Summarize(entries, rates).ToDictionary(x => x.WorkerId);

        var rows = workers
            .Select(worker =>
            {
                var marks = entries.Where(x => x.ProjectWorkerId == worker.Id).ToList();
                var summary = labour.GetValueOrDefault(worker.Id);
                return new WageSheetRow(worker.Id, worker.FullName, worker.Phone, worker.Trade, worker.EngagementType,
                    marks.Count(x => x.Status == ProjectMusterStatuses.Present), marks.Count(x => x.Status == ProjectMusterStatuses.HalfDay),
                    summary?.ManDays ?? 0, ProjectLabour.RateOn(rates.Where(x => x.ProjectWorkerId == worker.Id), to),
                    summary?.Cost ?? 0, summary?.UncostedDays ?? 0);
            })
            .Where(x => x.ManDays > 0)
            // Workers with days the office hasn't costed yet come first, so they get a rate before payday.
            .OrderByDescending(x => x.UncostedDays > 0).ThenBy(x => x.FullName)
            .ToList();
        var sheet = new WageSheetResponse(project.ProjectNumber, project.Name, project.Currency, from, to, rows,
            rows.Sum(x => x.ManDays), rows.Sum(x => x.Amount), rows.Count(x => x.UncostedDays > 0));

        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return Ok(sheet);

        var csv = new StringBuilder();
        csv.AppendLine("Worker,Phone,Trade,Engagement,Full days,Half days,Man-days,Daily rate,Amount,Uncosted days");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',', Csv(row.FullName), Csv(row.Phone), Csv(row.Trade), Csv(row.EngagementType),
                row.FullDays, row.HalfDays, Num(row.ManDays), row.CurrentRate.HasValue ? Num(row.CurrentRate.Value) : "", Num(row.Amount), Num(row.UncostedDays)));
        }
        csv.AppendLine(string.Join(',', "Total", "", "", "", "", "", Num(sheet.TotalManDays), "", Num(sheet.TotalAmount), ""));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"{project.ProjectNumber}-wages-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");

        static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            // Neutralise spreadsheet formulas and quote anything with separators.
            var safe = value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
            return safe.Contains(',') || safe.Contains('"') || safe.Contains('\n') ? $"\"{safe.Replace("\"", "\"\"")}\"" : safe;
        }
    }

    // ── Budget and cost entries ───────────────────────────────────────────────

    [HttpGet("budget-lines")]
    public async Task<ActionResult<IReadOnlyCollection<BudgetLineResponse>>> ListBudgetLines(Guid projectId, CancellationToken cancellationToken)
    {
        await LoadForReadAsync(projectId, cancellationToken);
        return Ok(await dbContext.ProjectBudgetLines.Where(x => x.TenantId == TenantId && x.ProjectId == projectId)
            .OrderBy(x => x.Category).ThenBy(x => x.CreatedAt)
            .Select(x => new BudgetLineResponse(x.Id, x.Category, x.Description, x.Amount))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("budget-lines")]
    public async Task<ActionResult<BudgetLineResponse>> CreateBudgetLine(Guid projectId, [FromBody] UpsertBudgetLineRequest request, CancellationToken cancellationToken)
    {
        await LoadForWriteAsync(projectId, cancellationToken);
        var line = new ProjectBudgetLine { TenantId = TenantId, ProjectId = projectId };
        ApplyBudget(line, request);
        dbContext.ProjectBudgetLines.Add(line);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new BudgetLineResponse(line.Id, line.Category, line.Description, line.Amount));
    }

    [HttpPut("budget-lines/{lineId:guid}")]
    public async Task<ActionResult<BudgetLineResponse>> UpdateBudgetLine(Guid projectId, Guid lineId, [FromBody] UpsertBudgetLineRequest request, CancellationToken cancellationToken)
    {
        await LoadForWriteAsync(projectId, cancellationToken);
        var line = await dbContext.ProjectBudgetLines.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == lineId, cancellationToken)
            ?? throw new NotFoundException("Budget line not found.");
        ApplyBudget(line, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new BudgetLineResponse(line.Id, line.Category, line.Description, line.Amount));
    }

    [HttpDelete("budget-lines/{lineId:guid}")]
    public async Task<IActionResult> DeleteBudgetLine(Guid projectId, Guid lineId, CancellationToken cancellationToken)
    {
        await LoadForWriteAsync(projectId, cancellationToken);
        var line = await dbContext.ProjectBudgetLines.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == lineId, cancellationToken)
            ?? throw new NotFoundException("Budget line not found.");
        dbContext.ProjectBudgetLines.Remove(line);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("cost-entries")]
    public async Task<ActionResult<IReadOnlyCollection<CostEntryResponse>>> ListCostEntries(Guid projectId, CancellationToken cancellationToken)
    {
        await LoadForReadAsync(projectId, cancellationToken);
        return Ok(await dbContext.ProjectCostEntries.Where(x => x.TenantId == TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.IncurredOn).ThenByDescending(x => x.CreatedAt)
            .Select(x => new CostEntryResponse(x.Id, x.Category, x.Description, x.Amount, x.IncurredOn, x.RecordedByUserId))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("cost-entries")]
    public async Task<ActionResult<CostEntryResponse>> CreateCostEntry(Guid projectId, [FromBody] UpsertCostEntryRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        var entry = new ProjectCostEntry { TenantId = TenantId, ProjectId = projectId, RecordedByUserId = UserId };
        ApplyCost(entry, request);
        dbContext.ProjectCostEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.cost.recorded", nameof(ProjectCostEntry), entry.Id.ToString(),
            $"{entry.Category} cost of {project.Currency} {entry.Amount:N2} recorded on {project.ProjectNumber}: {entry.Description}.", cancellationToken);
        return Ok(MapCost(entry));
    }

    [HttpPut("cost-entries/{entryId:guid}")]
    public async Task<ActionResult<CostEntryResponse>> UpdateCostEntry(Guid projectId, Guid entryId, [FromBody] UpsertCostEntryRequest request, CancellationToken cancellationToken)
    {
        await LoadForWriteAsync(projectId, cancellationToken);
        var entry = await LoadCostAsync(projectId, entryId, cancellationToken);
        ApplyCost(entry, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapCost(entry));
    }

    [HttpDelete("cost-entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteCostEntry(Guid projectId, Guid entryId, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        var entry = await LoadCostAsync(projectId, entryId, cancellationToken);
        dbContext.ProjectCostEntries.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.cost.deleted", nameof(ProjectCostEntry), entry.Id.ToString(),
            $"{entry.Category} cost of {project.Currency} {entry.Amount:N2} removed from {project.ProjectNumber}: {entry.Description}.", cancellationToken);
        return NoContent();
    }

    // ── Financials ─────────────────────────────────────────────────────────────

    [HttpGet("financials")]
    public async Task<ActionResult<ProjectFinancials>> GetFinancials(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await LoadForReadAsync(projectId, cancellationToken);
        if (!(await projectSettingsService.GetAsync(TenantId, cancellationToken)).Financials)
            throw new BusinessRuleException("Project financials are turned off in Project settings.");
        return Ok(await financialsService.ComputeAsync(TenantId, project, cancellationToken));
    }

    // ── Valuations ─────────────────────────────────────────────────────────────

    [HttpGet("valuations")]
    public async Task<ActionResult<IReadOnlyCollection<ValuationResponse>>> ListValuations(Guid projectId, CancellationToken cancellationToken)
    {
        await LoadForReadAsync(projectId, cancellationToken);
        var valuations = await dbContext.ProjectValuations
            .Include(x => x.CertifiedByConsultant).Include(x => x.Invoice)
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.PeriodEnd).ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(valuations.Select(MapValuation).ToList());
    }

    /// <summary>
    /// Works out a valuation without saving it, for the form. With no work-done figure it suggests
    /// progress × revised value; that is a starting point only, the QS's BQ figure replaces it.
    /// </summary>
    [HttpGet("valuations/preview")]
    public async Task<ActionResult<ValuationPreviewResponse>> PreviewValuation(Guid projectId, [FromQuery] decimal? workDoneToDate, [FromQuery] decimal? materialsOnSite,
        [FromQuery] Guid? valuationId, CancellationToken cancellationToken)
    {
        var project = await LoadForReadAsync(projectId, cancellationToken);
        var suggested = Math.Round(project.RevisedValue * project.EffectiveProgressPercent / 100m, 2);
        var (previous, certifiedBefore) = await PriorCertifiedAsync(projectId, valuationId, cancellationToken);
        var result = ProjectValuationCalculator.Compute(project, workDoneToDate ?? suggested, materialsOnSite ?? 0, previous, certifiedBefore);
        return Ok(new ValuationPreviewResponse(suggested, workDoneToDate ?? suggested, materialsOnSite ?? 0, result.GrossValueToDate, result.RetentionToDate,
            result.AdvanceRecoveredToDate, result.PreviouslyCertified, result.AmountDue, previous?.WorkDoneToDate ?? 0));
    }

    [HttpPost("valuations")]
    public async Task<ActionResult<ValuationResponse>> CreateValuation(Guid projectId, [FromBody] UpsertValuationRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        if (!(await projectSettingsService.GetAsync(TenantId, cancellationToken)).Valuations)
            throw new BusinessRuleException("Valuations are turned off in Project settings.");
        // One valuation in progress at a time: each one builds on the last certified figures.
        if (await dbContext.ProjectValuations.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == projectId
                && (x.Status == ProjectValuationStatuses.Draft || x.Status == ProjectValuationStatuses.Submitted), cancellationToken))
            throw new BusinessRuleException("Finish or void the valuation in progress before starting another.");

        var valuation = new ProjectValuation
        {
            TenantId = TenantId,
            ProjectId = projectId,
            ValuationNumber = await documentNumberingService.GenerateAsync(TenantId, project.BranchId, DocumentTypes.ProjectValuation, cancellationToken),
        };
        await ApplyValuationAsync(project, valuation, request, cancellationToken);
        dbContext.ProjectValuations.Add(valuation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.valuation.created", nameof(ProjectValuation), valuation.Id.ToString(),
            $"Valuation {valuation.ValuationNumber} started on {project.ProjectNumber} (period to {valuation.PeriodEnd:yyyy-MM-dd}).", cancellationToken);
        return Ok(MapValuation(valuation));
    }

    [HttpPut("valuations/{valuationId:guid}")]
    public async Task<ActionResult<ValuationResponse>> UpdateValuation(Guid projectId, Guid valuationId, [FromBody] UpsertValuationRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        var valuation = await LoadValuationAsync(projectId, valuationId, cancellationToken);
        if (valuation.Status != ProjectValuationStatuses.Draft)
            throw new BusinessRuleException("Only a draft valuation can be edited.");
        await ApplyValuationAsync(project, valuation, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapValuation(valuation));
    }

    [HttpPost("valuations/{valuationId:guid}/submit")]
    public async Task<ActionResult<ValuationResponse>> SubmitValuation(Guid projectId, Guid valuationId, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        var valuation = await LoadValuationAsync(projectId, valuationId, cancellationToken);
        if (valuation.Status != ProjectValuationStatuses.Draft)
            throw new BusinessRuleException("Only a draft valuation can be submitted.");
        Recompute(project, valuation, await PriorCertifiedAsync(projectId, valuation.Id, cancellationToken));
        valuation.Status = ProjectValuationStatuses.Submitted;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.valuation.submitted", nameof(ProjectValuation), valuation.Id.ToString(),
            $"Valuation {valuation.ValuationNumber} submitted for certification ({project.Currency} {valuation.AmountDue:N2} due).", cancellationToken);
        return Ok(MapValuation(valuation));
    }

    /// <summary>Records the consultant's certificate. The certified amount is what gets invoiced, even when it differs from the calculation.</summary>
    [HttpPost("valuations/{valuationId:guid}/certify")]
    public async Task<ActionResult<ValuationResponse>> CertifyValuation(Guid projectId, Guid valuationId, [FromBody] CertifyValuationRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        var valuation = await LoadValuationAsync(projectId, valuationId, cancellationToken);
        if (valuation.Status != ProjectValuationStatuses.Submitted)
            throw new BusinessRuleException("Submit the valuation before recording its certificate.");
        if (string.IsNullOrWhiteSpace(request.CertificateReference))
            throw new BusinessRuleException("Enter the certificate number.");
        if (request.CertifiedAmount < 0)
            throw new BusinessRuleException("The certified amount cannot be negative.");
        if (request.CertifiedOn > await TodayAsync(cancellationToken))
            throw new BusinessRuleException("The certificate date can't be in the future.");
        if (request.ConsultantId.HasValue && !await dbContext.ProjectConsultants.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.ConsultantId == request.ConsultantId, cancellationToken))
            throw new BusinessRuleException("The certifying consultant must be on this project's consultant list.");

        valuation.CertifiedByConsultantId = request.ConsultantId;
        valuation.CertificateReference = request.CertificateReference.Trim();
        valuation.CertifiedOn = request.CertifiedOn;
        valuation.CertifiedAmount = Math.Round(request.CertifiedAmount, 2);
        valuation.Status = ProjectValuationStatuses.Certified;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.valuation.certified", nameof(ProjectValuation), valuation.Id.ToString(),
            $"Valuation {valuation.ValuationNumber} certified under {valuation.CertificateReference} for {project.Currency} {valuation.CertifiedAmount:N2}"
            + (valuation.CertifiedAmount != valuation.AmountDue ? $" (calculated {project.Currency} {valuation.AmountDue:N2})." : "."), cancellationToken);
        await LoadValuationNavigationsAsync(valuation, cancellationToken);
        return Ok(MapValuation(valuation));
    }

    /// <summary>Raises a draft invoice for the certified amount plus VAT. If that invoice is later voided, a new one can be raised.</summary>
    [HttpPost("valuations/{valuationId:guid}/raise-invoice")]
    public async Task<ActionResult<ValuationResponse>> RaiseValuationInvoice(Guid projectId, Guid valuationId, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken, allowAfterClosure: true);
        var valuation = await LoadValuationAsync(projectId, valuationId, cancellationToken);
        var reraise = valuation.Status == ProjectValuationStatuses.Invoiced && valuation.InvoiceId.HasValue && !await InvoiceIsLiveAsync(valuation.InvoiceId.Value, cancellationToken);
        if (valuation.Status != ProjectValuationStatuses.Certified && !reraise)
            throw new BusinessRuleException(valuation.Status == ProjectValuationStatuses.Invoiced
                ? "This valuation is already invoiced."
                : "Record the certificate before raising the invoice.");

        var invoice = await projectInvoiceService.CreateDraftInvoiceAsync(TenantId, project,
            $"Interim payment certificate {valuation.CertificateReference} (valuation {valuation.ValuationNumber}, work to {valuation.PeriodEnd:dd MMM yyyy})",
            valuation.CertifiedAmount ?? valuation.AmountDue, valuation.Id, cancellationToken);
        valuation.InvoiceId = invoice.Id;
        valuation.Status = ProjectValuationStatuses.Invoiced;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.valuation.invoiced", nameof(ProjectValuation), valuation.Id.ToString(),
            $"Draft invoice {invoice.InvoiceNumber} raised from valuation {valuation.ValuationNumber}.", cancellationToken);
        await LoadValuationNavigationsAsync(valuation, cancellationToken);
        return Ok(MapValuation(valuation));
    }

    /// <summary>
    /// Voids the latest valuation. An invoiced valuation can be voided only after its invoice is
    /// voided, so the ledger and the certificate never disagree.
    /// </summary>
    [HttpPost("valuations/{valuationId:guid}/void")]
    public async Task<ActionResult<ValuationResponse>> VoidValuation(Guid projectId, Guid valuationId, [FromBody] VoidValuationRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        var valuation = await LoadValuationAsync(projectId, valuationId, cancellationToken);
        if (valuation.Status == ProjectValuationStatuses.Void)
            throw new BusinessRuleException("This valuation is already void.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BusinessRuleException("Give a reason for voiding the valuation.");
        if (valuation.Status == ProjectValuationStatuses.Invoiced && valuation.InvoiceId.HasValue && await InvoiceIsLiveAsync(valuation.InvoiceId.Value, cancellationToken))
            throw new BusinessRuleException("Void the valuation's invoice first.");
        if (await dbContext.ProjectValuations.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id != valuation.Id
                && x.Status != ProjectValuationStatuses.Void && (x.PeriodEnd > valuation.PeriodEnd || (x.PeriodEnd == valuation.PeriodEnd && x.CreatedAt > valuation.CreatedAt)), cancellationToken))
            throw new BusinessRuleException("Only the latest valuation can be voided; later ones are worked out from it.");

        valuation.Status = ProjectValuationStatuses.Void;
        valuation.Notes = string.IsNullOrWhiteSpace(valuation.Notes) ? $"Voided: {request.Reason.Trim()}" : $"{valuation.Notes}\nVoided: {request.Reason.Trim()}";
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.valuation.voided", nameof(ProjectValuation), valuation.Id.ToString(),
            $"Valuation {valuation.ValuationNumber} on {project.ProjectNumber} voided. Reason: {request.Reason.Trim()}", cancellationToken);
        await LoadValuationNavigationsAsync(valuation, cancellationToken);
        return Ok(MapValuation(valuation));
    }

    // ── Advance and retention ─────────────────────────────────────────────────

    [HttpPost("advance-invoice")]
    public async Task<ActionResult<ProjectBillingTermsResponse>> RaiseAdvanceInvoice(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken);
        if (project.AdvancePaymentAmount is not > 0)
            throw new BusinessRuleException("Set the advance amount in the billing terms first.");
        if (project.AdvanceInvoiceId.HasValue && await InvoiceIsLiveAsync(project.AdvanceInvoiceId.Value, cancellationToken))
            throw new BusinessRuleException("The advance is already invoiced.");

        var invoice = await projectInvoiceService.CreateDraftInvoiceAsync(TenantId, project, $"Advance payment for {project.ProjectNumber} {project.Name}",
            project.AdvancePaymentAmount.Value, null, cancellationToken);
        project.AdvanceInvoiceId = invoice.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.advance.invoiced", nameof(Project), project.Id.ToString(),
            $"Draft advance invoice {invoice.InvoiceNumber} raised on {project.ProjectNumber}.", cancellationToken);
        return Ok(await MapTermsAsync(project, cancellationToken));
    }

    [HttpGet("retention")]
    public async Task<ActionResult<RetentionResponse>> GetRetention(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await LoadForReadAsync(projectId, cancellationToken);
        return Ok(await MapRetentionAsync(project, cancellationToken));
    }

    /// <summary>
    /// Releases one part of the retention and raises its invoice: the completion share after
    /// handover, the balance after the defects liability period. If a release invoice is voided,
    /// releasing the same stage again raises a fresh invoice for it.
    /// </summary>
    [HttpPost("retention-releases")]
    public async Task<ActionResult<RetentionResponse>> ReleaseRetention(Guid projectId, [FromBody] ReleaseRetentionRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForWriteAsync(projectId, cancellationToken, allowAfterClosure: true);
        var stage = request.Stage?.Trim() switch
        {
            var s when string.Equals(s, ProjectRetentionStages.PracticalCompletion, StringComparison.OrdinalIgnoreCase) => ProjectRetentionStages.PracticalCompletion,
            var s when string.Equals(s, ProjectRetentionStages.EndOfDefectsLiability, StringComparison.OrdinalIgnoreCase) => ProjectRetentionStages.EndOfDefectsLiability,
            _ => throw new BusinessRuleException("Stage must be PracticalCompletion or EndOfDefectsLiability."),
        };

        var existing = await dbContext.ProjectRetentionReleases.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Stage == stage, cancellationToken);
        TenantInvoice invoice;
        var description = stage == ProjectRetentionStages.PracticalCompletion
            ? $"Release of retention at practical completion, {project.ProjectNumber} {project.Name}"
            : $"Release of retention balance after the defects liability period, {project.ProjectNumber} {project.Name}";
        if (existing is not null)
        {
            if (existing.InvoiceId.HasValue && await InvoiceIsLiveAsync(existing.InvoiceId.Value, cancellationToken))
                throw new BusinessRuleException("This part of the retention is already released.");
            invoice = await projectInvoiceService.CreateDraftInvoiceAsync(TenantId, project, description, existing.Amount, null, cancellationToken);
            existing.InvoiceId = invoice.Id;
            existing.ReleasedByUserId = UserId;
        }
        else
        {
            var (_, completionDue, finalDue) = await financialsService.RetentionPositionAsync(TenantId, project, cancellationToken);
            var amount = stage == ProjectRetentionStages.PracticalCompletion ? completionDue : finalDue;
            if (amount <= 0)
                throw new BusinessRuleException(stage == ProjectRetentionStages.PracticalCompletion
                    ? "No retention is due yet: it's released after handover."
                    : "No retention balance is due yet: it's released once the defects liability period ends.");
            invoice = await projectInvoiceService.CreateDraftInvoiceAsync(TenantId, project, description, amount, null, cancellationToken);
            dbContext.ProjectRetentionReleases.Add(new ProjectRetentionRelease
            {
                TenantId = TenantId,
                ProjectId = projectId,
                Stage = stage,
                Amount = amount,
                ReleasedOn = await TodayAsync(cancellationToken),
                InvoiceId = invoice.Id,
                ReleasedByUserId = UserId,
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.retention.released", nameof(Project), project.Id.ToString(),
            $"Retention ({stage}) released on {project.ProjectNumber}: draft invoice {invoice.InvoiceNumber}.", cancellationToken);
        return Ok(await MapRetentionAsync(project, cancellationToken));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        DateOnly.FromDateTime(await tenantTimeZoneService.TenantTodayAsync(TenantId, cancellationToken));

    private async Task<Project> LoadForReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projectAccessService.LoadVisibleAsync(TenantId, projectId, cancellationToken);
        projectAccessService.EnsureAny(PermissionNames.ProjectsFinancialsView, PermissionNames.ProjectsFinancialsManage);
        return project;
    }

    /// <summary>
    /// Commercial changes need projects.financials.manage and an open project. Invoicing an existing
    /// certificate or retention release can still happen after closure, since that money is owed
    /// whatever the project's stage.
    /// </summary>
    private async Task<Project> LoadForWriteAsync(Guid projectId, CancellationToken cancellationToken, bool allowAfterClosure = false)
    {
        var project = await projectAccessService.LoadVisibleAsync(TenantId, projectId, cancellationToken);
        projectAccessService.Ensure(PermissionNames.ProjectsFinancialsManage);
        var category = await dbContext.ProjectStages.Where(x => x.Id == project.StageId).Select(x => x.Category).SingleAsync(cancellationToken);
        if (category == ProjectStageCategories.Cancelled || (category == ProjectStageCategories.Closed && !allowAfterClosure))
            throw new BusinessRuleException($"Project {project.ProjectNumber} is {category.ToLowerInvariant()}. Reopen it to make changes.");
        return project;
    }

    private async Task<bool> InvoiceIsLiveAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        await dbContext.TenantInvoices.AnyAsync(x => x.TenantId == TenantId && x.Id == invoiceId && x.Status != "Void", cancellationToken);

    private async Task<ProjectValuation> LoadValuationAsync(Guid projectId, Guid valuationId, CancellationToken cancellationToken) =>
        await dbContext.ProjectValuations.Include(x => x.CertifiedByConsultant).Include(x => x.Invoice)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == valuationId, cancellationToken)
        ?? throw new NotFoundException("Valuation not found.");

    private async Task LoadValuationNavigationsAsync(ProjectValuation valuation, CancellationToken cancellationToken)
    {
        await dbContext.Entry(valuation).Reference(x => x.CertifiedByConsultant).LoadAsync(cancellationToken);
        await dbContext.Entry(valuation).Reference(x => x.Invoice).LoadAsync(cancellationToken);
    }

    /// <summary>The latest earlier certified valuation and the total certified before this one.</summary>
    private async Task<(ProjectValuation? Previous, decimal CertifiedBefore)> PriorCertifiedAsync(Guid projectId, Guid? excludeId, CancellationToken cancellationToken)
    {
        var certified = await dbContext.ProjectValuations.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id != excludeId
                && (x.Status == ProjectValuationStatuses.Certified || x.Status == ProjectValuationStatuses.Invoiced))
            .ToListAsync(cancellationToken);
        var previous = certified.OrderByDescending(x => x.PeriodEnd).ThenByDescending(x => x.CreatedAt).FirstOrDefault();
        return (previous, certified.Sum(x => x.CertifiedAmount ?? x.AmountDue));
    }

    private async Task ApplyValuationAsync(Project project, ProjectValuation valuation, UpsertValuationRequest request, CancellationToken cancellationToken)
    {
        var prior = await PriorCertifiedAsync(project.Id, valuation.Id, cancellationToken);
        if (prior.Previous is not null && request.PeriodEnd <= prior.Previous.PeriodEnd)
            throw new BusinessRuleException($"The period must end after the last certified valuation ({prior.Previous.PeriodEnd:dd MMM yyyy}).");
        if (prior.Previous is not null && request.WorkDoneToDate < prior.Previous.WorkDoneToDate)
            throw new BusinessRuleException("Work done to date can't be less than on the last certified valuation.");
        valuation.PeriodEnd = request.PeriodEnd;
        valuation.WorkDoneToDate = Math.Round(request.WorkDoneToDate, 2);
        valuation.MaterialsOnSite = Math.Round(request.MaterialsOnSite, 2);
        valuation.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        Recompute(project, valuation, prior);
    }

    private static void Recompute(Project project, ProjectValuation valuation, (ProjectValuation? Previous, decimal CertifiedBefore) prior)
    {
        var result = ProjectValuationCalculator.Compute(project, valuation.WorkDoneToDate, valuation.MaterialsOnSite, prior.Previous, prior.CertifiedBefore);
        valuation.GrossValueToDate = result.GrossValueToDate;
        valuation.RetentionToDate = result.RetentionToDate;
        valuation.AdvanceRecoveredToDate = result.AdvanceRecoveredToDate;
        valuation.PreviouslyCertified = result.PreviouslyCertified;
        valuation.AmountDue = result.AmountDue;
    }

    private async Task<ProjectCostEntry> LoadCostAsync(Guid projectId, Guid entryId, CancellationToken cancellationToken) =>
        await dbContext.ProjectCostEntries.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == entryId, cancellationToken)
        ?? throw new NotFoundException("Cost entry not found.");

    private static string NormalizeCategory(string? category) =>
        ProjectCostCategories.All.FirstOrDefault(x => string.Equals(x, category?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new BusinessRuleException("Category must be Materials, Labour, Subcontractor, Equipment or Other.");

    private static void ApplyBudget(ProjectBudgetLine line, UpsertBudgetLineRequest request)
    {
        if (request.Amount < 0)
            throw new BusinessRuleException("A budget amount cannot be negative.");
        line.Category = NormalizeCategory(request.Category);
        line.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        line.Amount = Math.Round(request.Amount, 2);
    }

    private static void ApplyCost(ProjectCostEntry entry, UpsertCostEntryRequest request)
    {
        if (request.Amount <= 0)
            throw new BusinessRuleException("Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new BusinessRuleException("Describe the cost.");
        entry.Category = NormalizeCategory(request.Category);
        entry.Description = request.Description.Trim();
        entry.Amount = Math.Round(request.Amount, 2);
        entry.IncurredOn = request.IncurredOn;
    }

    private async Task<ProjectBillingTermsResponse> MapTermsAsync(Project project, CancellationToken cancellationToken)
    {
        var advanceInvoice = project.AdvanceInvoiceId is { } id
            ? await dbContext.TenantInvoices.Where(x => x.TenantId == TenantId && x.Id == id).Select(x => new { x.InvoiceNumber, x.Status }).SingleOrDefaultAsync(cancellationToken)
            : null;
        var locked = await dbContext.ProjectValuations.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == project.Id
            && (x.Status == ProjectValuationStatuses.Certified || x.Status == ProjectValuationStatuses.Invoiced), cancellationToken);
        return new ProjectBillingTermsResponse(project.Currency, project.RetentionPercent, project.RetentionCapAmount, project.RetentionReleaseAtCompletionPercent,
            project.DefectsLiabilityMonths, project.AdvancePaymentAmount, project.AdvanceRecoveryPercent, project.AdvanceInvoiceId,
            advanceInvoice?.InvoiceNumber, advanceInvoice?.Status, locked);
    }

    private async Task<RetentionResponse> MapRetentionAsync(Project project, CancellationToken cancellationToken)
    {
        var (held, completionDue, finalDue) = await financialsService.RetentionPositionAsync(TenantId, project, cancellationToken);
        var releases = await dbContext.ProjectRetentionReleases.Include(x => x.Invoice)
            .Where(x => x.TenantId == TenantId && x.ProjectId == project.Id)
            .OrderBy(x => x.ReleasedOn)
            .ToListAsync(cancellationToken);
        DateOnly? liabilityEnds = project.HandedOverAt.HasValue
            ? DateOnly.FromDateTime(project.HandedOverAt.Value.AddMonths(project.DefectsLiabilityMonths ?? 0))
            : null;
        return new RetentionResponse(project.Currency, project.RetentionPercent, project.RetentionReleaseAtCompletionPercent, held, completionDue, finalDue,
            project.HandedOverAt, liabilityEnds,
            releases.Select(x => new RetentionReleaseResponse(x.Id, x.Stage, x.Amount, x.ReleasedOn, x.InvoiceId, x.Invoice?.InvoiceNumber, x.Invoice?.Status)).ToList());
    }

    private static WorkerRateResponse MapRate(ProjectWorkerRate x) => new(x.Id, x.EffectiveFrom, x.DailyRate, x.Note, x.SetByUserId);

    private static CostEntryResponse MapCost(ProjectCostEntry x) => new(x.Id, x.Category, x.Description, x.Amount, x.IncurredOn, x.RecordedByUserId);

    private static ValuationResponse MapValuation(ProjectValuation x) => new(
        x.Id, x.ValuationNumber, x.PeriodEnd, x.WorkDoneToDate, x.MaterialsOnSite, x.GrossValueToDate, x.RetentionToDate, x.AdvanceRecoveredToDate,
        x.PreviouslyCertified, x.AmountDue, x.Status, x.CertifiedByConsultantId, x.CertifiedByConsultant?.FirmName, x.CertificateReference, x.CertifiedOn,
        x.CertifiedAmount, x.InvoiceId, x.Invoice?.InvoiceNumber, x.Invoice?.Status, x.Notes, x.CreatedAt);
}

public sealed record ProjectBillingTermsResponse(
    string Currency, decimal? RetentionPercent, decimal? RetentionCapAmount, decimal RetentionReleaseAtCompletionPercent, int? DefectsLiabilityMonths,
    decimal? AdvancePaymentAmount, decimal? AdvanceRecoveryPercent, Guid? AdvanceInvoiceId, string? AdvanceInvoiceNumber, string? AdvanceInvoiceStatus,
    bool CalculationTermsLocked);

public sealed record UpdateBillingTermsRequest(
    decimal? RetentionPercent, decimal? RetentionCapAmount, decimal? RetentionReleaseAtCompletionPercent, int? DefectsLiabilityMonths,
    decimal? AdvancePaymentAmount, decimal? AdvanceRecoveryPercent);

public sealed record WorkerRateResponse(Guid Id, DateOnly EffectiveFrom, decimal DailyRate, string? Note, Guid SetByUserId);

public sealed record WorkerRatesResponse(
    Guid WorkerId, string FullName, string? Phone, string? Trade, string EngagementType, bool IsActive,
    decimal? CurrentRate, decimal ManDays, decimal UncostedDays, decimal LabourCost, IReadOnlyList<WorkerRateResponse> Rates);

public sealed record SetWorkerRateRequest(DateOnly EffectiveFrom, decimal DailyRate, string? Note);

public sealed record WageSheetRow(
    Guid WorkerId, string FullName, string? Phone, string? Trade, string EngagementType, int FullDays, int HalfDays,
    decimal ManDays, decimal? CurrentRate, decimal Amount, decimal UncostedDays);

public sealed record WageSheetResponse(
    string ProjectNumber, string ProjectName, string Currency, DateOnly From, DateOnly To, IReadOnlyList<WageSheetRow> Rows,
    decimal TotalManDays, decimal TotalAmount, int WorkersWithUncostedDays);

public sealed record BudgetLineResponse(Guid Id, string Category, string? Description, decimal Amount);

public sealed record UpsertBudgetLineRequest(string Category, string? Description, decimal Amount);

public sealed record CostEntryResponse(Guid Id, string Category, string Description, decimal Amount, DateOnly IncurredOn, Guid RecordedByUserId);

public sealed record UpsertCostEntryRequest(string Category, string Description, decimal Amount, DateOnly IncurredOn);

public sealed record ValuationResponse(
    Guid Id, string ValuationNumber, DateOnly PeriodEnd, decimal WorkDoneToDate, decimal MaterialsOnSite, decimal GrossValueToDate,
    decimal RetentionToDate, decimal AdvanceRecoveredToDate, decimal PreviouslyCertified, decimal AmountDue, string Status,
    Guid? CertifiedByConsultantId, string? CertifiedByConsultantName, string? CertificateReference, DateOnly? CertifiedOn, decimal? CertifiedAmount,
    Guid? InvoiceId, string? InvoiceNumber, string? InvoiceStatus, string? Notes, DateTime CreatedAt);

public sealed record ValuationPreviewResponse(
    decimal SuggestedWorkDoneToDate, decimal WorkDoneToDate, decimal MaterialsOnSite, decimal GrossValueToDate, decimal RetentionToDate,
    decimal AdvanceRecoveredToDate, decimal PreviouslyCertified, decimal AmountDue, decimal PreviousWorkDoneToDate);

public sealed record UpsertValuationRequest(DateOnly PeriodEnd, decimal WorkDoneToDate, decimal MaterialsOnSite, string? Notes);

public sealed record CertifyValuationRequest(Guid? ConsultantId, string CertificateReference, DateOnly CertifiedOn, decimal CertifiedAmount);

public sealed record VoidValuationRequest(string? Reason);

public sealed record ReleaseRetentionRequest(string? Stage);

public sealed record RetentionReleaseResponse(Guid Id, string Stage, decimal Amount, DateOnly ReleasedOn, Guid? InvoiceId, string? InvoiceNumber, string? InvoiceStatus);

public sealed record RetentionResponse(
    string Currency, decimal? RetentionPercent, decimal RetentionReleaseAtCompletionPercent, decimal Held, decimal CompletionDue, decimal FinalDue,
    DateTime? HandedOverAt, DateOnly? DefectsLiabilityEnds, IReadOnlyList<RetentionReleaseResponse> Releases);
