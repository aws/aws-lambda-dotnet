// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Tests;

internal static class TestModuleInitializer
{
    /// <summary>
    /// Disable Amazon.Lambda.RuntimeSupport's stdout/stderr capture for the duration of this test assembly.
    /// Tests such as <see cref="AddAWSLambdaBeforeSnapshotRequestTests"/> start a real LambdaBootstrap (via
    /// app.RunAsync()), which otherwise replaces the process-wide Console.Out/Console.Error with synchronized
    /// writers. That global change leaks across tests in the same process and can deadlock other tests' logging,
    /// hanging the whole test host (observed as a multi-hour stall on the Linux CI machines). Setting this before
    /// any test runs makes the bootstrap skip the capture. See AWS_LAMBDA_DOTNET_DISABLE_CONSOLE_CAPTURE handling
    /// in Amazon.Lambda.RuntimeSupport's LogLevelLoggerWriter.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize()
    {
        Environment.SetEnvironmentVariable("AWS_LAMBDA_DOTNET_DISABLE_CONSOLE_CAPTURE", "true");
    }
}
