// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Internal;

#if NET8_0_OR_GREATER
/// <summary>
/// Helper class for storing Requests for
/// <see cref="ServiceCollectionExtensions.AddAWSLambdaBeforeSnapshotRequest"/>
/// </summary>
internal class GetBeforeSnapshotRequestsCollector
{
    public HttpRequestMessage? Request { get; set; }
}
#endif
