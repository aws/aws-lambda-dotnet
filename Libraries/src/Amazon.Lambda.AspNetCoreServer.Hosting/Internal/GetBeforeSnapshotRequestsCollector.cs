// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Internal;

/// <summary>
/// Helper class for storing Requests for
/// <see cref="ServiceCollectionExtensions.AddAWSLambdaBeforeSnapshotRequest"/>
/// </summary>
internal class GetBeforeSnapshotRequestsCollector
{
    public HttpRequestMessage? Requests { get; set; }
}

