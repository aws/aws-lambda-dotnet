// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Amazon.Lambda.DurableExecution.Analyzers
{
    /// <summary>
    /// Shared helpers for reasoning about where an operation sits relative to durable-operation
    /// delegates. Used by DE001 (is this non-deterministic call inside a step?) and DE002 (is this
    /// durable call inside a step body?).
    /// </summary>
    internal static class DurableScope
    {
        /// <summary>
        /// Describes the nearest enclosing durable-operation delegate of an operation, if any.
        /// </summary>
        internal readonly struct EnclosingDelegate
        {
            internal EnclosingDelegate(DurableDelegateRole role, string operationName, IAnonymousFunctionOperation function)
            {
                Role = role;
                OperationName = operationName;
                Function = function;
            }

            internal DurableDelegateRole Role { get; }

            internal string OperationName { get; }

            internal IAnonymousFunctionOperation Function { get; }

            internal bool Found => Role != DurableDelegateRole.None;
        }

        /// <summary>
        /// Walks up the operation tree from <paramref name="operation"/> and returns the nearest
        /// enclosing lambda that is an argument to a durable operation, classified by its role.
        /// Non-durable lambdas (e.g. a <c>Select</c> projection or <c>Task.Run</c>) are skipped so a
        /// non-deterministic call nested in such a lambda is still attributed to the durable step that
        /// contains it.
        /// </summary>
        internal static EnclosingDelegate FindNearestDurableDelegate(IOperation operation, DurableKnownSymbols symbols)
        {
            for (var current = operation.Parent; current is not null; current = current.Parent)
            {
                if (current is IAnonymousFunctionOperation lambda
                    && TryClassifyDelegate(lambda, symbols, out var role, out var opName))
                {
                    return new EnclosingDelegate(role, opName, lambda);
                }
            }

            return default;
        }

        /// <summary>
        /// True if any enclosing scope (method, local function, or lambda) of
        /// <paramref name="operation"/> is durable workflow code — i.e. declares an
        /// <c>IDurableContext</c> parameter. This is the single shared scoping primitive: it covers
        /// the [DurableExecution] annotation path, the hand-wired
        /// <c>DurableFunction.WrapAsync(async (input, ctx) =&gt; …)</c> lambda, and child/parallel/map
        /// branch delegates.
        /// </summary>
        internal static bool IsInWorkflowCode(IOperation operation, DurableKnownSymbols symbols)
        {
            for (var current = operation.Parent; current is not null; current = current.Parent)
            {
                switch (current)
                {
                    case IAnonymousFunctionOperation lambda
                        when symbols.HasDurableContextParameter(lambda.Symbol.Parameters):
                        return true;
                    case ILocalFunctionOperation local
                        when symbols.HasDurableContextParameter(local.Symbol.Parameters):
                        return true;
                    case IMethodBodyOperation:
                    case IBlockOperation { Parent: null }:
                        break;
                }
            }

            // The enclosing method symbol (top-level body, not reachable as an IOperation parent).
            var enclosing = operation.SemanticModel?.GetEnclosingSymbol(operation.Syntax.SpanStart) as IMethodSymbol;
            while (enclosing is not null)
            {
                if (symbols.HasDurableContextParameter(enclosing.Parameters))
                {
                    return true;
                }

                enclosing = enclosing.ContainingSymbol as IMethodSymbol;
            }

            return false;
        }

        /// <summary>
        /// If <paramref name="lambda"/> is passed as an argument to a durable operation, reports which
        /// operation and the delegate's role. Maps the lambda's argument position to the corresponding
        /// parameter so the WaitForCallbackAsync submitter / WaitForConditionAsync check (which are
        /// step-wrapped) and the ParallelAsync/MapAsync branches (child context) are classified
        /// correctly — even though they are not the operation's "first" delegate.
        /// </summary>
        internal static bool TryClassifyDelegate(
            IAnonymousFunctionOperation lambda,
            DurableKnownSymbols symbols,
            out DurableDelegateRole role,
            out string operationName)
        {
            role = DurableDelegateRole.None;
            operationName = string.Empty;

            // The lambda may be a direct argument (StepAsync(lambda)), wrapped in a delegate
            // conversion, or nested inside the array/collection that builds the branches list of
            // ParallelAsync ([ (c, ct) => …, (c, ct) => … ]). Walk up through those expression
            // wrappers to the enclosing argument, but stop at any statement, block, or another
            // anonymous function so we never attribute the lambda to a non-enclosing invocation.
            // This avoids naming ICollectionExpressionOperation, which is not in all Roslyn versions.
            IOperation? argument = lambda.Parent;
            while (argument is not null
                   && argument is not IArgumentOperation
                   && argument is not IAnonymousFunctionOperation
                   && argument is not IBlockOperation
                   && argument is not IExpressionStatementOperation)
            {
                argument = argument.Parent;
            }

            if (argument is not IArgumentOperation arg || arg.Parent is not IInvocationOperation invocation)
            {
                return false;
            }

            if (!symbols.IsDurableOperation(invocation.TargetMethod, out operationName, out role))
            {
                role = DurableDelegateRole.None;
                operationName = string.Empty;
                return false;
            }

            return role != DurableDelegateRole.None;
        }

        /// <summary>
        /// Collects the symbols (parameters and locals) declared within <paramref name="function"/>,
        /// including nested lambdas/local functions, so DE003 can tell a captured outer variable from a
        /// delegate-local one.
        /// </summary>
        internal static HashSet<ISymbol> CollectDeclaredSymbols(IAnonymousFunctionOperation function)
        {
            // The comparer IS supplied; this suppresses a known RS1024 false positive in the 4.0.1
            // analyzer pack that flags `new HashSet<ISymbol>(SymbolEqualityComparer.Default)`.
#pragma warning disable RS1024 // Compare symbols correctly
            var declared = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
#pragma warning restore RS1024

            foreach (var p in function.Symbol.Parameters)
            {
                declared.Add(p);
            }

            CollectDescendantDeclarations(function.Body, declared);
            return declared;
        }

        private static void CollectDescendantDeclarations(IOperation? operation, HashSet<ISymbol> declared)
        {
            if (operation is null)
            {
                return;
            }

            foreach (var child in operation.Descendants())
            {
                switch (child)
                {
                    case IVariableDeclaratorOperation decl:
                        declared.Add(decl.Symbol);
                        break;
                    case IAnonymousFunctionOperation nested:
                        foreach (var p in nested.Symbol.Parameters)
                        {
                            declared.Add(p);
                        }

                        break;
                    case ILocalFunctionOperation localFn:
                        foreach (var p in localFn.Symbol.Parameters)
                        {
                            declared.Add(p);
                        }

                        break;
                }
            }
        }
    }
}
