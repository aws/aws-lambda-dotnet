// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Amazon.Lambda.DurableExecution.Analyzers.Tests.AnalyzerVerifier<
    Amazon.Lambda.DurableExecution.Analyzers.NestedDurableOperationAnalyzer>;

namespace Amazon.Lambda.DurableExecution.Analyzers.Tests
{
    public class NestedDurableOperationAnalyzerTests
    {
        private const string Usings = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.DurableExecution;
";

        [Fact]
        public async Task DurableOp_InsideStepBody_ViaCapturedContext_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.StepAsync(async (s, ct) =>
        {
            await {|#0:context.WaitAsync(TimeSpan.FromSeconds(1))|};
        });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NestedDurableOperationInsideStep)
                .WithLocation(0).WithArguments("WaitAsync", "context", "step");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task NestedStepAsync_InsideStepBody_IsFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.StepAsync(async (s, ct) =>
        {
            await {|#0:context.StepAsync((s2, ct2) => Task.CompletedTask)|};
        });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NestedDurableOperationInsideStep)
                .WithLocation(0).WithArguments("StepAsync", "context", "step");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task DurableOp_InsideChildContext_OnChildContext_IsNotFlagged()
        {
            // Using the child's own context inside a child context is the SANCTIONED nesting pattern.
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.RunInChildContextAsync(async (child, ct) =>
        {
            await child.StepAsync((s, c) => Task.CompletedTask);
            await child.WaitAsync(TimeSpan.FromSeconds(1));
        });
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task DurableOp_DirectlyInWorkflowBody_IsNotFlagged()
        {
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.StepAsync((s, ct) => Task.CompletedTask);
        await context.WaitAsync(TimeSpan.FromSeconds(1));
    }
}";
            await Verify.VerifyAsync(source);
        }

        [Fact]
        public async Task DurableOp_InsideConditionCheck_ViaCapturedContext_IsFlagged()
        {
            // WaitForConditionAsync's check delegate is step-wrapped at runtime.
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.WaitForConditionAsync<int>(async (state, cc, ct) =>
        {
            await {|#0:context.WaitAsync(TimeSpan.FromSeconds(1))|};
            return state + 1;
        }, new WaitForConditionConfig<int>());
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NestedDurableOperationInsideStep)
                .WithLocation(0).WithArguments("WaitAsync", "context", "condition-check");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task DurableOp_InsideCallbackSubmitter_ViaCapturedContext_IsFlagged()
        {
            // WaitForCallbackAsync's submitter delegate is step-wrapped at runtime.
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.WaitForCallbackAsync<int>(async (callbackId, cc, ct) =>
        {
            await {|#0:context.StepAsync((s, c) => Task.CompletedTask)|};
        });
    }
}";
            var expected = Verify.Diagnostic(DurableDiagnostics.NestedDurableOperationInsideStep)
                .WithLocation(0).WithArguments("StepAsync", "context", "callback-submitter");
            await Verify.VerifyAsync(source, expected);
        }

        [Fact]
        public async Task ConfigureLogger_LikeMembers_NotTreatedAsDurableOps()
        {
            // Reading ExecutionContext (a property, not a durable op) inside a step is fine.
            var source = Usings + @"
class W
{
    public async Task Run(string input, IDurableContext context)
    {
        await context.StepAsync((s, ct) =>
        {
            var arn = context.ExecutionContext.DurableExecutionArn;
            return Task.FromResult(arn);
        });
    }
}";
            await Verify.VerifyAsync(source);
        }
    }
}
