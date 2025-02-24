// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Model;

namespace Amazon.Lambda.TestTool.Services;

public interface ILambdaClient
{
    Task<InvokeResponse> InvokeAsync(InvokeRequest request);
    void SetEndpoint(string endpoint);
}
