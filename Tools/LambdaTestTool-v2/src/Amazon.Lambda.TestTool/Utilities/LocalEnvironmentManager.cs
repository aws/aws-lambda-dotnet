// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using Amazon.Lambda.TestTool.Services.IO;

namespace Amazon.Lambda.TestTool.Utilities;

public class LocalEnvironmentManager(IDictionary environmentManager) : IEnvironmentManager
{
    public IDictionary GetEnvironmentVariables() => environmentManager;
}
