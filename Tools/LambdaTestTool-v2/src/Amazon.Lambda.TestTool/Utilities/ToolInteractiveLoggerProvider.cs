// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Amazon.Lambda.TestTool.Services;

namespace Amazon.Lambda.TestTool.Utilities;

internal class ToolInteractiveLoggerProvider(IToolInteractiveService toolInteractiveSerivce) : ILoggerProvider
{
    private Logger _logger = new Logger(toolInteractiveSerivce);
    public ILogger CreateLogger(string categoryName) => _logger;
    public void Dispose() { }

    class Logger(IToolInteractiveService toolInteractiveSerivce) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
             where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            toolInteractiveSerivce.WriteLine(message);
        }
    }
}
