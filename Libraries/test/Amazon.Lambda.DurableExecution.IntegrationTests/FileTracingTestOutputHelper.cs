// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// An <see cref="ITestOutputHelper"/> that forwards to an inner helper and ALSO
/// appends each line to an autoflushed file, so deploy/poll progress is visible
/// live (via <c>tail -f</c>) instead of only when the test completes — xUnit
/// buffers <see cref="ITestOutputHelper"/> output until then.
/// </summary>
/// <remarks>
/// Opt-in through the <c>DURABLE_INTEG_TRACE</c> environment variable:
/// set it to a file path, or to <c>1</c>/<c>true</c> for a default path under
/// the temp directory. Unset (the default) returns the original helper unchanged,
/// so there is no behavior change for normal runs.
/// </remarks>
internal sealed class FileTracingTestOutputHelper : ITestOutputHelper
{
    private readonly ITestOutputHelper _inner;
    private readonly string _path;
    private static readonly object FileLock = new();

    private FileTracingTestOutputHelper(ITestOutputHelper inner, string path)
    {
        _inner = inner;
        _path = path;
    }

    public static ITestOutputHelper MaybeWrap(ITestOutputHelper inner)
    {
        var trace = Environment.GetEnvironmentVariable("DURABLE_INTEG_TRACE");
        if (string.IsNullOrEmpty(trace))
            return inner;

        var path = trace is "1" or "true" or "TRUE"
            ? Path.Combine(Path.GetTempPath(), "durable-integ-trace.log")
            : trace;

        return new FileTracingTestOutputHelper(inner, path);
    }

    public void WriteLine(string message)
    {
        _inner.WriteLine(message);
        Append(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        _inner.WriteLine(format, args);
        Append(string.Format(format, args));
    }

    private void Append(string message)
    {
        // Serialize cross-thread writes; a single test class can build several
        // deployments concurrently (e.g. CreateWithDownstreamAsync). Swallow IO
        // errors — tracing must never fail a test.
        try
        {
            lock (FileLock)
            {
                File.AppendAllText(_path, $"{DateTime.UtcNow:HH:mm:ss} {message}{System.Environment.NewLine}");
            }
        }
        catch
        {
            // Best-effort diagnostics only.
        }
    }
}
