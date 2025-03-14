// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace Amazon.Lambda.AspNetCoreServer.Test;

public class EnvironmentVariableHelper : IDisposable
{
    private string _name;
    private string? _oldValue;
    public EnvironmentVariableHelper(string name, string value)
    {
        _name = name;
        _oldValue = Environment.GetEnvironmentVariable(name);

        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _oldValue);
}
