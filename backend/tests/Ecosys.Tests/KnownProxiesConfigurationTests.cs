using System.Net;
using Ecosys.Api.Configuration;
using Microsoft.AspNetCore.Builder;

namespace Ecosys.Tests;

public sealed class KnownProxiesConfigurationTests
{
    [Fact]
    public void Accepts_addresses_and_cidr_ranges_and_reports_the_rest()
    {
        var options = new ForwardedHeadersOptions();
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        var invalid = KnownProxiesConfiguration.Apply(options, " 10.0.0.5, 172.18.0.0/16 ,not-an-ip, 10.0.0.0/99");

        Assert.Equal([IPAddress.Parse("10.0.0.5")], options.KnownProxies);
        var network = Assert.Single(options.KnownIPNetworks);
        Assert.True(network.Contains(IPAddress.Parse("172.18.0.3")));
        Assert.Equal(["not-an-ip", "10.0.0.0/99"], invalid);
    }

    [Fact]
    public void Leaves_the_defaults_when_unset()
    {
        var options = new ForwardedHeadersOptions();
        var before = options.KnownProxies.Count;

        Assert.Empty(KnownProxiesConfiguration.Apply(options, null));
        Assert.Equal(before, options.KnownProxies.Count);
    }
}
