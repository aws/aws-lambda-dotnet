using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    /// <summary>
    /// Tests that call <c>Amazon.Lambda.Core.LambdaLogger.ConfigureStructuredLogging</c> mutate a process-wide
    /// static callback on LambdaLogger and construct JsonLogMessageFormatter instances that register themselves as
    /// that callback's target. If such tests run in parallel across classes they interfere with each other (the
    /// last formatter constructed wins the static registration). Placing every such test in this single collection
    /// makes xUnit run them serially. See https://github.com/aws/aws-lambda-dotnet/issues/2350.
    /// </summary>
    [CollectionDefinition("StructuredLogging")]
    public class StructuredLoggingCollection
    {
    }
}
