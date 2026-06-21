using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public interface IPmWorkOrderChecklistService
{
    Task<PmTemplate> GetActiveTemplateAsync(Guid tenantId, Guid pmTemplateId, CancellationToken cancellationToken = default);
    Task<int> AttachPmTemplateToWorkOrderAsync(Guid workOrderId, Guid pmTemplateId, Guid tenantId, Guid? actorUserId = null, bool replaceExisting = false, CancellationToken cancellationToken = default);
    Task<WorkOrderChecklistItem> UpdateChecklistItemAsync(Guid workOrderId, Guid itemId, Guid tenantId, Guid actorUserId, string? responseValue, string? remarks, bool isCompleted, CancellationToken cancellationToken = default);
    Task EnsureRequiredChecklistCompletedAsync(Guid workOrderId, Guid tenantId, CancellationToken cancellationToken = default);
}

internal sealed class PmWorkOrderChecklistService(
    AppDbContext dbContext,
    IAuditLogService auditLogService) : IPmWorkOrderChecklistService
{
    private static readonly HashSet<string> AllowedInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "checkbox",
        "text",
        "multilinetext",
        "number",
        "reading",
        "date",
        "dropdown",
        "photo",
        "signature",
        "yesno",
        "passfail",
        "boolean",
        "condition"
    };

    public async Task<PmTemplate> GetActiveTemplateAsync(Guid tenantId, Guid pmTemplateId, CancellationToken cancellationToken = default) =>
        await dbContext.PmTemplates
            .Include(x => x.Sections)
            .Include(x => x.Questions)
            .ThenInclude(x => x.Section)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == pmTemplateId && x.IsActive, cancellationToken)
        ?? throw new NotFoundException("PM template was not found.");

    public async Task<int> AttachPmTemplateToWorkOrderAsync(
        Guid workOrderId,
        Guid pmTemplateId,
        Guid tenantId,
        Guid? actorUserId = null,
        bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .Include(x => x.ChecklistItems)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var template = await GetActiveTemplateAsync(tenantId, pmTemplateId, cancellationToken);
        var existingItems = workOrder.ChecklistItems
            .Where(x => x.TenantId == tenantId)
            .ToList();

        if (existingItems.Count > 0 && !replaceExisting)
        {
            return existingItems.Count;
        }

        if (existingItems.Count > 0)
        {
            dbContext.WorkOrderChecklistItems.RemoveRange(existingItems);
        }

        var checklistItems = template.Questions
            .OrderBy(x => x.Section?.DisplayOrder ?? int.MaxValue)
            .ThenBy(x => x.DisplayOrder)
            .Select(question => new WorkOrderChecklistItem
            {
                TenantId = tenantId,
                WorkOrderId = workOrderId,
                PmTemplateQuestionId = question.Id,
                SectionName = Normalize(question.SectionName),
                SectionOrder = question.Section?.DisplayOrder ?? 0,
                QuestionText = question.Prompt.Trim(),
                HelperText = Normalize(question.HelperText),
                InputType = NormalizeInputType(question.ResponseType),
                QuestionType = NormalizeInputType(question.ResponseType),
                Unit = Normalize(question.Unit),
                DefaultValue = Normalize(question.DefaultValue),
                MinValue = question.MinValue,
                MaxValue = question.MaxValue,
                IsRequired = question.IsRequired,
                EvidenceRequired = question.EvidenceRequired,
                TechnicianRemarksAllowed = question.TechnicianRemarksAllowed,
                RequiresNoteOnFail = question.RequiresNoteOnFail,
                SortOrder = question.DisplayOrder,
                ResponseValue = Normalize(question.DefaultValue),
                Remarks = null,
                IsCompleted = false,
                CompletedByUserId = null,
                CompletedAt = null,
                OptionsJson = Normalize(question.OptionsJson)
            })
            .ToList();

        dbContext.WorkOrderChecklistItems.AddRange(checklistItems);
        workOrder.PmTemplateId = template.Id;
        workOrder.PmTemplateNameSnapshot = template.Name;
        workOrder.PmTemplateVersionSnapshot = template.Version;
        workOrder.IsPreventiveMaintenance = string.Equals(template.WorkType, "PreventiveMaintenance", StringComparison.OrdinalIgnoreCase);
        workOrder.ServiceType = template.WorkType;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(
            tenantId,
            actorUserId,
            "PM template attached to work order",
            nameof(WorkOrder),
            workOrderId.ToString(),
            $"Attached template '{template.Name}' with {checklistItems.Count} checklist items.",
            cancellationToken);

        return checklistItems.Count;
    }

    public async Task<WorkOrderChecklistItem> UpdateChecklistItemAsync(
        Guid workOrderId,
        Guid itemId,
        Guid tenantId,
        Guid actorUserId,
        string? responseValue,
        string? remarks,
        bool isCompleted,
        CancellationToken cancellationToken = default)
    {
        var checklistItem = await dbContext.WorkOrderChecklistItems
            .Include(x => x.WorkOrder)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId && x.Id == itemId, cancellationToken)
            ?? throw new NotFoundException("Checklist item was not found.");

        var normalizedResponse = Normalize(responseValue);
        ValidateResponse(checklistItem, normalizedResponse, isCompleted);

        checklistItem.ResponseValue = normalizedResponse;
        checklistItem.Remarks = checklistItem.TechnicianRemarksAllowed ? Normalize(remarks) : null;
        checklistItem.IsCompleted = isCompleted;
        checklistItem.CompletedByUserId = isCompleted ? actorUserId : null;
        checklistItem.CompletedAt = isCompleted ? DateTime.UtcNow : null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(
            tenantId,
            actorUserId,
            "PM checklist item updated",
            nameof(WorkOrder),
            workOrderId.ToString(),
            $"Updated checklist item '{checklistItem.QuestionText}'.",
            cancellationToken);

        return checklistItem;
    }

    public async Task EnsureRequiredChecklistCompletedAsync(Guid workOrderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = await dbContext.WorkOrderChecklistItems
            .Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.SectionOrder)
            .ThenBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var signatureTypes = await dbContext.WorkOrderSignatures
            .Where(x => x.TenantId == tenantId && x.WorkOrderId == workOrderId)
            .Select(x => x.SignatureType)
            .ToListAsync(cancellationToken);

        var incomplete = items.FirstOrDefault(item => item.IsRequired && !IsChecklistItemSatisfied(item, signatureTypes));
        if (incomplete is not null)
        {
            throw new BusinessRuleException($"Complete required checklist item '{incomplete.QuestionText}' before closing this work order.");
        }

        var requiredEvidenceMissing = items.FirstOrDefault(
            x => (x.EvidenceRequired || string.Equals(x.InputType, "photo", StringComparison.OrdinalIgnoreCase))
                && x.AttachmentId == null);

        if (requiredEvidenceMissing is not null)
        {
            throw new BusinessRuleException("Attach required checklist evidence before closing this work order.");
        }
    }

    private static void ValidateResponse(WorkOrderChecklistItem item, string? responseValue, bool isCompleted)
    {
        if (!isCompleted)
        {
            return;
        }

        if (item.IsRequired
            && !string.Equals(item.InputType, "photo", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.InputType, "signature", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(responseValue))
        {
            throw new BusinessRuleException("A required checklist response cannot be empty.");
        }

        if (string.Equals(item.InputType, "date", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(responseValue)
            && !DateOnly.TryParse(responseValue, out _)
            && !DateTime.TryParse(responseValue, out _))
        {
            throw new BusinessRuleException("Date checklist responses must be valid dates.");
        }

        if ((!string.Equals(item.InputType, "number", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.InputType, "reading", StringComparison.OrdinalIgnoreCase))
            || string.IsNullOrWhiteSpace(responseValue))
        {
            item.NumberValue = null;
            return;
        }

        if (!decimal.TryParse(responseValue, out var numberValue))
        {
            throw new BusinessRuleException("Numeric checklist responses must be valid numbers.");
        }

        if (item.MinValue.HasValue && numberValue < item.MinValue.Value)
        {
            throw new BusinessRuleException($"Checklist response must be at least {item.MinValue.Value}.");
        }

        if (item.MaxValue.HasValue && numberValue > item.MaxValue.Value)
        {
            throw new BusinessRuleException($"Checklist response must be at most {item.MaxValue.Value}.");
        }

        item.NumberValue = numberValue;
    }

    private static string NormalizeInputType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (AllowedInputTypes.Contains(normalized))
        {
            return normalized;
        }

        throw new BusinessRuleException("Unsupported PM checklist input type.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsChecklistItemSatisfied(WorkOrderChecklistItem item, IReadOnlyCollection<string> signatureTypes)
    {
        if (!item.IsCompleted)
        {
            return false;
        }

        if (string.Equals(item.InputType, "photo", StringComparison.OrdinalIgnoreCase))
        {
            return item.AttachmentId.HasValue;
        }

        if (string.Equals(item.InputType, "signature", StringComparison.OrdinalIgnoreCase))
        {
            if (item.QuestionText.Contains("client", StringComparison.OrdinalIgnoreCase))
            {
                return signatureTypes.Contains("Client", StringComparer.OrdinalIgnoreCase);
            }

            if (item.QuestionText.Contains("technician", StringComparison.OrdinalIgnoreCase))
            {
                return signatureTypes.Contains("Technician", StringComparer.OrdinalIgnoreCase);
            }

            return signatureTypes.Count > 0;
        }

        return !string.IsNullOrWhiteSpace(item.ResponseValue);
    }
}
