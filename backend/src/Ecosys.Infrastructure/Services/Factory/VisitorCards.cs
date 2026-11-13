using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record NewCardBatch(Guid FactoryId, string Prefix, int From, int To, string Kind, string? Colour);

public interface IVisitorCardService
{
    /// <summary>Adds numbered cards to a gate's stock (e.g. V-001 to V-050); numbers already in use are skipped. Returns how many were added.</summary>
    Task<int> AddBatchAsync(Guid tenantId, NewCardBatch batch, CancellationToken cancellationToken = default);

    /// <summary>Hands a card to someone on a visit. The card must be in the box at the visit's factory.</summary>
    Task<VisitorCard> IssueAsync(Guid tenantId, Guid attendeeId, string cardCode, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>Takes a card back from someone still inside (a swap, or it was handed over early).</summary>
    Task<VisitorCard> ReturnAsync(Guid tenantId, Guid attendeeId, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>Retires a card, marks it missing, or puts a found card back in the box.</summary>
    Task<VisitorCard> SetStatusAsync(Guid tenantId, Guid cardId, string status, string? notes, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>Registers (or clears) the serial of the card's RFID/NFC chip, read by tapping it.</summary>
    Task<VisitorCard> SetChipAsync(Guid tenantId, Guid cardId, string? serial, CancellationToken cancellationToken = default);

    /// <summary>A card from what was scanned, tapped or typed: its QR ("CARD:…"), chip serial or printed number.</summary>
    Task<VisitorCard?> FindAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);
}

internal sealed class VisitorCardService(AppDbContext dbContext) : IVisitorCardService
{
    public const int MaxBatch = 500;

    public async Task<int> AddBatchAsync(Guid tenantId, NewCardBatch batch, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == tenantId && x.Id == batch.FactoryId && x.Level == OrgNodeLevels.Factory && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Choose the factory gate the cards are for.");
        var prefix = (batch.Prefix ?? "").Trim().ToUpperInvariant();
        if (prefix.Length > 10 || prefix.Any(c => !char.IsLetterOrDigit(c) && c != '-')) throw new BusinessRuleException("Use a short prefix of letters, digits or a dash, e.g. V- or MUH-V.");
        if (batch.From < 1 || batch.To < batch.From) throw new BusinessRuleException("Give the first and last card numbers, e.g. 1 to 50.");
        if (batch.To - batch.From + 1 > MaxBatch) throw new BusinessRuleException($"Add at most {MaxBatch} cards at a time.");
        var kind = VisitorCardKinds.Find(batch.Kind) ?? throw new BusinessRuleException($"Card type must be one of: {string.Join(", ", VisitorCardKinds.All)}.");
        var width = Math.Max(3, batch.To.ToString().Length);
        var numbers = Enumerable.Range(batch.From, batch.To - batch.From + 1).Select(n => $"{prefix}{n.ToString().PadLeft(width, '0')}").ToList();
        var existing = (await dbContext.VisitorCards.Where(x => x.TenantId == tenantId && numbers.Contains(x.CardNumber)).Select(x => x.CardNumber).ToListAsync(cancellationToken)).ToHashSet();
        var added = 0;
        foreach (var number in numbers.Where(x => !existing.Contains(x)))
        {
            dbContext.VisitorCards.Add(new VisitorCard
            {
                TenantId = tenantId, FactoryId = batch.FactoryId, CardNumber = number, Kind = kind, Colour = VisitorService.Trim(batch.Colour, 50),
                Token = VisitorService.NewBadgeToken(),
            });
            added++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return added;
    }

    public async Task<VisitorCard> IssueAsync(Guid tenantId, Guid attendeeId, string cardCode, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var attendee = await dbContext.VisitAttendees.Include(x => x.Visit).Include(x => x.Visitor)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == attendeeId, cancellationToken)
            ?? throw new NotFoundException("Visitor not found on any visit.");
        if (attendee.CheckedOutAt is not null) throw new BusinessRuleException($"{attendee.Visitor!.FullName} has left.");
        if (attendee.CardId is not null && attendee.CardReturnedAt is null && !attendee.CardNotReturned)
            throw new BusinessRuleException($"{attendee.Visitor!.FullName} already has card {attendee.CardNumber}; take it back first.");
        var card = await FindAsync(tenantId, cardCode, cancellationToken) ?? throw new BusinessRuleException("That is not one of our cards.");
        EnsureCanIssue(card, attendee.Visit!.FactoryId);

        var now = OccurredAt.UtcNow;
        card.Status = VisitorCardStatuses.Issued;
        card.HolderAttendeeId = attendee.Id;
        card.IssuedAt = now;
        attendee.CardId = card.Id;
        attendee.CardNumber = card.CardNumber;
        attendee.CardIssuedAt = now;
        attendee.CardIssuedByName = actor.Name;
        attendee.CardReturnedAt = null;
        attendee.CardNotReturned = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return card;
    }

    /// <summary>Why a card can't be handed out, as a message the gate understands.</summary>
    internal static void EnsureCanIssue(VisitorCard card, Guid factoryId)
    {
        if (card.FactoryId != factoryId) throw new BusinessRuleException($"Card {card.CardNumber} belongs to another factory's gate.");
        switch (card.Status)
        {
            case VisitorCardStatuses.Issued: throw new BusinessRuleException($"Card {card.CardNumber} is already with someone on site.");
            case VisitorCardStatuses.Missing: throw new BusinessRuleException($"Card {card.CardNumber} is reported missing; mark it found first.");
            case VisitorCardStatuses.Retired: throw new BusinessRuleException($"Card {card.CardNumber} is retired.");
        }
    }

    public async Task<VisitorCard> ReturnAsync(Guid tenantId, Guid attendeeId, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var attendee = await dbContext.VisitAttendees.Include(x => x.Card)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == attendeeId, cancellationToken)
            ?? throw new NotFoundException("Visitor not found on any visit.");
        if (attendee.Card is not { } card || attendee.CardReturnedAt is not null) throw new BusinessRuleException("They have no card to hand back.");
        VisitorCardRules.TakeBack(attendee, card, returned: true, OccurredAt.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return card;
    }

    public async Task<VisitorCard> SetStatusAsync(Guid tenantId, Guid cardId, string status, string? notes, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var card = await dbContext.VisitorCards.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == cardId, cancellationToken)
            ?? throw new NotFoundException("Card not found.");
        var target = VisitorCardStatuses.All.FirstOrDefault(x => string.Equals(x, status?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Choose Available (found or back in the box), Missing or Retired.");
        if (target == VisitorCardStatuses.Issued) throw new BusinessRuleException("Cards are issued to a person at the gate, from their visit.");
        if (card.Status == VisitorCardStatuses.Issued && card.HolderAttendeeId is { } holder)
        {
            // Taking it out of circulation while someone holds it: record it on their visit too.
            var attendee = await dbContext.VisitAttendees.SingleAsync(x => x.Id == holder, cancellationToken);
            VisitorCardRules.TakeBack(attendee, card, returned: target == VisitorCardStatuses.Available, OccurredAt.UtcNow);
        }

        card.Status = target;
        card.HolderAttendeeId = null;
        if (target == VisitorCardStatuses.Available) card.LastReturnedAt = DateTime.UtcNow;
        if (notes is not null) card.Notes = VisitorService.Trim(notes, 500);
        await dbContext.SaveChangesAsync(cancellationToken);
        return card;
    }

    public async Task<VisitorCard> SetChipAsync(Guid tenantId, Guid cardId, string? serial, CancellationToken cancellationToken = default)
    {
        var card = await dbContext.VisitorCards.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == cardId, cancellationToken)
            ?? throw new NotFoundException("Card not found.");
        var normalised = PatrolService.NormaliseSerial(serial);
        if (serial is not null && normalised is null) throw new BusinessRuleException("That is not a card chip serial number.");
        if (normalised is not null && await dbContext.VisitorCards.AnyAsync(x => x.TenantId == tenantId && x.ChipSerial == normalised && x.Id != cardId, cancellationToken))
            throw new ConflictException("That chip is already registered to another card.");
        card.ChipSerial = normalised;
        await dbContext.SaveChangesAsync(cancellationToken);
        return card;
    }

    public Task<VisitorCard?> FindAsync(Guid tenantId, string code, CancellationToken cancellationToken = default) =>
        VisitorCardRules.FindAsync(dbContext, tenantId, code, cancellationToken);
}

/// <summary>Card rules shared by the card service and check-out.</summary>
public static class VisitorCardRules
{
    public const string QrPrefix = "CARD:";

    /// <summary>Records a card coming back (or not) from its holder.</summary>
    public static void TakeBack(VisitAttendee attendee, VisitorCard card, bool returned, DateTime at)
    {
        if (returned)
        {
            attendee.CardReturnedAt = at;
            attendee.CardNotReturned = false;
            card.Status = VisitorCardStatuses.Available;
            card.LastReturnedAt = at;
        }
        else
        {
            attendee.CardNotReturned = true;
            card.Status = VisitorCardStatuses.Missing;
        }

        card.HolderAttendeeId = null;
    }

    /// <summary>A card from its QR ("CARD:&lt;token&gt;"), its chip serial, or its printed number.</summary>
    public static async Task<VisitorCard?> FindAsync(AppDbContext dbContext, Guid tenantId, string code, CancellationToken cancellationToken)
    {
        var value = (code ?? "").Trim();
        if (value.Length == 0) return null;
        if (value.StartsWith(QrPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = value[QrPrefix.Length..];
            return await dbContext.VisitorCards.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Token == token, cancellationToken);
        }

        var number = value.ToUpperInvariant();
        var byNumber = await dbContext.VisitorCards.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.CardNumber == number, cancellationToken);
        if (byNumber is not null) return byNumber;
        var serial = PatrolService.NormaliseSerial(value);
        return serial is null ? null : await dbContext.VisitorCards.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ChipSerial == serial, cancellationToken);
    }
}
