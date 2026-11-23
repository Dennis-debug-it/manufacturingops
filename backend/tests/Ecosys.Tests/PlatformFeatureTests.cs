using Ecosys.Infrastructure.Services;

namespace Ecosys.Tests;

public sealed class PlatformFeatureTests
{
    private static readonly PlatformFeatureOptions Off = new() { WhatsApp = false };

    [Theory]
    [InlineData("POST", "/api/whatsapp/conversations")]
    [InlineData("PUT", "/api/whatsapp-notifications/settings")]
    [InlineData("POST", "/api/me/whatsapp/start")]
    [InlineData("PUT", "/api/platform/whatsapp/tenants/6f0c1a4e-0000-0000-0000-000000000000")]
    public void SwitchedOffFeatures_RefuseChanges(string method, string path) =>
        Assert.NotNull(Off.RefusalFor(method, path));

    [Theory]
    [InlineData("GET", "/api/whatsapp-notifications/outbox")]
    [InlineData("POST", "/api/whatsapp/webhook")]
    [InlineData("POST", "/api/intake/monitoring/webhook")]
    [InlineData("POST", "/api/crm/opportunities/6f0c1a4e-0000-0000-0000-000000000000/email")]
    public void Reads_TheMetaWebhook_AndOtherFeatures_StillWork(string method, string path) =>
        Assert.Null(Off.RefusalFor(method, path));

    [Fact]
    public void FeaturesOnByDefault_RefuseNothing() =>
        Assert.Null(new PlatformFeatureOptions().RefusalFor("PUT", "/api/whatsapp-notifications/settings"));
}
