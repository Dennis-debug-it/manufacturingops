using System.Net;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Infrastructure.Services.Sms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests.Factory;

public sealed class SmsAndDashboardTests
{
    [Theory]
    [InlineData("0712 345 678", "+254712345678")]
    [InlineData("+254 712 345 678", "+254712345678")]
    [InlineData("254712345678", "+254712345678")]
    [InlineData("712345678", "+254712345678")]
    [InlineData("00256 772 123456", "+256772123456")]
    [InlineData("12345", null)]
    [InlineData(null, null)]
    public void PhoneNumbers_AreSentInInternationalForm(string? input, string? expected) =>
        Assert.Equal(expected, SmsText.NormalizePhone(input));

    [Fact]
    public void LongAlerts_AreCutToTwoSegments()
    {
        var text = SmsText.Compose("Breakdown reported: Dryer 1", new string('x', 500));
        Assert.Equal(SmsText.MaxLength, text.Length);
        Assert.StartsWith("Breakdown reported: Dryer 1: ", text);
    }

    [Fact]
    public async Task AfricasTalking_GetsAFormPostWithTheApiKey()
    {
        var handler = new CapturingHandler();
        var gateway = new AfricasTalkingSmsGateway(new SingleClientFactory(handler),
            Microsoft.Extensions.Options.Options.Create(new SmsOptions { Username = "chelal", ApiKey = "secret-key", SenderId = "ECOSYS", BaseUrl = "https://sms.test" }),
            NullLogger<AfricasTalkingSmsGateway>.Instance);

        Assert.True(await gateway.SendAsync("+254712345678", "Dryer down"));
        Assert.Equal("https://sms.test/version1/messaging", handler.Request!.RequestUri!.ToString());
        Assert.Equal("secret-key", handler.Request.Headers.GetValues("apiKey").Single());
        Assert.Contains("username=chelal", handler.Body);
        Assert.Contains("to=%2B254712345678", handler.Body);
        Assert.Contains("from=ECOSYS", handler.Body);

        handler.Status = HttpStatusCode.Unauthorized;
        Assert.False(await gateway.SendAsync("+254712345678", "Dryer down"));
    }

    [Fact]
    public void Sms_IsOnByDefaultOnlyForUrgentFactoryAlerts_AndUsersCanTurnItOff()
    {
        var reported = NotificationKeyDescriptor.Find("factory-job.reported")!;
        var assigned = NotificationKeyDescriptor.Find("work-order.assigned")!;
        Assert.True(NotificationPreferences.Resolve(reported, null).HasFlag(NotificationChannels.Sms));
        Assert.False(NotificationPreferences.Resolve(assigned, null).HasFlag(NotificationChannels.Sms));

        var saved = new UserNotificationPreference { NotificationKey = reported.Key, EmailEnabled = true, InAppEnabled = true };
        Assert.True(NotificationPreferences.Resolve(reported, saved).HasFlag(NotificationChannels.Sms)); // unset: follows the default
        saved.SmsEnabled = false;
        Assert.False(NotificationPreferences.Resolve(reported, saved).HasFlag(NotificationChannels.Sms));
    }

    [Fact]
    public async Task Dashboard_ShowsJobsBreakdownsDowntimeAndFailingAssets()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        site.Dryer.LostProductionPerHour = 100;
        await db.SaveChangesAsync();
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);

        var breakdown = Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<CreatedAtActionResult>((await jobs.Create(
            new CreateFactoryWorkOrderRequest("Dryer tripped", AssetId: site.Dryer.Id, IsBreakdown: true, DowntimeStartedAt: DateTime.UtcNow.AddHours(-3)), CancellationToken.None)).Result).Value);
        Assert.IsType<CreatedAtActionResult>((await jobs.Create(new CreateFactoryWorkOrderRequest("Guard loose", AssetId: site.Dryer.Id, DueDate: DateTime.UtcNow.AddDays(-1)), CancellationToken.None)).Result);
        Assert.IsType<CreatedAtActionResult>((await jobs.Create(new CreateFactoryWorkOrderRequest("Paint walkway", OrgNodeId: site.Drying, WorkType: "Safety"), CancellationToken.None)).Result);

        var dashboard = Assert.IsType<FactoryDashboardResponse>(Assert.IsType<OkObjectResult>(
            (await new FactoryDashboardController(db, site.Admin, FactoryTestSupport.Access(site.Admin), FactoryTestSupport.Scope(db, site.Admin), FactoryTestSupport.Settings(db))
                .Get(null, CancellationToken.None)).Result).Value);

        Assert.Equal(3, dashboard.Jobs.Open);
        Assert.Equal(1, dashboard.Jobs.Overdue);
        Assert.Equal(2, dashboard.Jobs.AwaitingApproval); // the breakdown skipped approval
        Assert.Equal(1, dashboard.AssetsDown);
        Assert.Equal(breakdown.Id, Assert.Single(dashboard.OpenBreakdowns).WorkOrderId);
        Assert.InRange(dashboard.DowntimeHoursLast7Days, 2.9m, 3.1m);
        Assert.InRange(dashboard.ProductionLostLast7Days, 290m, 310m);
        Assert.Equal("kg made tea", dashboard.ProductionUnit);
        var top = Assert.Single(dashboard.TopFailingAssets);
        Assert.Equal((2, 1), (top.Jobs, top.Breakdowns));
        Assert.Equal((2, 1), (dashboard.ReactiveJobsLast30Days, dashboard.PlannedJobsLast30Days));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent("{}") };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
