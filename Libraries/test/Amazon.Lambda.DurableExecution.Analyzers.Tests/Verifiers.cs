// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Amazon.Lambda.DurableExecution.Analyzers.Tests
{
    /// <summary>
    /// Analyzer-only verifier that injects the durable stub surface into every test compilation and
    /// targets the net8.0 reference assemblies.
    /// </summary>
    internal static class AnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        internal static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor) =>
            new DiagnosticResult(descriptor);

        internal static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new Test { TestCode = source };
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync(CancellationToken.None);
        }

        private sealed class Test : CSharpAnalyzerTest<TAnalyzer, XUnitVerifier>
        {
            public Test()
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
                TestState.Sources.Add(DurableStubs.Source);
            }
        }
    }

    /// <summary>
    /// Code-fix verifier mirroring <see cref="AnalyzerVerifier{TAnalyzer}"/> with the stub injected
    /// into both the pre-fix and post-fix compilations.
    /// </summary>
    internal static class CodeFixVerifier<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        internal static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor) =>
            new DiagnosticResult(descriptor);

        internal static Task VerifyAsync(string source, string fixedSource, params DiagnosticResult[] expected)
        {
            var test = new Test
            {
                TestCode = source,
                FixedCode = fixedSource,
            };
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync(CancellationToken.None);
        }

        private sealed class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, XUnitVerifier>
        {
            public Test()
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
                TestState.Sources.Add(DurableStubs.Source);
                FixedState.Sources.Add(DurableStubs.Source);
            }
        }
    }
}
