using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

public class RealmValidatorTests
{
    [Theory]
    [InlineData("https://auth.docker.io/token")]
    [InlineData("https://public.ecr.aws/token/")]
    [InlineData("https://123456789012.dkr.ecr.us-west-2.amazonaws.com/")]
    public void Accepts_public_https_realms(string realm)
    {
        Uri result = RealmValidator.Validate(realm, "registry.example.com");
        Assert.Equal("https", result.Scheme);
    }

    [Theory]
    [InlineData("http://auth.docker.io/token")]       // not https
    [InlineData("ftp://auth.docker.io/token")]         // not https
    public void Rejects_non_https_scheme(string realm)
    {
        Assert.Throws<InvalidAuthResponseException>(() => RealmValidator.Validate(realm, "r"));
    }

    [Theory]
    [InlineData("https://127.0.0.1/token")]            // loopback v4
    [InlineData("https://[::1]/token")]                // loopback v6
    [InlineData("https://10.0.0.5/token")]             // private
    [InlineData("https://192.168.1.10/token")]         // private
    [InlineData("https://172.16.4.4/token")]           // private
    [InlineData("https://169.254.1.1/token")]          // link-local
    [InlineData("https://localhost/token")]            // loopback name
    [InlineData("https://foo.localhost/token")]        // loopback name
    [InlineData("https://127.0.0.1./token")]           // trailing-dot loopback
    public void Rejects_loopback_private_and_linklocal_hosts(string realm)
    {
        Assert.Throws<InvalidAuthResponseException>(() => RealmValidator.Validate(realm, "r"));
    }

    [Fact]
    public void Rejects_unparseable_realm()
    {
        Assert.Throws<InvalidAuthResponseException>(() => RealmValidator.Validate("not a url", "r"));
    }

    [Theory]
    [InlineData("127.0.0.1", true)]     // loopback
    [InlineData("10.1.2.3", true)]      // private
    [InlineData("172.16.5.5", true)]    // private
    [InlineData("192.168.0.9", true)]   // private
    [InlineData("169.254.1.1", true)]   // link-local
    [InlineData("::1", true)]           // IPv6 loopback
    [InlineData("8.8.8.8", false)]      // public
    public void IsBlockedAddress_flags_private_and_loopback(string ip, bool expected)
        => Assert.Equal(expected, RealmValidator.IsBlockedAddress(System.Net.IPAddress.Parse(ip)));

    [Theory]
    [InlineData("https://localhost/x", true)]
    [InlineData("https://sub.localhost/x", true)]
    [InlineData("https://127.0.0.1/x", true)]
    [InlineData("https://10.0.0.5/x", true)]
    [InlineData("https://registry.example.com/x", false)] // public DNS name: not blocked by the pre-connect check
    public void IsBlockedHost_flags_loopback_names_and_blocked_literals(string url, bool expected)
        => Assert.Equal(expected, RealmValidator.IsBlockedHost(new Uri(url)));
}
