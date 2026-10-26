using System.Data;
using System.Data.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public static class DocumentTypes
{
    public const string TenantCode = "TenantCode";
    public const string WorkOrder = "WorkOrder";
    public const string PreventiveMaintenance = "PreventiveMaintenance";
    public const string MaterialRequest = "MaterialRequest";
    public const string MaterialIssue = "MaterialIssue";
    public const string Asset = "Asset";
    public const string Site = "Site";
    public const string StockTransfer = "StockTransfer";
    public const string Contract = "Contract";
    public const string Quotation = "Quotation";
    public const string ProformaInvoice = "ProformaInvoice";
    public const string Invoice = "Invoice";
    public const string CreditNote = "CreditNote";
    public const string DebitNote = "DebitNote";
    public const string Receipt = "Receipt";
    public const string Payment = "Payment";
    public const string Expense = "Expense";
    public const string PurchaseOrder = "PurchaseOrder";
    public const string SupplierBill = "SupplierBill";
    public const string ServiceReport = "ServiceReport";
    public const string PartsReturn = "PartsReturn";
    public const string StockCount = "StockCount";
    public const string Project = "Project";
    public const string ProjectVariation = "ProjectVariation";
    public const string ProjectValuation = "ProjectValuation";

    public static string Normalize(string documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            throw new Ecosys.Shared.Errors.BusinessRuleException("Document type is required.");
        var v = documentType.Trim();
        return v.ToLowerInvariant() switch
        {
            var x when x == TenantCode.ToLowerInvariant() => TenantCode,
            var x when x == WorkOrder.ToLowerInvariant() => WorkOrder,
            var x when x == PreventiveMaintenance.ToLowerInvariant() => PreventiveMaintenance,
            var x when x == MaterialRequest.ToLowerInvariant() => MaterialRequest,
            var x when x == MaterialIssue.ToLowerInvariant() => MaterialIssue,
            var x when x == Asset.ToLowerInvariant() => Asset,
            var x when x == Site.ToLowerInvariant() => Site,
            var x when x == StockTransfer.ToLowerInvariant() => StockTransfer,
            var x when x == Contract.ToLowerInvariant() => Contract,
            var x when x == Quotation.ToLowerInvariant() => Quotation,
            var x when x == ProformaInvoice.ToLowerInvariant() => ProformaInvoice,
            var x when x == Invoice.ToLowerInvariant() => Invoice,
            var x when x == CreditNote.ToLowerInvariant() => CreditNote,
            var x when x == DebitNote.ToLowerInvariant() => DebitNote,
            var x when x == Receipt.ToLowerInvariant() => Receipt,
            var x when x == Payment.ToLowerInvariant() => Payment,
            var x when x == Expense.ToLowerInvariant() => Expense,
            var x when x == PurchaseOrder.ToLowerInvariant() => PurchaseOrder,
            var x when x == SupplierBill.ToLowerInvariant() => SupplierBill,
            var x when x == ServiceReport.ToLowerInvariant() => ServiceReport,
            var x when x == PartsReturn.ToLowerInvariant() => PartsReturn,
            var x when x == StockCount.ToLowerInvariant() => StockCount,
            var x when x == Project.ToLowerInvariant() => Project,
            var x when x == ProjectVariation.ToLowerInvariant() => ProjectVariation,
            var x when x == ProjectValuation.ToLowerInvariant() => ProjectValuation,
            _ => throw new Ecosys.Shared.Errors.BusinessRuleException("Unsupported document type.")
        };
    }
}

public static class NumberResetFrequencies
{
    public const string Never = "Never";
    public const string Yearly = "Yearly";
    public const string Monthly = "Monthly";

    public static readonly string[] All =
    [
        Never,
        Yearly,
        Monthly
    ];
}

public interface IDocumentNumberingService
{
    Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default);
    Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false);
}

/// <summary>
/// Raised when a document number could not be reserved because concurrent requests kept colliding
/// on the numbering row. Maps to HTTP 409 with the same <c>CONCURRENCY_CONFLICT</c> error code the
/// global handler already uses for <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>,
/// so clients see one consistent "retry this request" signal instead of an opaque 500 (EC-017).
/// </summary>
public sealed class DocumentNumberContentionException(string message)
    : AppException(message), ILocalizedErrorCode
{
    public override int StatusCode => 409;
    public string ErrorCode => "CONCURRENCY_CONFLICT";
}

internal sealed class DocumentNumberingService(AppDbContext dbContext) : IDocumentNumberingService
{
    /// <summary>PostgreSQL <c>serialization_failure</c>.</summary>
    private const string SerializationFailure = "40001";

    /// <summary>PostgreSQL <c>deadlock_detected</c>.</summary>
    private const string DeadlockDetected = "40P01";

    private const int MaxGenerateAttempts = 5;

    /// <summary>
    /// Reserves the next document number. The reservation runs in a serializable transaction, so
    /// concurrent requests for the same counter can legitimately fail with SQLSTATE 40001; that is
    /// PostgreSQL asking the loser to try again, not a server fault. Retry a bounded number of times
    /// with a short randomised backoff and, only if the contention really does not clear, surface a
    /// 409 the client can act on (EC-017).
    /// </summary>
    public async Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default)
    {
        // A retry re-runs SaveChanges, which flushes everything tracked by this (request-scoped)
        // context — not just the counter. A serialization failure raised at COMMIT time leaves those
        // other changes accepted in memory but rolled back in the database, so replaying would
        // silently drop them. When the caller already has unrelated pending work — or owns an ambient
        // transaction, which a serialization failure has already aborted — take the single attempt
        // and translate a failure into 409 instead of retrying blind.
        var canRetry = !HasPendingChangesOutsideNumbering()
            && (!dbContext.Database.IsRelational() || dbContext.Database.CurrentTransaction is null);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await GenerateOnceAsync(tenantId, branchId, documentType, cancellationToken);
            }
            catch (Exception ex) when (IsConcurrencyConflict(ex))
            {
                // The transaction rolled back, so nothing of this attempt has been applied.
                DetachNumberingState();

                if (!canRetry || attempt >= MaxGenerateAttempts)
                {
                    throw new DocumentNumberContentionException(
                        "Another user reserved a document number at the same moment. Nothing was saved — please submit again.");
                }

                // Short randomised backoff so colliding requests do not line up again immediately.
                await Task.Delay(Random.Shared.Next(10, 40) * attempt, cancellationToken);
            }
        }
    }

    private async Task<string> GenerateOnceAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken)
    {
        // Providers without transaction support (the in-memory provider used by tests) cannot begin
        // one; there is no concurrency to serialise there either.
        var useTransaction = dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null;
        var transaction = useTransaction
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        try
        {
            var setting = await FindSettingAsync(tenantId, branchId, documentType, cancellationToken)
                ?? await CreateDefaultSettingAsync(tenantId, branchId, documentType, cancellationToken);

            if (dbContext.Entry(setting).State == EntityState.Detached)
            {
                dbContext.NumberingSettings.Add(setting);
            }

            ResetIfNeeded(setting);

            var nextNumber = setting.NextNumber;
            setting.NextNumber += 1;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Format(setting, branchId, nextNumber);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// True when the exception chain carries a PostgreSQL serialization failure or deadlock — the
    /// two SQLSTATEs that mean "this transaction lost a race and may simply be retried".
    /// </summary>
    private static bool IsConcurrencyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException dbException
                && dbException.SqlState is SerializationFailure or DeadlockDetected)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPendingChangesOutsideNumbering() =>
        dbContext.ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && entry.Entity is not NumberingSetting);

    /// <summary>
    /// Drops the counter rows this attempt touched so the retry re-reads the committed values
    /// instead of replaying the already-incremented (and now rolled back) in-memory state.
    /// </summary>
    private void DetachNumberingState()
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<NumberingSetting>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task<NumberingSetting> UpsertAsync(
        Guid tenantId,
        Guid? branchId,
        string documentType,
        string prefix,
        long nextNumber,
        int paddingLength,
        string resetFrequency,
        bool includeYear,
        bool includeMonth,
        bool isActive,
        CancellationToken cancellationToken = default,
        string? suffix = null,
        string yearFormat = "YYYY",
        string separator = "-",
        bool isLocked = false)
    {
        Validate(documentType, prefix, nextNumber, paddingLength, resetFrequency);

        var normalizedDocumentType = NormalizeDocumentType(documentType);
        var normalizedResetFrequency = NormalizeResetFrequency(resetFrequency);
        var normalizedPrefix = prefix.Trim().ToUpperInvariant();

        var setting = await dbContext.NumberingSettings.SingleOrDefaultAsync(
            x => x.TenantId == tenantId
                && x.BranchId == branchId
                && x.DocumentType == normalizedDocumentType,
            cancellationToken);

        if (setting is null)
        {
            setting = new NumberingSetting
            {
                TenantId = tenantId,
                BranchId = branchId,
                DocumentType = normalizedDocumentType
            };
            dbContext.NumberingSettings.Add(setting);
        }

        setting.Prefix = normalizedPrefix;
        setting.Suffix = string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim().ToUpperInvariant();
        setting.NextNumber = nextNumber;
        setting.PaddingLength = paddingLength;
        setting.ResetFrequency = normalizedResetFrequency;
        setting.IncludeYear = includeYear;
        setting.YearFormat = string.IsNullOrWhiteSpace(yearFormat) ? "YYYY" : yearFormat.Trim().ToUpperInvariant();
        setting.IncludeMonth = includeMonth;
        setting.Separator = string.IsNullOrEmpty(separator) ? "-" : separator;
        setting.IsLocked = isLocked;
        setting.IsActive = isActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private async Task<NumberingSetting?> FindSettingAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken)
    {
        var normalizedDocumentType = NormalizeDocumentType(documentType);

        if (branchId.HasValue)
        {
            var branchSetting = await dbContext.NumberingSettings.SingleOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.BranchId == branchId
                    && x.DocumentType == normalizedDocumentType
                    && x.IsActive,
                cancellationToken);

            if (branchSetting is not null)
            {
                return branchSetting;
            }
        }

        return await dbContext.NumberingSettings.SingleOrDefaultAsync(
            x => x.TenantId == tenantId
                && x.BranchId == null
                && x.DocumentType == normalizedDocumentType
                && x.IsActive,
            cancellationToken);
    }

    private async Task<NumberingSetting> CreateDefaultSettingAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken)
    {
        var prefix = DefaultPrefix(documentType);
        if (branchId.HasValue)
        {
            var branchCode = await dbContext.Branches
                .Where(x => x.TenantId == tenantId && x.Id == branchId.Value)
                .Select(x => x.Code)
                .SingleOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(branchCode))
            {
                prefix = $"{prefix}-{branchCode.Trim().ToUpperInvariant()}";
            }
        }

        return new NumberingSetting
        {
            TenantId = tenantId,
            BranchId = branchId,
            DocumentType = NormalizeDocumentType(documentType),
            Prefix = prefix,
            NextNumber = 1,
            PaddingLength = 6,
            ResetFrequency = NumberResetFrequencies.Never,
            IncludeYear = false,
            IncludeMonth = false,
            IsActive = true
        };
    }

    private static void ResetIfNeeded(NumberingSetting setting)
    {
        var now = DateTime.UtcNow;
        var lastReset = setting.LastResetAt;
        var shouldReset = setting.ResetFrequency switch
        {
            NumberResetFrequencies.Monthly => !lastReset.HasValue || lastReset.Value.Year != now.Year || lastReset.Value.Month != now.Month,
            NumberResetFrequencies.Yearly => !lastReset.HasValue || lastReset.Value.Year != now.Year,
            _ => false
        };

        if (shouldReset)
        {
            setting.NextNumber = 1;
            setting.LastResetAt = now;
        }
        else if (!setting.LastResetAt.HasValue)
        {
            setting.LastResetAt = now;
        }
    }

    private static string Format(NumberingSetting setting, Guid? requestedBranchId, long number)
    {
        var now = DateTime.UtcNow;
        var sep = string.IsNullOrEmpty(setting.Separator) ? "-" : setting.Separator;
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(setting.Prefix))
            parts.Add(setting.Prefix.Trim().ToUpperInvariant());

        if (setting.IncludeYear)
            parts.Add(string.Equals(setting.YearFormat, "YY", StringComparison.OrdinalIgnoreCase)
                ? now.Year.ToString()[2..]
                : now.Year.ToString("0000"));

        if (setting.IncludeMonth)
            parts.Add(now.Month.ToString("00"));

        parts.Add(number.ToString().PadLeft(setting.PaddingLength, '0'));

        if (!string.IsNullOrWhiteSpace(setting.Suffix))
            parts.Add(setting.Suffix.Trim().ToUpperInvariant());

        return string.Join(sep, parts);
    }

    private static void Validate(string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency)
    {
        if (string.IsNullOrWhiteSpace(documentType))
        {
            throw new BusinessRuleException("Document type is required.");
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new BusinessRuleException("Prefix is required.");
        }

        if (nextNumber < 1)
        {
            throw new BusinessRuleException("Next number must be greater than zero.");
        }

        if (paddingLength < 1 || paddingLength > 12)
        {
            throw new BusinessRuleException("Padding length must be between 1 and 12.");
        }

        NormalizeResetFrequency(resetFrequency);
    }

    public static string NormalizeDocumentType(string documentType) => DocumentTypes.Normalize(documentType);

    public static string NormalizeResetFrequency(string resetFrequency)
    {
        var normalized = string.IsNullOrWhiteSpace(resetFrequency) ? NumberResetFrequencies.Never : resetFrequency.Trim();
        return NumberResetFrequencies.All.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? NumberResetFrequencies.All.Single(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase))
            : throw new BusinessRuleException("Reset frequency must be Never, Yearly, or Monthly.");
    }

    private static string DefaultPrefix(string documentType) =>
        NormalizeDocumentType(documentType) switch
        {
            DocumentTypes.TenantCode => "TEN",
            DocumentTypes.WorkOrder => "WO",
            DocumentTypes.PreventiveMaintenance => "PM",
            DocumentTypes.MaterialRequest => "MR",
            DocumentTypes.MaterialIssue => "MI",
            DocumentTypes.Asset => "ASSET",
            DocumentTypes.Site => "SITE",
            DocumentTypes.StockTransfer => "ST",
            DocumentTypes.Contract => "CON",
            DocumentTypes.Quotation => "QT",
            DocumentTypes.ProformaInvoice => "PI",
            DocumentTypes.Invoice => "INV",
            DocumentTypes.CreditNote => "CN",
            DocumentTypes.DebitNote => "DN",
            DocumentTypes.Receipt => "REC",
            DocumentTypes.Payment => "PAY",
            DocumentTypes.Expense => "EXP",
            DocumentTypes.PurchaseOrder => "PO",
            DocumentTypes.SupplierBill => "BILL",
            DocumentTypes.ServiceReport => "SR",
            DocumentTypes.PartsReturn => "RTN",
            DocumentTypes.StockCount => "SC",
            DocumentTypes.Project => "PRJ",
            DocumentTypes.ProjectVariation => "VO",
            DocumentTypes.ProjectValuation => "VAL",
            _ => "DOC"
        };
}
