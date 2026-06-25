// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Amazon.Lambda.DurableExecution.Analyzers
{
    /// <summary>
    /// Code fix for DE004: rewrites <c>await Task.WhenAll(ctx.StepAsync(a), ctx.StepAsync(b))</c> into
    /// <c>await ctx.ParallelAsync(new[] { (c, ct) =&gt; c.StepAsync(a), (c, ct) =&gt; c.StepAsync(b) })</c>
    /// for the one provably-safe shape: an inline list of direct durable calls on a single shared
    /// context whose aggregate result is discarded. Every other shape (result consumed, mixed task
    /// types, a variable instead of an inline list, WhenAny) is left diagnostic-only.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DurableTaskCombinatorCodeFixProvider))]
    [Shared]
    public sealed class DurableTaskCombinatorCodeFixProvider : CodeFixProvider
    {
        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(DurableDiagnostics.DurableTaskInTaskCombinator.Id);

        /// <summary>Fix-all is disabled — each conversion changes durable-operation structure and is reviewed individually.</summary>
        public override FixAllProvider? GetFixAllProvider() => null;

        /// <inheritdoc />
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }

            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation is null)
            {
                return;
            }

            // Only WhenAll (not WhenAny) and only when its result is discarded (the await is an
            // expression statement, not consumed by an assignment / argument / return).
            if (GetCombinatorName(invocation) != "WhenAll" || !ResultIsDiscarded(invocation))
            {
                return;
            }

            // All arguments must be inline durable calls of the SAME receiver identifier.
            if (!TryGetHomogeneousDurableCalls(invocation, out var receiver, out var calls))
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
            {
                return;
            }

            // Determine the shared result type T (each durable call returns Task<T>). Bail if the calls
            // are not all the same T — that is not the provably-safe homogeneous shape.
            var elementType = TryGetSharedResultType(calls, semanticModel, context.CancellationToken);
            if (elementType is null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Convert to {receiver}.ParallelAsync(...)",
                    createChangedDocument: ct => ConvertAsync(context.Document, root, invocation, receiver, calls, elementType, ct),
                    equivalenceKey: "ConvertToParallelAsync"),
                diagnostic);
        }

        private static string? TryGetSharedResultType(
            List<InvocationExpressionSyntax> calls,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            string? shared = null;
            foreach (var call in calls)
            {
                if (semanticModel.GetTypeInfo(call, cancellationToken).Type is not INamedTypeSymbol taskType
                    || taskType.TypeArguments.Length != 1)
                {
                    return null; // Non-generic Task (void step) — not handled by this fix.
                }

                var display = taskType.TypeArguments[0].ToDisplayString();
                if (shared is null)
                {
                    shared = display;
                }
                else if (shared != display)
                {
                    return null; // Heterogeneous result types — bail.
                }
            }

            return shared;
        }

        private static string? GetCombinatorName(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression switch
            {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
                _ => null,
            };
        }

        private static bool ResultIsDiscarded(InvocationExpressionSyntax invocation)
        {
            // The WhenAll invocation is typically wrapped in an await; the await must stand alone.
            SyntaxNode current = invocation;
            if (current.Parent is AwaitExpressionSyntax awaitExpr)
            {
                current = awaitExpr;
            }

            return current.Parent is ExpressionStatementSyntax;
        }

        private static bool TryGetHomogeneousDurableCalls(
            InvocationExpressionSyntax invocation,
            out string receiver,
            out List<InvocationExpressionSyntax> calls)
        {
            receiver = string.Empty;
            calls = new List<InvocationExpressionSyntax>();

            var arguments = invocation.ArgumentList.Arguments;
            if (arguments.Count < 2)
            {
                return false; // Nothing to parallelize.
            }

            string? sharedReceiver = null;
            foreach (var argument in arguments)
            {
                if (argument.Expression is not InvocationExpressionSyntax call
                    || call.Expression is not MemberAccessExpressionSyntax memberAccess
                    || memberAccess.Expression is not IdentifierNameSyntax receiverId)
                {
                    return false; // Not a direct ctx.Method(...) call.
                }

                if (sharedReceiver is null)
                {
                    sharedReceiver = receiverId.Identifier.ValueText;
                }
                else if (sharedReceiver != receiverId.Identifier.ValueText)
                {
                    return false; // Mixed receivers — bail.
                }

                calls.Add(call);
            }

            receiver = sharedReceiver!;
            return true;
        }

        private static Task<Document> ConvertAsync(
            Document document,
            SyntaxNode root,
            InvocationExpressionSyntax whenAll,
            string receiver,
            List<InvocationExpressionSyntax> calls,
            string elementType,
            CancellationToken cancellationToken)
        {
            // Build one branch lambda per call: (c, ct) => c.StepAsync(...originalArgs...)
            var branchLambdas = calls.Select(call =>
            {
                var memberAccess = (MemberAccessExpressionSyntax)call.Expression;
                var rebound = call.WithExpression(
                    memberAccess.WithExpression(SyntaxFactory.IdentifierName("c")));

                return (ExpressionSyntax)SyntaxFactory.ParenthesizedLambdaExpression(
                    SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("c")),
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("ct")),
                    })),
                    rebound);
            }).ToArray();

            // Explicitly-typed array — a lambda has no inferable type, so `new[] { … }` would not
            // compile; emit `new Func<IDurableContext, CancellationToken, Task<T>>[] { … }`.
            var arrayType = SyntaxFactory.ArrayType(
                SyntaxFactory.ParseTypeName(
                    $"System.Func<Amazon.Lambda.DurableExecution.IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task<{elementType}>>"),
                SyntaxFactory.SingletonList(
                    SyntaxFactory.ArrayRankSpecifier(
                        SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                            SyntaxFactory.OmittedArraySizeExpression()))));

            var arrayLiteral = SyntaxFactory.ArrayCreationExpression(
                arrayType,
                SyntaxFactory.InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SyntaxFactory.SeparatedList(branchLambdas)));

            var parallelCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(receiver),
                    SyntaxFactory.IdentifierName("ParallelAsync")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(arrayLiteral))))
                .WithTriviaFrom(whenAll)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(whenAll, parallelCall);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }
    }
}
