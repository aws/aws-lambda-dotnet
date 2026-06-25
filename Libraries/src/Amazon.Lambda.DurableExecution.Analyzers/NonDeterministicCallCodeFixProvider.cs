// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

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
    /// Code fix for DE001: wraps a non-deterministic expression in a step so its value is checkpointed
    /// (<c>await context.StepAsync((_, _) =&gt; Task.FromResult(E))</c>). Offered as a single-occurrence
    /// quick fix only — never a fix-all — because inserting a step shifts the position-derived
    /// operation IDs of subsequent durable calls, which would break replay for already-running
    /// executions. The fix is withheld for shapes it cannot safely rewrite.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NonDeterministicCallCodeFixProvider))]
    [Shared]
    public sealed class NonDeterministicCallCodeFixProvider : CodeFixProvider
    {
        private const string IDurableContextMetadataName = DurableKnownSymbols.IDurableContextMetadataName;

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(DurableDiagnostics.NonDeterministicCallOutsideStep.Id);

        /// <summary>
        /// Fix-all is intentionally disabled: inserting a step shifts the position-derived operation
        /// IDs of every subsequent durable call, which would break replay for in-flight executions.
        /// </summary>
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
            var expression = node as ExpressionSyntax ?? node.FirstAncestorOrSelf<ExpressionSyntax>();
            if (expression is null)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
            {
                return;
            }

            if (!IsSafelyWrappable(expression, semanticModel, context.CancellationToken, out var contextName))
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Wrap in {contextName}.StepAsync(...)",
                    createChangedDocument: ct => WrapInStepAsync(context.Document, root, expression, contextName, ct),
                    equivalenceKey: "WrapInStepAsync"),
                diagnostic);
        }

        private static bool IsSafelyWrappable(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            out string contextName)
        {
            contextName = "context";

            // Guard: the expression must not itself contain an await, out/ref/in argument, or be a
            // method group / lambda — wrapping any of those produces non-compiling or wrong code.
            if (expression.DescendantNodesAndSelf().Any(n => n is AwaitExpressionSyntax))
            {
                return false;
            }

            foreach (var argument in expression.DescendantNodes().OfType<ArgumentSyntax>())
            {
                if (!argument.RefKindKeyword.IsKind(SyntaxKind.None))
                {
                    return false;
                }
            }

            // The expression must produce a usable value (object creation of e.g. Random is flagged by
            // DE001 but is not meaningfully wrappable into a checkpointed value — skip it).
            if (expression is ObjectCreationExpressionSyntax)
            {
                return false;
            }

            var typeInfo = semanticModel.GetTypeInfo(expression, cancellationToken);
            if (typeInfo.Type is null || typeInfo.Type.TypeKind == TypeKind.Error || typeInfo.Type.SpecialType == SpecialType.System_Void)
            {
                return false;
            }

            // await is only legal inside an async context; require one.
            if (!IsInAsyncContext(expression))
            {
                return false;
            }

            // Find an in-scope IDurableContext to call StepAsync on.
            var ctxName = FindDurableContextName(expression, semanticModel, cancellationToken);
            if (ctxName is null)
            {
                return false;
            }

            contextName = ctxName;
            return true;
        }

        private static bool IsInAsyncContext(SyntaxNode node)
        {
            for (var current = node.Parent; current is not null; current = current.Parent)
            {
                switch (current)
                {
                    case MethodDeclarationSyntax m:
                        return m.Modifiers.Any(SyntaxKind.AsyncKeyword);
                    case LocalFunctionStatementSyntax lf:
                        return lf.Modifiers.Any(SyntaxKind.AsyncKeyword);
                    case AnonymousFunctionExpressionSyntax af:
                        return af.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
                }
            }

            return false;
        }

        private static string? FindDurableContextName(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var durableContextType = semanticModel.Compilation.GetTypeByMetadataName(IDurableContextMetadataName);
            if (durableContextType is null)
            {
                return null;
            }

            // Walk enclosing lambdas/methods looking for an IDurableContext-typed parameter.
            for (var current = expression.Parent; current is not null; current = current.Parent)
            {
                var parameters = current switch
                {
                    ParenthesizedLambdaExpressionSyntax pl => pl.ParameterList.Parameters,
                    SimpleLambdaExpressionSyntax sl => SyntaxFactory.SeparatedList(new[] { sl.Parameter }),
                    MethodDeclarationSyntax m => m.ParameterList.Parameters,
                    LocalFunctionStatementSyntax lf => lf.ParameterList.Parameters,
                    _ => default,
                };

                foreach (var parameter in parameters)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(parameter, cancellationToken) as IParameterSymbol;
                    if (symbol is not null && Implements(symbol.Type, durableContextType))
                    {
                        return symbol.Name;
                    }
                }
            }

            return null;
        }

        private static bool Implements(ITypeSymbol type, INamedTypeSymbol durableContextType)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, durableContextType))
            {
                return true;
            }

            return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, durableContextType));
        }

        private static Task<Document> WrapInStepAsync(
            Document document,
            SyntaxNode root,
            ExpressionSyntax expression,
            string contextName,
            CancellationToken cancellationToken)
        {
            // await {ctx}.StepAsync((_, _) => System.Threading.Tasks.Task.FromResult(E))
            var lambda = SyntaxFactory.ParenthesizedLambdaExpression(
                SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("_")),
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("__")),
                })),
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.ParseExpression("System.Threading.Tasks.Task.FromResult"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(expression.WithoutTrivia())))));

            var stepCall = SyntaxFactory.AwaitExpression(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(contextName),
                        SyntaxFactory.IdentifierName("StepAsync")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(lambda)))));

            var replacement = stepCall
                .WithLeadingTrivia(expression.GetLeadingTrivia())
                .WithTrailingTrivia(expression.GetTrailingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(expression, replacement);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }
    }
}
