using Amazon.OCI.Builder;
using Xunit;

namespace AWS.OCI.Builder.Tests;

public class ImageReferenceTests
{
    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/aspnet:8.0", "mcr.microsoft.com", "dotnet/aspnet", "8.0", false)]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp:latest",
        "123456789012.dkr.ecr.us-west-2.amazonaws.com", "myapp", "latest", false)]
    [InlineData("library/ubuntu:22.04", "registry-1.docker.io", "library/ubuntu", "22.04", false)]
    public void Parses_explicit_and_hub_references(
        string input, string registry, string repository, string reference, bool isDigest)
    {
        ImageReference parsed = ImageReference.Parse(input);

        Assert.Equal(registry, parsed.Registry);
        Assert.Equal(repository, parsed.Repository);
        Assert.Equal(reference, parsed.Reference);
        Assert.Equal(isDigest, parsed.ReferenceIsDigest);
    }

    [Fact]
    public void Single_segment_hub_repo_gets_library_prefix_and_latest_tag()
    {
        ImageReference parsed = ImageReference.Parse("ubuntu");

        Assert.Equal("registry-1.docker.io", parsed.Registry);
        Assert.Equal("library/ubuntu", parsed.Repository);
        Assert.Equal("latest", parsed.Reference);
    }

    [Fact]
    public void Parses_digest_reference()
    {
        const string digest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";
        ImageReference parsed = ImageReference.Parse($"mcr.microsoft.com/dotnet/runtime@{digest}");

        Assert.Equal("dotnet/runtime", parsed.Repository);
        Assert.Equal(digest, parsed.Reference);
        Assert.True(parsed.ReferenceIsDigest);
    }

    [Theory]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com/app:latest", "us-west-2")]
    [InlineData("123456789012.dkr.ecr.eu-central-1.amazonaws.com/app:latest", "eu-central-1")]
    [InlineData("210987654321.dkr.ecr-fips.us-east-1.amazonaws.com/app:latest", "us-east-1")]
    public void Detects_ecr_and_extracts_region(string input, string expectedRegion)
    {
        ImageReference parsed = ImageReference.Parse(input);

        Assert.True(parsed.IsEcr);
        Assert.Equal(expectedRegion, parsed.EcrRegion);
    }

    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/aspnet:8.0")]
    [InlineData("registry-1.docker.io/library/ubuntu:22.04")]
    public void Non_ecr_references_are_not_ecr(string input)
    {
        ImageReference parsed = ImageReference.Parse(input);

        Assert.False(parsed.IsEcr);
        Assert.Null(parsed.EcrRegion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_null_or_blank(string input)
        => Assert.Throws<ArgumentException>(() => ImageReference.Parse(input));

    [Fact]
    public void Preserves_non_default_registry_port()
    {
        ImageReference parsed = ImageReference.Parse("localhost:5000/myapp:dev");

        Assert.Equal("localhost:5000", parsed.Registry);
        Assert.Equal("myapp", parsed.Repository);
        Assert.Equal("dev", parsed.Reference);
    }
}
