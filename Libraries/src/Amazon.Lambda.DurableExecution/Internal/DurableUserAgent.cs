// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;
using Amazon.Runtime.Internal;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Tags the durable-execution requests this SDK issues with a SDK-identifying
/// user-agent component so the service can track usage of the .NET durable
/// execution SDK at runtime. Mirrors the sibling SDKs (e.g. the Java SDK emits
/// <c>aws-durable-execution-sdk-java/&lt;version&gt;</c>).
///
/// Applied per request — on the two <see cref="Services.LambdaDurableServiceClient"/>
/// calls — rather than via a process-wide pipeline customizer, so it works for both
/// the default client and a caller-supplied <see cref="IAmazonLambda"/> and never
/// tags unrelated requests on a shared client. The <see cref="AssemblyVersion"/>
/// member is generated at build time from the package <c>$(Version)</c> by the
/// <c>_GenerateDurableUserAgentVersion</c> MSBuild target.
/// </summary>
internal static partial class DurableUserAgent
{
    /// <summary>
    /// The user-agent component appended to every durable-execution request, e.g.
    /// <c>aws-durable-execution-sdk-dotnet/1.0.0</c>.
    /// </summary>
    internal static readonly string UserAgentString =
        $"aws-durable-execution-sdk-dotnet/{AssemblyVersion}";

    /// <summary>
    /// Appends the SDK-identifying component to the request's user agent. The
    /// underlying <c>UserAgentDetails</c> dedupes components internally, so repeated
    /// calls are harmless.
    /// </summary>
    internal static void Apply(AmazonWebServiceRequest request)
    {
        ((IAmazonWebServiceRequest)request).UserAgentDetails.AddUserAgentComponent(UserAgentString);
    }
}
