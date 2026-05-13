// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// Provides an implementation of <see cref="IToolInteractiveService"/> that interacts with the console.
/// </summary>
public class ConsoleInteractiveService : IToolInteractiveService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleInteractiveService"/> class.
    /// </summary>
    public ConsoleInteractiveService()
    {
        Console.Title = Constants.ProductName;
    }

    /// <inheritdoc/>
    public void WriteLine(string? message)
    {
        Console.WriteLine(message);
    }

    /// <inheritdoc/>
    public void WriteErrorLine(string? message)
    {
        var color = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = color;
        }
    }
}
