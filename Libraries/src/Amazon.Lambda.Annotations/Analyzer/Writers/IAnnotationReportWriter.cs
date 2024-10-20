using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    public interface IAnnotationReportWriter
    {
        void ApplyReport(AnnotationReport report);
    }
}