// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Amazon.Lambda.DurableExecution.Analyzers
{
    /// <summary>
    /// DE003 — flags mutation of a variable captured from an outer scope inside a durable-operation
    /// delegate (StepAsync, RunInChildContextAsync, WaitForConditionAsync, WaitForCallbackAsync, and
    /// the ParallelAsync / MapAsync branches). On replay the delegate body is skipped and the cached
    /// result returned, so the write never happens and the captured variable holds stale state.
    /// Reading a captured variable is safe and not flagged.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MutableCaptureAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(DurableDiagnostics.MutableCaptureInDurableOperation);

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

            if (!symbols.IsDurableOperation(invocation.TargetMethod, out _, out _))
            {
                return;
            }

            if (invocation.Instance is null || !symbols.IsDurableContextType(invocation.Instance.Type))
            {
                return;
            }

            // Each delegate argument is analyzed independently. ParallelAsync takes a list of branch
            // delegates, so a single invocation can carry several lambdas (nested in array/collection
            // wrappers). We collect only the OUTERMOST lambdas (the actual step/branch delegates);
            // a lambda nested inside a delegate body is covered by that delegate's recursive analysis,
            // so collecting it again would double-report the same captured write.
            foreach (var argument in invocation.Arguments)
            {
                foreach (var lambda in OutermostLambdas(argument))
                {
                    AnalyzeDelegate(context, lambda);
                }
            }
        }

        private static IEnumerable<IAnonymousFunctionOperation> OutermostLambdas(IOperation root)
        {
            foreach (var op in root.Descendants())
            {
                // Keep a lambda only if no other lambda sits between it and the argument root, so we
                // return the branch/step delegates themselves, not lambdas nested inside their bodies.
                if (op is IAnonymousFunctionOperation lambda && !HasEnclosingLambdaBelow(lambda, root))
                {
                    yield return lambda;
                }
            }
        }

        private static bool HasEnclosingLambdaBelow(IOperation operation, IOperation root)
        {
            for (var parent = operation.Parent; parent is not null && !ReferenceEquals(parent, root); parent = parent.Parent)
            {
                if (parent is IAnonymousFunctionOperation)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AnalyzeDelegate(OperationAnalysisContext context, IAnonymousFunctionOperation lambda)
        {
            var declaredInDelegate = DurableScope.CollectDeclaredSymbols(lambda);

            foreach (var descendant in lambda.Body.Descendants())
            {
                var targetOp = GetAssignmentTargetOperation(descendant);
                if (targetOp is null)
                {
                    continue;
                }

                var target = GetReferencedSymbol(targetOp);
                if (target is null)
                {
                    continue;
                }

                // Captured iff the mutated symbol is not declared within this delegate (or a lambda
                // nested inside it). Reads never reach here because only assignment targets are
                // collected, so reading a captured variable is inherently allowed.
                if (!declaredInDelegate.Contains(target))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DurableDiagnostics.MutableCaptureInDurableOperation,
                        targetOp.Syntax.GetLocation(),
                        target.Name));
                }
            }
        }

        /// <summary>
        /// Returns the target operation being mutated by an assignment, compound assignment, coalesce
        /// assignment, or increment/decrement; otherwise <c>null</c>.
        /// </summary>
        private static IOperation? GetAssignmentTargetOperation(IOperation operation)
        {
            return operation switch
            {
                ISimpleAssignmentOperation simple => simple.Target,
                ICompoundAssignmentOperation compound => compound.Target,
                ICoalesceAssignmentOperation coalesce => coalesce.Target,
                IIncrementOrDecrementOperation incDec => incDec.Target,
                _ => null,
            };
        }

        private static ISymbol? GetReferencedSymbol(IOperation targetOp)
        {
            return targetOp switch
            {
                ILocalReferenceOperation local => local.Local,
                IParameterReferenceOperation parameter => parameter.Parameter,
                _ => null,
            };
        }
    }
}
