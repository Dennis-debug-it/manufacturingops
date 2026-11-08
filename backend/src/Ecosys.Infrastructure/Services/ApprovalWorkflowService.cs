using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public sealed record ApprovalStartResult(Guid RequestId, bool RequiresApproval);

public interface IApprovalWorkflowService
{
    /// <summary>
    /// Starts an approval workflow for a document if an active matrix exists for its type.
    /// Returns null when no matrix is configured — callers must then keep their existing behavior.
    /// </summary>
    Task<ApprovalStartResult?> StartAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        string documentNumber,
        decimal amount,
        string currency,
        Guid initiatedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Records a decision on the current level of a request. Advances the chain, or
    /// finalizes and applies the outcome to the underlying document.</summary>
    Task DecideAsync(Guid tenantId, Guid requestId, bool approve, string? note, CancellationToken cancellationToken = default);

    Task<ApprovalRequest?> GetActiveForDocumentAsync(Guid tenantId, string documentType, Guid documentId, CancellationToken cancellationToken = default);
}

internal sealed class ApprovalWorkflowService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService) : IApprovalWorkflowService
{
    public async Task<ApprovalStartResult?> StartAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        string documentNumber,
        decimal amount,
        string currency,
        Guid initiatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var matrix = await dbContext.ApprovalMatrices
            .Include(x => x.Levels)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.DocumentType == documentType && x.IsActive, cancellationToken);

        if (matrix is null || matrix.Levels.Count == 0)
        {
            return null;
        }

        // The chain runs from level 1 up to and including the highest level whose amount
        // band contains this amount. Levels above the matched band do not participate.
        var orderedLevels = matrix.Levels.OrderBy(x => x.LevelOrder).ToList();
        var matchedLevel = orderedLevels
            .Where(x => amount >= x.MinAmount && (!x.MaxAmount.HasValue || amount < x.MaxAmount.Value))
            .OrderByDescending(x => x.LevelOrder)
            .FirstOrDefault();

        if (matchedLevel is null)
        {
            // Amount falls outside every configured band — nothing to approve.
            return null;
        }

        var chain = orderedLevels.Where(x => x.LevelOrder <= matchedLevel.LevelOrder).ToList();
        var now = DateTime.UtcNow;

        var request = new ApprovalRequest
        {
            TenantId = tenantId,
            ApprovalMatrixId = matrix.Id,
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNumber = documentNumber,
            Amount = amount,
            Currency = string.IsNullOrWhiteSpace(currency) ? matrix.Currency : currency,
            Status = ApprovalStatuses.Pending,
            CurrentLevelOrder = chain[0].LevelOrder,
            InitiatedByUserId = initiatedByUserId,
            Steps = chain.Select(level => new ApprovalRequestStep
            {
                TenantId = tenantId,
                LevelOrder = level.LevelOrder,
                LevelName = level.Name,
                ApproverType = level.ApproverType,
                RequiredPermission = level.RequiredPermission,
                ApproverUserIdsCsv = level.ApproverUserIdsCsv,
                Status = ApprovalStatuses.Pending,
                DueAt = level.ReminderAfterHours.HasValue ? now.AddHours(level.ReminderAfterHours.Value) : null
            }).ToList()
        };

        dbContext.ApprovalRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            tenantId, initiatedByUserId, "approval.request.started", nameof(ApprovalRequest), request.Id.ToString(),
            $"Approval started for {documentType} {documentNumber} ({request.Currency} {amount:N2}) — {chain.Count} level(s).",
            cancellationToken);

        return new ApprovalStartResult(request.Id, true);
    }

    public async Task DecideAsync(Guid tenantId, Guid requestId, bool approve, string? note, CancellationToken cancellationToken = default)
    {
        var request = await dbContext.ApprovalRequests
            .Include(x => x.Steps)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == requestId, cancellationToken)
            ?? throw new NotFoundException("Approval request was not found.");

        if (request.Status != ApprovalStatuses.Pending)
        {
            throw new BusinessRuleException($"This approval request is already {request.Status.ToLowerInvariant()}.");
        }

        var step = request.Steps.SingleOrDefault(x => x.LevelOrder == request.CurrentLevelOrder && x.Status == ApprovalStatuses.Pending)
            ?? throw new BusinessRuleException("No pending approval step was found for this request.");

        EnsureCanDecide(step);

        var userId = tenantContext.GetRequiredUserId();
        var approverName = await dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        var now = DateTime.UtcNow;
        step.DecidedByUserId = userId;
        step.DecidedByName = approverName;
        step.DecidedAt = now;
        step.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (!approve)
        {
            step.Status = ApprovalStatuses.Rejected;
            request.Status = ApprovalStatuses.Rejected;
            request.CompletedAt = now;
            await ApplyOutcomeAsync(request, approved: false, approverName, cancellationToken);

            await auditLogService.LogAsync(
                tenantId, userId, "approval.request.rejected", nameof(ApprovalRequest), request.Id.ToString(),
                $"{request.DocumentType} {request.DocumentNumber} rejected at level {step.LevelOrder} by {approverName}.",
                severity: "Warning", cancellationToken: cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        step.Status = ApprovalStatuses.Approved;

        var nextStep = request.Steps
            .Where(x => x.LevelOrder > step.LevelOrder && x.Status == ApprovalStatuses.Pending)
            .OrderBy(x => x.LevelOrder)
            .FirstOrDefault();

        if (nextStep is not null)
        {
            request.CurrentLevelOrder = nextStep.LevelOrder;
            await auditLogService.LogAsync(
                tenantId, userId, "approval.request.level-approved", nameof(ApprovalRequest), request.Id.ToString(),
                $"{request.DocumentType} {request.DocumentNumber} approved at level {step.LevelOrder} by {approverName}; awaiting level {nextStep.LevelOrder}.",
                cancellationToken);
        }
        else
        {
            request.Status = ApprovalStatuses.Approved;
            request.CompletedAt = now;
            await ApplyOutcomeAsync(request, approved: true, approverName, cancellationToken);

            await auditLogService.LogAsync(
                tenantId, userId, "approval.request.approved", nameof(ApprovalRequest), request.Id.ToString(),
                $"{request.DocumentType} {request.DocumentNumber} fully approved by {approverName}.",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ApprovalRequest?> GetActiveForDocumentAsync(Guid tenantId, string documentType, Guid documentId, CancellationToken cancellationToken = default) =>
        dbContext.ApprovalRequests
            .Include(x => x.Steps)
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.DocumentType == documentType && x.DocumentId == documentId && x.Status == ApprovalStatuses.Pending,
                cancellationToken);

    private void EnsureCanDecide(ApprovalRequestStep step)
    {
        if (tenantContext.IsAdmin)
        {
            return;
        }

        if (string.Equals(step.ApproverType, ApprovalApproverTypes.SpecificUsers, StringComparison.OrdinalIgnoreCase))
        {
            var userId = tenantContext.GetRequiredUserId();
            var allowed = (step.ApproverUserIdsCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
                .Contains(userId);
            if (!allowed)
            {
                throw new ForbiddenException("You are not an assigned approver for this step.");
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(step.RequiredPermission) && tenantContext.HasPermission(step.RequiredPermission))
        {
            return;
        }

        throw new ForbiddenException("You do not have permission to approve at this level.");
    }

    /// <summary>Applies the finalized outcome to the underlying document. Kept in one place
    /// so every supported document type resolves consistently.</summary>
    private async Task ApplyOutcomeAsync(ApprovalRequest request, bool approved, string approverName, CancellationToken cancellationToken)
    {
        switch (request.DocumentType)
        {
            case ApprovalDocumentTypes.PurchaseOrder:
            {
                // A PO stays Draft on approval (now cleared to be Sent); a rejection cancels it.
                if (!approved)
                {
                    var order = await dbContext.PurchaseOrders
                        .SingleOrDefaultAsync(x => x.TenantId == request.TenantId && x.Id == request.DocumentId, cancellationToken);
                    if (order is not null && order.Status is "Draft" or "PendingApproval")
                    {
                        order.Status = "Cancelled";
                        order.UpdatedAt = DateTime.UtcNow;
                    }
                }
                break;
            }
        }
    }
}
