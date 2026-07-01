// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Amazon.Lambda.DurableExecution.Analyzers.Tests.CodeFixVerifier<
    Amazon.Lambda.DurableExecution.Analyzers.NonDeterministicCallAnalyzer,
    Amazon.Lambda.DurableExecution.Analyzers.NonDeterministicCallCodeFixProvider>;

namespace Amazon.Lambda.DurableExecution.Analyzers.Tests
{
    public class NonDeterministicCallCodeFixTests
    {
        private const string Usings = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.DurableExecution;
";

        [Fact]
        public async Task WrapsDateTimeNow_InStepAsync()
        {
            var source = Usings + @"
class W
{
    public async Task<DateTime> Run(string input, IDurableContext context)
    {
        var now = {|#0:DateTime.UtcNow|};
        return now;
    }
}";
            var fixedSource = Usings + @"
class W
{
    public async Task<DateTime> Run(string input, IDurableContext context)
    {
        var now = await context.StepAsync((_, __) => System.Threading.Tasks.Task.FromResult(DateTime.UtcNow));
        return now;
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NonDeterministicCallOutsideStep)
                .WithLocation(0).WithArguments("DateTime.UtcNow");
            await Verify.VerifyAsync(source, fixedSource, expected);
        }
    }
}
