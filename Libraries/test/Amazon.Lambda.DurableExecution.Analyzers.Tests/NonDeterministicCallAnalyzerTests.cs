// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Amazon.Lambda.DurableExecution.Analyzers.Tests.AnalyzerVerifier<
    Amazon.Lambda.DurableExecution.Analyzers.NonDeterministicCallAnalyzer>;

namespace Amazon.Lambda.DurableExecution.Analyzers.Tests
{
    public class NonDeterministicCallAnalyzerTests
    {
        private const string Usings = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.DurableExecution;
";

        [Fact]
        public async Task DateTimeNow_InWorkflowBody_OutsideStep_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        var now = {|#0:DateTime.Now|};
        await context.StepAsync((s, ct) => Task.FromResult(now));
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NonDeterministicCallOutsideStep)
                .WithLocation(0).WithArguments("DateTime.Now");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task DateTimeUtcNow_InsideStep_IsNotFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.StepAsync((s, ct) => Task.FromResult(DateTime.UtcNow));
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task GuidNewGuid_OutsideStep_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        var id = {|#0:Guid.NewGuid()|};
        await context.StepAsync((s, ct) => Task.FromResult(id.ToString()));
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NonDeterministicCallOutsideStep)
                .WithLocation(0).WithArguments("Guid.NewGuid()");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task NewRandom_OutsideStep_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        var r = {|#0:new Random()|};
        await context.StepAsync((s, ct) => Task.FromResult(r.Next()));
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NonDeterministicCallOutsideStep)
                .WithLocation(0).WithArguments("new Random()");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task SeededRandom_IsNotFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        var r = new Random(42);
        await context.StepAsync((s, ct) => Task.FromResult(r.Next()));
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task RandomShared_OutsideStep_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        var n = {|#0:Random.Shared|}.Next();
        await context.StepAsync((s, ct) => Task.FromResult(n));
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NonDeterministicCallOutsideStep)
                .WithLocation(0).WithArguments("Random.Shared");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task NonDeterminism_NestedInNonDurableLambdaInsideStep_IsNotFlagged()
        {
            // DateTime.UtcNow inside a LINQ projection that itself runs inside a step is fine —
            // the enclosing durable delegate (StepAsync) is step-wrapped.
            var source = Usings + @"
using System.Linq;
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.StepAsync((s, ct) =>
        {
            var times = Enumerable.Range(0, 3).Select(x => DateTime.UtcNow).ToArray();
            return Task.FromResult(times.Length);
        });
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task DateTimeNow_InNonWorkflowMethod_IsNotFlagged()
        {
            // A method with no IDurableContext parameter is not workflow code.
            var source = Usings + @"
class W
{
    public DateTime Helper() => DateTime.Now;
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task DateTimeNow_InsideChildContextBranch_IsFlagged()
        {
            // A child-context body is still workflow code that must be deterministic.
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.RunInChildContextAsync(async (child, ct) =>
        {
            var now = {|#0:DateTime.Now|};
            await child.StepAsync((s, c) => Task.FromResult(now));
        });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NonDeterministicCallOutsideStep)
                .WithLocation(0).WithArguments("DateTime.Now");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task UnrelatedStepMethodOnOtherType_DoesNotSuppress()
        {
            // Semantic matching: an unrelated type's StepAsync must NOT be treated as a durable step,
            // so it does not suppress non-determinism. DateTime.Now nested in it is still workflow code
            // outside any real durable step, so it IS flagged.
            var source = Usings + @"
class NotDurable { public Task<DateTime> StepAsync(Func<Task<DateTime>> f) => f(); }
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        var other = new NotDurable();
        await other.StepAsync(() => Task.FromResult({|#0:DateTime.Now|}));
        await context.StepAsync((s, ct) => Task.CompletedTask);
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NonDeterministicCallOutsideStep)
                .WithLocation(0).WithArguments("DateTime.Now");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task UnrelatedTypeProducesNoDiagnostic_OutsideWorkflow()
        {
            // The same unrelated StepAsync, but in a method with no IDurableContext — not workflow code.
            var source = Usings + @"
class NotDurable { public Task<DateTime> StepAsync(Func<Task<DateTime>> f) => f(); }
class W
{
    public async Task Helper()
    {
        var other = new NotDurable();
        await other.StepAsync(() => Task.FromResult(DateTime.Now));
    }
}";
            await Verify.VerifyAsync(source);
        }
    }
}
