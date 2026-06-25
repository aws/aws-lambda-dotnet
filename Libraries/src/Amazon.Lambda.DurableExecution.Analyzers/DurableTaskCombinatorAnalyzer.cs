// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Amazon.Lambda.DurableExecution.Analyzers
{
    /// <summary>
    /// DE004 — flags <c>Task.WhenAll</c>/<c>Task.WhenAny</c> called with tasks produced by durable
    /// operations. This is advisory (Info): the combinators work correctly with durable tasks because
    /// operation IDs are allocated deterministically, but they bypass completion policies, concurrency
    /// limits, branch naming, and structured <c>IBatchResult</c> output, so <c>ParallelAsync</c> /
    /// <c>MapAsync</c> are preferred.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DurableTaskCombinatorAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(DurableDiagnostics.DurableTaskInTaskCombinator);

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

            if (!symbols.IsTaskCombinator(invocation.TargetMethod, out var friendlyName))
            {
                return;
            }

            // Gather the task expressions passed in and report if any is produced by a durable op.
            foreach (var argument in invocation.Arguments)
            {
                if (ArgumentContainsDurableTask(argument.Value, symbols))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DurableDiagnostics.DurableTaskInTaskCombinator,
                        invocation.Syntax.GetLocation(),
                        friendlyName));
                    return;
                }
            }
        }

        /// <summary>
        /// True if the argument value contains (directly, or via the local it references) at least one
        /// task produced by a durable-context operation.
        /// </summary>
        private static bool ArgumentContainsDurableTask(IOperation value, DurableKnownSymbols symbols)
        {
            // Strip params-array / collection conversions.
            value = Unwrap(value);

            // Inline: Task.WhenAll(ctx.StepAsync(a), ctx.StepAsync(b)) or new[] { ... } or [ ... ].
            foreach (var op in value.DescendantsAndSelf())
            {
                if (op is IInvocationOperation inv && IsDurableTaskProducer(inv, symbols))
                {
                    return true;
                }
            }

            // Indirect: Task[] tasks = ...; await Task.WhenAll(tasks). Follow a local to its
            // initializer and to assignments/Add calls in the enclosing method body.
            if (value is ILocalReferenceOperation localRef)
            {
                return LocalIsPopulatedWithDurableTasks(localRef, symbols);
            }

            return false;
        }

        private static IOperation Unwrap(IOperation op)
        {
            while (op is IConversionOperation conv)
            {
                op = conv.Operand;
            }

            return op;
        }

        private static bool IsDurableTaskProducer(IInvocationOperation invocation, DurableKnownSymbols symbols)
        {
            if (!symbols.IsDurableOperation(invocation.TargetMethod, out _, out _))
            {
                return false;
            }

            return invocation.Instance is not null && symbols.IsDurableContextType(invocation.Instance.Type);
        }

        /// <summary>
        /// Bounded scan: walks the enclosing method/lambda body for the declaration of the referenced
        /// local and any assignments or <c>List.Add</c> calls that store a durable task into it.
        /// </summary>
        private static bool LocalIsPopulatedWithDurableTasks(ILocalReferenceOperation localRef, DurableKnownSymbols symbols)
        {
            var local = localRef.Local;

            // Find the enclosing body operation that contains the WhenAll call.
            IOperation root = localRef;
            while (root.Parent is not null)
            {
                root = root.Parent;
            }

            foreach (var op in root.DescendantsAndSelf())
            {
                switch (op)
                {
                    case IVariableDeclaratorOperation decl
                        when SymbolEqualityComparer.Default.Equals(decl.Symbol, local)
                             && decl.Initializer is not null:
                        if (ContainsDurableTaskProducer(decl.Initializer.Value, symbols))
                        {
                            return true;
                        }

                        break;

                    case ISimpleAssignmentOperation assign
                        when TargetsLocal(assign.Target, local):
                        if (ContainsDurableTaskProducer(assign.Value, symbols))
                        {
                            return true;
                        }

                        break;

                    case IInvocationOperation addCall
                        when addCall.TargetMethod.Name == "Add"
                             && addCall.Instance is ILocalReferenceOperation listRef
                             && SymbolEqualityComparer.Default.Equals(listRef.Local, local):
                        foreach (var addArg in addCall.Arguments)
                        {
                            if (ContainsDurableTaskProducer(addArg.Value, symbols))
                            {
                                return true;
                            }
                        }

                        break;
                }
            }

            return false;
        }

        private static bool TargetsLocal(IOperation target, ILocalSymbol local)
        {
            // Direct local assignment, or element assignment tasks[i] = ...
            switch (Unwrap(target))
            {
                case ILocalReferenceOperation l:
                    return SymbolEqualityComparer.Default.Equals(l.Local, local);
                case IArrayElementReferenceOperation arr when arr.ArrayReference is ILocalReferenceOperation arrLocal:
                    return SymbolEqualityComparer.Default.Equals(arrLocal.Local, local);
                default:
                    return false;
            }
        }

        private static bool ContainsDurableTaskProducer(IOperation value, DurableKnownSymbols symbols)
        {
            foreach (var op in Unwrap(value).DescendantsAndSelf())
            {
                if (op is IInvocationOperation inv && IsDurableTaskProducer(inv, symbols))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
