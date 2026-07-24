// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Tests;

/// <summary>
/// Groups tests that mutate the process-wide AWS_LAMBDA_FUNCTION_NAME (and related) environment
/// variables via <see cref="Amazon.Lambda.AspNetCoreServer.Test.EnvironmentVariableHelper"/>. xUnit runs
/// all classes in a single collection sequentially, so applying [Collection(Name)] to these classes
/// prevents them from running in parallel and clobbering each other's environment variables - which
/// otherwise causes intermittent failures (for example a test that expects the variable to be unset
/// running while another test has it set).
/// </summary>
[CollectionDefinition(Name)]
public class EnvironmentVariableCollection
{
    public const string Name = "EnvironmentVariable";
}
