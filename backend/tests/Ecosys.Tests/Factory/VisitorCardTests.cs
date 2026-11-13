using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Ecosys.Tests.Factory.VisitorTests;

namespace Ecosys.Tests.Factory;

/// <summary>Reusable access cards: stock, issue at check-in, scanning, return or missing, lost and found.</summary>
public sealed class VisitorCardTests
{
    [Fact]
    public async Task CardsAreAddedInNumberedBatches_WithoutDuplicates()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var cards = new VisitorCardService(db);

        Assert.Equal(20, await cards.AddBatchAsync(site.TenantId, new NewCardBatch(site.FactoryId, "v-", 1, 20, "Visitor", "Green")));
        Assert.Equal(5, await cards.AddBatchAsync(site.TenantId, new NewCardBatch(site.FactoryId, "V-", 15, 25, "visitor", null)));
        Assert.Equal(["V-001", "V-002"], await db.VisitorCards.OrderBy(x => x.CardNumber).Take(2).Select(x => x.CardNumber).ToListAsync());
        Assert.Equal(25, await db.VisitorCards.Select(x => x.Token).Distinct().CountAsync());
        Assert.Equal(3, await cards.AddBatchAsync(site.TenantId, new NewCardBatch(site.FactoryId, "C-", 1, 3, "Contractor", "Orange")));
        await Assert.ThrowsAsync<BusinessRuleException>(() => cards.AddBatchAsync(site.TenantId, new NewCardBatch(site.FactoryId, "V/", 1, 2, "Visitor", null)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => cards.AddBatchAsync(site.TenantId, new NewCardBatch(site.FactoryId, "V-", 1, 600, "Visitor", null)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => cards.AddBatchAsync(site.TenantId, new NewCardBatch(site.FactoryId, "V-", 1, 2, "Pirate", null)));
    }

    [Fact]
    public async Task ACardHandedOverAtCheckIn_FindsItsHolder_AndComesBackAtCheckOut()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var cards = new VisitorCardService(db);
        await cards.AddBatchAsync(site.TenantId, new NewCardBatch(site.FactoryId, "V-", 1, 3, "Visitor", null));
        var visitors = Service(db);
        await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("Ann One"), new VisitorDetails("Ben Two")]), site.Host);
        var people = await db.VisitAttendees.OrderBy(x => x.BadgeNumber).ToListAsync();

        // The gate checks flag a wrong card before anything is saved.
        Assert.Equal("card", Assert.Single((await visitors.CheckAsync(site.TenantId, people[0].Id, new GateConfirmation(CardCode: "V-999"), DateTime.UtcNow)).Issues).Code);
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckInAsync(site.TenantId, people[0].Id, new GateConfirmation(CardCode: "V-999"), site.Gate));
        Assert.Null((await db.VisitAttendees.SingleAsync(x => x.Id == people[0].Id)).CheckedInAt);

        await visitors.CheckInAsync(site.TenantId, people[0].Id, new GateConfirmation(CardCode: "v-001"), site.Gate);
        var card = await db.VisitorCards.SingleAsync(x => x.CardNumber == "V-001");
        Assert.Equal((VisitorCardStatuses.Issued, people[0].Id), (card.Status, card.HolderAttendeeId));
        Assert.Equal(("V-001", "Gate Officer"), ((await db.VisitAttendees.SingleAsync(x => x.Id == people[0].Id)).CardNumber, (await db.VisitAttendees.SingleAsync(x => x.Id == people[0].Id)).CardIssuedByName));

        // The same card can't go to someone else while it is out.
        Assert.Contains("already with someone", Assert.Single((await visitors.CheckAsync(site.TenantId, people[1].Id, new GateConfirmation(CardCode: "V-001"), DateTime.UtcNow)).Issues).Message);

        // Scanning the card's QR, typing its number or tapping its chip all find Ann.
        await cards.SetChipAsync(site.TenantId, card.Id, "04:a2:2b:9c:11:80");
        Assert.Equal(people[0].Id, (await visitors.FindBadgeAsync(site.TenantId, $"CARD:{card.Token}"))!.Id);
        Assert.Equal(people[0].Id, (await visitors.FindBadgeAsync(site.TenantId, "v-001"))!.Id);
        Assert.Equal(people[0].Id, (await visitors.FindBadgeAsync(site.TenantId, "04A22B9C1180"))!.Id);
        Assert.Null(await visitors.FindBadgeAsync(site.TenantId, "V-002")); // in the box: nobody holds it

        await visitors.CheckOutAsync(site.TenantId, people[0].Id, site.Gate);
        card = await db.VisitorCards.SingleAsync(x => x.CardNumber == "V-001");
        Assert.Equal((VisitorCardStatuses.Available, (Guid?)null), (card.Status, card.HolderAttendeeId));
        Assert.NotNull((await db.VisitAttendees.SingleAsync(x => x.Id == people[0].Id)).CardReturnedAt);
        Assert.Null(await visitors.FindBadgeAsync(site.TenantId, "V-001"));
    }

    [Fact]
    public async Task ACardNotHandedBack_IsMissingUntilFound()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var cards = new VisitorCardService(db);
        await cards.AddBatchAsync(site.TenantId, new NewCardBatch(site.FactoryId, "V-", 1, 2, "Visitor", null));
        var visitors = Service(db);
        await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("Ann One"), new VisitorDetails("Ben Two")]), site.Host);
        var people = await db.VisitAttendees.OrderBy(x => x.BadgeNumber).ToListAsync();
        await visitors.CheckInAsync(site.TenantId, people[0].Id, new GateConfirmation(CardCode: "V-001"), site.Gate);

        await visitors.CheckOutAsync(site.TenantId, people[0].Id, site.Gate, cardReturned: false);
        var card = await db.VisitorCards.SingleAsync(x => x.CardNumber == "V-001");
        Assert.Equal(VisitorCardStatuses.Missing, card.Status);
        Assert.True((await db.VisitAttendees.SingleAsync(x => x.Id == people[0].Id)).CardNotReturned);
        Assert.Null(await visitors.FindBadgeAsync(site.TenantId, "V-001")); // a missing card opens nothing

        // Ben can't get it; he gets V-002 after check-in instead, then hands it back early.
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckInAsync(site.TenantId, people[1].Id, new GateConfirmation(CardCode: "V-001"), site.Gate));
        await visitors.CheckInAsync(site.TenantId, people[1].Id, new GateConfirmation(), site.Gate);
        await cards.IssueAsync(site.TenantId, people[1].Id, "V-002", site.Gate);
        await Assert.ThrowsAsync<BusinessRuleException>(() => cards.IssueAsync(site.TenantId, people[1].Id, "V-001", site.Gate));
        await cards.ReturnAsync(site.TenantId, people[1].Id, site.Gate);
        Assert.Equal(VisitorCardStatuses.Available, (await db.VisitorCards.SingleAsync(x => x.CardNumber == "V-002")).Status);

        // Found in the car park.
        await cards.SetStatusAsync(site.TenantId, card.Id, "Available", "Found in the car park", site.Gate);
        Assert.Equal((VisitorCardStatuses.Available, "Found in the car park"), ((await db.VisitorCards.SingleAsync(x => x.Id == card.Id)).Status, (await db.VisitorCards.SingleAsync(x => x.Id == card.Id)).Notes));
        await cards.IssueAsync(site.TenantId, people[1].Id, "V-001", site.Gate);

        // Retiring a card someone holds takes it off them.
        await cards.SetStatusAsync(site.TenantId, card.Id, "Retired", "Cracked", site.Manager);
        Assert.True((await db.VisitAttendees.SingleAsync(x => x.Id == people[1].Id)).CardNotReturned);
        await Assert.ThrowsAsync<BusinessRuleException>(() => cards.SetStatusAsync(site.TenantId, card.Id, "Issued", null, site.Manager));
    }

    [Fact]
    public async Task ACardFromAnotherFactorysGate_IsRefused()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var other = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        db.Add(other);
        await db.SaveChangesAsync();
        await new VisitorCardService(db).AddBatchAsync(site.TenantId, new NewCardBatch(other.Id, "M-", 1, 1, "Visitor", null));
        var visitors = Service(db);
        await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("Ann One")]), site.Host);
        var ann = await db.VisitAttendees.SingleAsync();
        Assert.Contains("another factory", Assert.Single((await visitors.CheckAsync(site.TenantId, ann.Id, new GateConfirmation(CardCode: "M-001"), DateTime.UtcNow)).Issues).Message);
    }

    [Fact]
    public async Task Postgres_CardQueries_AllTranslate()
    {
        await FactoryPostgres.RunAsync("cards", async connection =>
        {
            var (db, context) = await FactoryPostgres.SeedAsync(connection);
            await using var _ = db;
            var tenantId = context.TenantId!.Value;
            var factory = new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
            db.Add(factory);
            await db.SaveChangesAsync();
            var cards = new VisitorCardService(db);
            var gate = WithUser(new VisitorGateController(db, context, FactoryTestSupport.Scope(db, context), new VehicleLogService(db), new VisitorPassService(db, Service(db)),
                FactoryTestSupport.Audit(db), new VisitorBadgePdfRenderer(), cards, new AssetLabelPdfRenderer()));
            Assert.Equal(10, Ok(await gate.AddCards(new AddCardsRequest(factory.Id, 1, 10, "V-", "Visitor", "Green"), default)).Added);

            var visits = Controller(db, context);
            var visit = Ok(await visits.Create(new CreateVisitRequest(factory.Id, "Meeting", [new VisitorDetailsRequest("Amina Hassan", "0712345678"), new VisitorDetailsRequest("Joe Kim")]), default));
            var amina = visit.Attendees.Single(x => x.FullName == "Amina Hassan");
            var joe = visit.Attendees.Single(x => x.FullName == "Joe Kim");
            Assert.Equal("card", Assert.Single(Ok(await visits.Check(amina.Id, new GateConfirmationRequest(CardCode: "V-099"), default)).Issues).Code);
            var row = Ok(await visits.CheckIn(amina.Id, new GateConfirmationRequest(CardCode: "V-003"), default));
            Assert.Equal(("V-003", true), (row.Attendees.Single(x => x.Id == amina.Id).CardNumber, row.Attendees.Single(x => x.Id == amina.Id).CardOut));
            Ok(await visits.CheckIn(joe.Id, new GateConfirmationRequest(), default));
            Ok(await gate.IssueCard(joe.Id, new IssueCardRequest("V-004"), default));

            Assert.Equal("V-003", Assert.Single(Ok(await visits.OnSite(null, default)), x => x.FullName == "Amina Hassan").CardNumber);
            var index = Ok(await visits.GateIndex(null, default));
            Assert.Equal(("V-003", true), (index.People.Single(x => x.AttendeeId == amina.Id).CardNumber, index.People.Single(x => x.AttendeeId == amina.Id).CardToken is not null));
            Assert.Equal(amina.Id, Ok(await visits.Badge("V-003", default)).Attendee.Id);

            Ok(await visits.CheckOut(amina.Id, new CheckOutRequest(CardReturned: false), default));
            var list = Ok(await gate.CardList(null, null, null, default));
            Assert.Equal(10, list.Count);
            var missing = list.Single(x => x.CardNumber == "V-003");
            Assert.Equal((VisitorCardStatuses.Missing, "Amina Hassan", "+254712345678"), (missing.Status, missing.LastHolderName, missing.LastHolderPhone));
            Assert.Equal("Joe Kim", list.Single(x => x.CardNumber == "V-004").HolderName);
            Assert.Single(Ok(await gate.CardList(factory.Id, VisitorCardStatuses.Missing, null, default)));
            Assert.Single(Ok(await gate.CardList(null, null, "003", default)));
            Ok(await gate.SetCardStatus(missing.Id, new CardStatusRequest("Available", "Returned by courier"), default));
            Ok(await gate.ReturnCard(joe.Id, default));

            var labels = Assert.IsType<FileContentResult>(await gate.CardLabels(new CardLabelsRequest(factory.Id), default));
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(labels.FileContents, 0, 4));
        });
    }
}
