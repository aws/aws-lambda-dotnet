// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Amazon.Lambda.DurableExecution.Analyzers.Tests.AnalyzerVerifier<
    Amazon.Lambda.DurableExecution.Analyzers.MutableCaptureAnalyzer>;

namespace Amazon.Lambda.DurableExecution.Analyzers.Tests
{
    public class MutableCaptureAnalyzerTests
    {
        private const string Usings = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.DurableExecution;
";

        [Fact]
        public async Task SimpleAssignment_OfCapturedVariable_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        int counter = 0;
        await context.StepAsync((s, ct) => { {|#0:counter|} = 5; return Task.CompletedTask; });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.MutableCaptureInDurableOperation)
                .WithLocation(0).WithArguments("counter");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task CompoundAssignment_OfCapturedVariable_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        int total = 0;
        await context.StepAsync((s, ct) => { {|#0:total|} += 1; return Task.CompletedTask; });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.MutableCaptureInDurableOperation)
                .WithLocation(0).WithArguments("total");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task Increment_OfCapturedVariable_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        int n = 0;
        await context.StepAsync((s, ct) => { {|#0:n|}++; return Task.CompletedTask; });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.MutableCaptureInDurableOperation)
                .WithLocation(0).WithArguments("n");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task ReadingCapturedVariable_IsNotFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        int seed = 41;
        await context.StepAsync((s, ct) => Task.FromResult(seed + 1));
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task MutatingDelegateLocal_IsNotFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.StepAsync((s, ct) =>
        {
            int local = 0;
            local += 1;
            return Task.FromResult(local);
        });
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task ShadowingLocal_IsNotFlagged()
        {
            // An inner local that shadows an outer name is delegate-local by symbol identity.
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        int total = 100;
        await context.StepAsync((s, ct) =>
        {
            int total2 = 0;
            total2 += 1;
            return Task.FromResult(total2);
        });
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task MutatingCapturedVariable_InChildContext_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        int flag = 0;
        await context.RunInChildContextAsync((child, ct) => { {|#0:flag|} = 1; return Task.CompletedTask; });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.MutableCaptureInDurableOperation)
                .WithLocation(0).WithArguments("flag");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task MutatingPerItemAndBranchLocals_InParallel_IsNotFlagged()
        {
            // The branch delegate's own parameters and locals are not captures.
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.ParallelAsync(new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            (branch, ct) => { int x = 0; x += 1; return Task.FromResult(x); },
            (branch, ct) => Task.FromResult(2),
        });
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task MutatingCapturedVariable_FromParallelBranch_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        int shared = 0;
        await context.ParallelAsync(new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            (branch, ct) => { {|#0:shared|} += 1; return Task.FromResult(shared); },
        });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.MutableCaptureInDurableOperation)
                .WithLocation(0).WithArguments("shared");
            await Verify.VerifyAsync(source, expected);
        }
    }
}
