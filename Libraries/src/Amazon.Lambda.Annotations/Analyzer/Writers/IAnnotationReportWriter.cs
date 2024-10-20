using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    internal interface IAnnotationReportWriter
    {
        void ApplyReport(AnnotationReport report);
    }
}