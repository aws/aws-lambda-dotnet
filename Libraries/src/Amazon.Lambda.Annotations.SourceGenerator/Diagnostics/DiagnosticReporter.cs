using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Diagnostics
{
    /// <summary>
    /// Wrapper for GeneratorExecutionContext.ReportDiagnostic method
    /// because GeneratorExecutionContext is a struct and can't be mocked easily.
    /// </summary>
    public interface IDiagnosticReporter
    {
        void Report(Diagnostic diagnostic);
    }

    public class DiagnosticReporter : IDiagnosticReporter
    {
        private readonly GeneratorExecutionContext _context;

        public DiagnosticReporter(GeneratorExecutionContext context)
        {
            _context = context;
        }

        public void Report(Diagnostic diagnostic)
        {
            _context.ReportDiagnostic(diagnostic);
        }
    }
}