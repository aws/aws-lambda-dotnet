// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Amazon.Lambda.DurableExecution.Analyzers
{
    /// <summary>
    /// DE002 — flags a durable operation invoked inside a step-wrapped delegate (a StepAsync body, a
    /// WaitForCallback submitter, or a WaitForCondition check) by capturing the outer durable context.
    /// In .NET a step delegate receives <c>IStepContext</c>, which exposes no durable operations, so
    /// the only way to compile a nested durable call is to capture the outer <c>IDurableContext</c> —
    /// which is exactly what this rule detects. Nesting durable operations requires
    /// <c>RunInChildContextAsync</c>.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NestedDurableOperationAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(DurableDiagnostics.NestedDurableOperationInsideStep);

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
                    return;
                }

                compilationStart.RegisterOperationAction(
                    ctx => AnalyzeInvocation(ctx, symbols),
                    OperationKind.Invocation);
            });
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context, DurableKnownSymbols symbols)
        {
            var invocation = (IInvocationOperation)context.Operation;

            // Is this call itself a durable operation?
            if (!symbols.IsDurableOperation(invocation.TargetMethod, out var nestedOpName, out _))
            {
                return;
            }

            // The receiver must be a durable context. Static/extension calls (Instance == null) and
            // non-durable receivers are ignored.
            if (invocation.Instance is null || !symbols.IsDurableContextType(invocation.Instance.Type))
            {
                return;
            }

            // Find the nearest enclosing durable delegate. We only care about step-wrapped bodies;
            // nesting inside a child-context / parallel / map branch is legitimate.
            var enclosing = DurableScope.FindNearestDurableDelegate(invocation, symbols);
            if (!enclosing.Found || enclosing.Role != DurableDelegateRole.StepWrapped)
            {
                return;
            }

            // The receiver must be captured from OUTSIDE this step delegate. A step delegate's own
            // parameter is IStepContext (not a durable context), so any durable-context receiver here
            // is necessarily captured — but we verify the symbol is not declared inside the delegate
            // to stay correct for child/parallel branches that legitimately receive their own context.
            var receiverSymbol = GetReferencedSymbol(invocation.Instance);
            if (receiverSymbol is not null)
            {
                var declaredInDelegate = DurableScope.CollectDeclaredSymbols(enclosing.Function);
                if (declaredInDelegate.Contains(receiverSymbol))
                {
                    return; // Receiver is local to the step delegate — not a captured outer context.
                }
            }

            var contextName = receiverSymbol?.Name ?? "context";
            var bodyKind = DescribeBody(enclosing.OperationName);

            context.ReportDiagnostic(Diagnostic.Create(
                DurableDiagnostics.NestedDurableOperationInsideStep,
                invocation.Syntax.GetLocation(),
                nestedOpName,
                contextName,
                bodyKind));
        }

        private static ISymbol? GetReferencedSymbol(IOperation receiver)
        {
            switch (receiver)
            {
                case ILocalReferenceOperation local:
                    return local.Local;
                case IParameterReferenceOperation parameter:
                    return parameter.Parameter;
                case IFieldReferenceOperation field:
                    return field.Field;
                default:
                    return null;
            }
        }

        private static string DescribeBody(string enclosingOperationName)
        {
            switch (enclosingOperationName)
            {
                case "WaitForConditionAsync":
                    return "condition-check";
                case "WaitForCallbackAsync":
                    return "callback-submitter";
                default:
                    return "step";
            }
        }
    }
}
