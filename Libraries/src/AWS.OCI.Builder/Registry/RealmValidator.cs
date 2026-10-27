using System.Net;
using System.Net.Sockets;

namespace Amazon.OCI.Builder.Registry;

/// <summary>
/// Validates the <c>realm</c> (token endpoint) URL returned in a registry's <c>WWW-Authenticate</c>
/// challenge before the client sends credentials to it.
/// </summary>
/// <remarks>
/// A compromised or malicious registry could return a <c>realm</c> that points at an internal or loopback
/// address in order to exfiltrate credentials or probe the host's private network (an SSRF vector). This
/// validator rejects any realm that is not HTTPS or whose host resolves to a loopback, link-local, private,
/// or unspecified address. The approach mirrors <c>Microsoft.NET.Build.Containers</c>. Because we only
/// target public registries (ECR, MCR, Docker Hub), there is no insecure/on-prem exception.
/// </remarks>
internal static class RealmValidator
{
    private static readonly IPNetwork[] BlockedV4Networks =
    [
        IPNetwork.Parse("0.0.0.0/8"),      // "this network" / unspecified
        IPNetwork.Parse("10.0.0.0/8"),     // private (RFC 1918)
        IPNetwork.Parse("172.16.0.0/12"),  // private (RFC 1918)
        IPNetwork.Parse("192.168.0.0/16"), // private (RFC 1918)
        IPNetwork.Parse("169.254.0.0/16"), // link-local
        IPNetwork.Parse("224.0.0.0/24"),   // link-local multicast
    ];

    /// <summary>
    /// Validates <paramref name="realm"/> and returns it as a <see cref="Uri"/>.
    /// </summary>
    /// <exception cref="InvalidAuthResponseException">
    /// Thrown when the realm is unparseable, not HTTPS, or points at a blocked (loopback/private/link-local) host.
    /// </exception>
    public static Uri Validate(string realm, string registryName)
    {
        if (!Uri.TryCreate(realm, UriKind.Absolute, out Uri? realmUri))
        {
            throw new InvalidAuthResponseException(
                registryName, $"The authentication realm '{realm}' is relative or could not be parsed.");
        }

        // Only HTTPS is permitted (we target public registries exclusively).
        if (!realmUri.Scheme.Equals("https", StringComparison.Ordinal))
        {
            throw new InvalidAuthResponseException(
                registryName, $"The authentication realm '{realm}' uses disallowed scheme '{realmUri.Scheme}'; only https is allowed.");
        }

        // Use IdnHost (the ASCII/canonical host) so Unicode-dot forms that the runtime canonicalizes
        // back to a loopback/private literal cannot bypass the check by appearing as a DNS name.
        string realmHost = TrimTrailingDot(realmUri.IdnHost);

        if (IPAddress.TryParse(realmHost, out IPAddress? realmIp) && IsBlockedIpLiteral(realmIp))
        {
            throw new InvalidAuthResponseException(
                registryName, $"The authentication realm '{realm}' resolves to a blocked (loopback/private/link-local) address '{realmHost}'.");
        }

        if (IsLoopbackDnsName(realmHost))
        {
            // RFC 6761 reserves "localhost"/"*.localhost" for loopback resolution regardless of /etc/hosts.
            throw new InvalidAuthResponseException(
                registryName, $"The authentication realm '{realm}' uses a loopback host name '{realmHost}'.");
        }

        return realmUri;
    }

    private static bool IsLoopbackDnsName(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
           || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static string TrimTrailingDot(string host)
        => host.Length > 1 && host[^1] == '.' ? host[..^1] : host;

    private static bool IsBlockedIpLiteral(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        // IPv4-mapped IPv6: unwrap so we don't need a parallel set of IPv4-in-IPv6 CIDRs.
        if (ip.IsIPv4MappedToIPv6)
        {
            return IsBlockedIpLiteral(ip.MapToIPv4());
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            foreach (IPNetwork net in BlockedV4Networks)
            {
                if (net.Contains(ip))
                {
                    return true;
                }
            }
        }

        // IPv6-only properties; all return false for IPv4 so the family gate is implicit.
        return ip.Equals(IPAddress.IPv6Any)
               || ip.IsIPv6LinkLocal
               || ip.IsIPv6SiteLocal
               || ip.IsIPv6UniqueLocal
               || (ip.IsIPv6Multicast && (ip.GetAddressBytes()[1] & 0x0f) == 0x02);
    }
}
