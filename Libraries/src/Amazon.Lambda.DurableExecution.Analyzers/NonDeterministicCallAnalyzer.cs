// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Amazon.Lambda.DurableExecution.Analyzers
{
    /// <summary>
    /// DE001 — flags non-deterministic API usage (DateTime.Now, Guid.NewGuid(), Random, …) in
    /// durable workflow code that is not inside a step. On replay the workflow re-runs from the top,
    /// so such values differ between the original execution and replays. The sanctioned place for
    /// non-determinism is inside a step (or a WaitForCallback submitter / WaitForCondition check,
    /// which the SDK also runs inside a step), where the result is checkpointed.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NonDeterministicCallAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(DurableDiagnostics.NonDeterministicCallOutsideStep);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStart =>
            {
                var symbols = DurableKnownSymbols.TryCreate(compilationStart.Compilation);
                if (symbols is null)
                {
                    return; // Project does not reference the durable SDK; nothing to analyze.
                }

                compilationStart.RegisterOperationAction(
                    ctx => AnalyzeOperation(ctx, symbols),
                    OperationKind.PropertyReference,
                    OperationKind.Invocation,
                    OperationKind.ObjectCreation);
            });
        }

        private static void AnalyzeOperation(OperationAnalysisContext context, DurableKnownSymbols symbols)
        {
            var operation = context.Operation;

            var api = symbols.TryGetNonDeterministicApi(operation);
            if (api is null)
            {
                return;
            }

            // Only flag inside durable workflow code (a method/lambda taking an IDurableContext).
            if (!DurableScope.IsInWorkflowCode(operation, symbols))
            {
                return;
            }

            // Suppress when the nearest enclosing durable delegate is step-wrapped — non-determinism
            // is allowed (and expected) there because its result is checkpointed.
            var enclosing = DurableScope.FindNearestDurableDelegate(operation, symbols);
            if (enclosing.Found && enclosing.Role == DurableDelegateRole.StepWrapped)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DurableDiagnostics.NonDeterministicCallOutsideStep,
                operation.Syntax.GetLocation(),
                api));
        }
    }
}
