// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool.Extensions;

/// <summary>
/// A class that contains extension methods for the <see cref="Exception"/> class.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// True if the <paramref name="e"/> inherits from
    /// <see cref="TestToolException"/>.
    /// </summary>
    public static bool IsExpectedException(this Exception e) =>
        e is TestToolException;

    /// <summary>
    /// Prints an exception in a user-friendly way.
    /// </summary>
    public static string PrettyPrint(this Exception? e)
    {
        if (null == e)
            return string.Empty;

        return $"{Environment.NewLine}{e.Message}" +
               $"{Environment.NewLine}{e.StackTrace}" +
               $"{PrettyPrint(e.InnerException)}";
    }
}
