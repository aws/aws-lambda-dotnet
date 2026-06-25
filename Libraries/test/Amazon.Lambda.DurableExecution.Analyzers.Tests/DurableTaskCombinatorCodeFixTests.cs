// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Amazon.Lambda.DurableExecution.Analyzers.Tests.CodeFixVerifier<
    Amazon.Lambda.DurableExecution.Analyzers.DurableTaskCombinatorAnalyzer,
    Amazon.Lambda.DurableExecution.Analyzers.DurableTaskCombinatorCodeFixProvider>;

namespace Amazon.Lambda.DurableExecution.Analyzers.Tests
{
    public class DurableTaskCombinatorCodeFixTests
    {
        private const string Usings = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.DurableExecution;
";

        [Fact]
        public async Task ConvertsWhenAll_ToParallelAsync_WhenResultDiscarded()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await {|#0:Task.WhenAll(
            context.StepAsync((s, ct) => Task.FromResult(1)),
            context.StepAsync((s, ct) => Task.FromResult(2)))|};
    }
}";
            var fixedSource = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.ParallelAsync(new System.Func<Amazon.Lambda.DurableExecution.IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task<int>>[] { (c, ct) => c.StepAsync((s, ct) => Task.FromResult(1)), (c, ct) => c.StepAsync((s, ct) => Task.FromResult(2)) });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.DurableTaskInTaskCombinator)
                .WithLocation(0).WithArguments("Task.WhenAll");
            await Verify.VerifyAsync(source, fixedSource, expected);
        }
    }
}
