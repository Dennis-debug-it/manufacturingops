using System.Net;
using Microsoft.AspNetCore.Builder;

namespace Ecosys.Api.Configuration;

/// <summary>
/// Reads <c>ECOSYS_KNOWN_PROXIES</c> into the forwarded-headers trust list. Entries may be single
/// addresses (<c>10.0.0.5</c>) or CIDR ranges (<c>172.18.0.0/16</c>, what the deployment guide
/// recommends because Docker reassigns the nginx container's address). Without a matching entry
/// every request appears to come from the proxy, so all users share one auth rate-limit bucket.
/// </summary>
public static class KnownProxiesConfiguration
{
    /// <summary>Adds each entry to <paramref name="options"/>; returns the entries that could not be parsed.</summary>
    public static IReadOnlyList<string> Apply(ForwardedHeadersOptions options, string? value)
    {
        var invalid = new List<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return invalid;
        }

        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (entry.Contains('/'))
            {
                if (IPNetwork.TryParse(entry, out var network))
                {
                    options.KnownIPNetworks.Add(network);
                    continue;
                }
            }
            else if (IPAddress.TryParse(entry, out var address))
            {
                options.KnownProxies.Add(address);
                continue;
            }

            invalid.Add(entry);
        }

        return invalid;
    }
}
