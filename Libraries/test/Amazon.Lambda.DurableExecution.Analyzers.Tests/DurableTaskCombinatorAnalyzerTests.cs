// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Amazon.Lambda.DurableExecution.Analyzers.Tests.AnalyzerVerifier<
    Amazon.Lambda.DurableExecution.Analyzers.DurableTaskCombinatorAnalyzer>;

namespace Amazon.Lambda.DurableExecution.Analyzers.Tests
{
    public class DurableTaskCombinatorAnalyzerTests
    {
        private const string Usings = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.DurableExecution;
";

        [Fact]
        public async Task WhenAll_OverInlineDurableTasks_IsFlagged()
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
            var expected = Verify.Diagnostic(DurableDiagnostics.DurableTaskInTaskCombinator)
                .WithLocation(0).WithArguments("Task.WhenAll");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task WhenAny_OverInlineDurableTasks_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await {|#0:Task.WhenAny(
            context.StepAsync((s, ct) => Task.FromResult(1)),
            context.StepAsync((s, ct) => Task.FromResult(2)))|};
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.DurableTaskInTaskCombinator)
                .WithLocation(0).WithArguments("Task.WhenAny");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task WhenAll_OverTaskArrayLocal_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        var tasks = new List<Task<int>>();
        tasks.Add(context.StepAsync((s, ct) => Task.FromResult(1)));
        await {|#0:Task.WhenAll(tasks)|};
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.DurableTaskInTaskCombinator)
                .WithLocation(0).WithArguments("Task.WhenAll");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task WhenAll_OverNonDurableTasks_IsNotFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await Task.WhenAll(Task.Delay(1), Task.Delay(2));
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task ParallelAsync_IsNotFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.ParallelAsync(new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            (c, ct) => c.StepAsync((s, t) => Task.FromResult(1)),
            (c, ct) => c.StepAsync((s, t) => Task.FromResult(2)),
        });
    }
}";
            await Verify.VerifyAsync(source);
        }
    }
}
