// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers
{
    /// <summary>
    /// Capturing implementation of <see cref="IConsoleLoggerWriter"/> used by tests to assert what was logged
    /// without touching the real console writers.
    /// </summary>
    internal class CapturingConsoleLoggerWriter : IConsoleLoggerWriter
    {
        public List<(string Level, string Message, object[] Args)> Writes { get; } = new();

        public void SetRuntimeHeaders(IRuntimeApiHeaders runtimeApiHeaders) { }

        public void FormattedWriteLine(string message) => Writes.Add((null, message, null));

        public void FormattedWriteLine(string level, string message, params object[] args) => Writes.Add((level, message, args));

        public void FormattedWriteLine(string level, Exception exception, string message, params object[] args) => Writes.Add((level, message, args));
    }
}
