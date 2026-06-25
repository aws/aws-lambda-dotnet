// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.DurableExecution.Analyzers
{
    /// <summary>
    /// Diagnostic descriptors for the durable-execution analyzers. These mirror the
    /// JavaScript SDK's ESLint plugin rules (no-non-deterministic-outside-step,
    /// no-nested-durable-operations, no-closure-in-durable-operations) and add a
    /// .NET-specific rule (DE004) for the <c>Task.WhenAll</c>/<c>WhenAny</c> pattern.
    /// </summary>
    public static class DurableDiagnostics
    {
        internal const string Category = "AWSLambdaDurableExecution";

        private const string HelpRoot =
            "https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.DurableExecution/docs/analyzers.md";

        /// <summary>
        /// DE001 — a non-deterministic API (DateTime.Now, Guid.NewGuid, Random, …) is used in
        /// workflow code outside a step. On replay the workflow re-runs from the top, so the value
        /// differs between the original run and replays, corrupting checkpoint-derived state.
        /// </summary>
        public static readonly DiagnosticDescriptor NonDeterministicCallOutsideStep = new DiagnosticDescriptor(
            id: "DE001",
            title: "Non-deterministic call outside a step",
            messageFormat: "Non-deterministic operation '{0}' is used in workflow code outside a step. Move it inside a step (e.g. context.StepAsync(...)) so its result is checkpointed and replays consistently",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Durable workflow code re-executes from the top on every invocation. Values from non-deterministic APIs differ between the original execution and replays unless they are captured inside a step.",
            helpLinkUri: HelpRoot + "#de001");

        /// <summary>
        /// DE002 — a durable operation is invoked inside a step body by capturing the outer
        /// <c>IDurableContext</c>. Step bodies must contain only plain, deterministic code;
        /// nesting durable operations requires RunInChildContextAsync.
        /// </summary>
        public static readonly DiagnosticDescriptor NestedDurableOperationInsideStep = new DiagnosticDescriptor(
            id: "DE002",
            title: "Nested durable operation inside a step body",
            messageFormat: "Durable operation '{0}' is called on the outer durable context '{1}' inside a {2} body. Step bodies must contain only plain, deterministic code; nest durable operations with context.RunInChildContextAsync(...) instead",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A step body is replayed verbatim on every retry. Calling another durable operation from inside it produces unpredictable behavior; use RunInChildContextAsync to group durable operations.",
            helpLinkUri: HelpRoot + "#de002");

        /// <summary>
        /// DE003 — a variable captured from an outer scope is mutated inside a durable-operation
        /// delegate. On replay the operation returns its cached result without re-executing the body,
        /// so the write never happens and the captured variable holds stale state.
        /// </summary>
        public static readonly DiagnosticDescriptor MutableCaptureInDurableOperation = new DiagnosticDescriptor(
            id: "DE003",
            title: "Mutable variable captured and modified inside a durable operation",
            messageFormat: "Variable '{0}' is captured from an outer scope and modified inside a durable operation. On replay the operation returns its cached result without re-executing the body, so this write is lost and '{0}' becomes stale. Return the value from the operation and assign it (e.g. {0} = await context.StepAsync(...)) instead",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Reading a captured variable inside a durable operation is safe; mutating it is not, because the body is skipped on replay.",
            helpLinkUri: HelpRoot + "#de003");

        /// <summary>
        /// DE004 — <c>Task.WhenAll</c>/<c>Task.WhenAny</c> is called with tasks produced by durable
        /// operations. This is not incorrect (operation IDs are allocated deterministically), but it
        /// bypasses completion policies, concurrency limits, branch naming, and IBatchResult output.
        /// Advisory (Info) only.
        /// </summary>
        public static readonly DiagnosticDescriptor DurableTaskInTaskCombinator = new DiagnosticDescriptor(
            id: "DE004",
            title: "Prefer ParallelAsync/MapAsync over Task.WhenAll/WhenAny for durable tasks",
            messageFormat: "'{0}' over durable tasks bypasses completion policies, concurrency limits, branch naming, and IBatchResult output. Use context.ParallelAsync (or MapAsync) so concurrent durable operations get framework coordination and complete execution traces",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Task.WhenAll/WhenAny work correctly with durable tasks, but ParallelAsync/MapAsync are preferred for completion policies, concurrency control, and observability.",
            helpLinkUri: HelpRoot + "#de004");
    }
}
