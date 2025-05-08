// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Services.IO;

/// <inheritdoc cref="IDirectoryManager"/>
public class DirectoryManager : IDirectoryManager
{
    /// <inheritdoc />
    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
}
