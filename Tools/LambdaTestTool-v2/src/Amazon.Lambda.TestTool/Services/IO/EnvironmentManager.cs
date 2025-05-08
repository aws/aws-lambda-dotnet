// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace Amazon.Lambda.TestTool.Services.IO;

/// <inheritdoc cref="IEnvironmentManager"/>
public class EnvironmentManager : IEnvironmentManager
{
    /// <inheritdoc />
    public IDictionary GetEnvironmentVariables() => Environment.GetEnvironmentVariables();
}
