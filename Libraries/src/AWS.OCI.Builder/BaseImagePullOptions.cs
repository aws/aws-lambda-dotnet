using System.Net;

namespace AWS.OCI.Builder;

/// <summary>
/// Options controlling how the base image is pulled by <see cref="OciImageBuilder.FromBaseImageAsync(string, string, BaseImagePullOptions?, CancellationToken)"/>.
/// </summary>
/// <remarks>
/// All properties are optional. The registry client created with these options also downloads the base image's
/// layers when <see cref="OciImageBuilder.PushToEcrAsync"/> copies them to ECR, so they apply to those downloads too.
/// </remarks>
public sealed class BaseImagePullOptions
{
    /// <summary>
    /// The proxy for requests to the base image's registry, or <see langword="null"/> (the default) for
    /// <see cref="System.Net.Http.HttpClient.DefaultProxy"/>, which reads the <c>HTTPS_PROXY</c>, <c>HTTP_PROXY</c>
    /// and <c>NO_PROXY</c> environment variables (and, on Windows, the system proxy settings).
    /// </summary>
    public IWebProxy? Proxy { get; init; }

    /// <summary>
    /// The HTTP timeout for each request to the base image's registry. Defaults to
    /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>, so a request is bounded only by the
    /// <see cref="CancellationToken"/> passed to the pull, or to the push for a base layer download.
    /// </summary>
    /// <remarks>
    /// A finite value must be positive; zero or a negative value is rejected when the pull starts.
    /// </remarks>
    public TimeSpan Timeout { get; init; } = System.Threading.Timeout.InfiniteTimeSpan;
}
