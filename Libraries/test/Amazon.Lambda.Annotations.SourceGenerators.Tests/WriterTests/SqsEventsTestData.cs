using Amazon.Lambda.Annotations.SourceGenerator;
using System.Collections.Generic;
using Xunit;
using static Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests.SqsEventsTestData;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public class SqsEventsTestData : TheoryData<CloudFormationTemplateFormat, IEnumerable<SqsEventAttributeModelTest>, string>
    {
        const string queueArn1 = "arn:aws:sqs:us-east-2:444455556666:queue1";
        const string queueArn2 = "arn:aws:sqs:us-east-2:444455556666:queue2";

        public SqsEventsTestData()
        {
            foreach (var templateFormat in new List<CloudFormationTemplateFormat> { CloudFormationTemplateFormat.Json, CloudFormationTemplateFormat.Yaml })
            {
                // Simple attribute
                Add(templateFormat, [new(queueArn1)], "void");

                // Report batch failure items.
                Add(templateFormat, [new(queueArn1)], TypeFullNames.SQSBatchResponse);

                // Mutliple SQSEvent attributes
                Add(templateFormat, [new(queueArn1), new(queueArn2)], TypeFullNames.SQSBatchResponse);

                // Use queue reference
                Add(templateFormat, [new("@MyQueue")], TypeFullNames.SQSBatchResponse);

                // Use both ARN and queue reference
                Add(templateFormat, [new(queueArn1), new("@MyQueue")], "void");

                // Specify filters
                Add(templateFormat, [new(queueArn1) { Filters = "SOME-FILTER1; SOME-FILTER2" },], "void");

                // Explicitly specify all properties
                Add(templateFormat,
                    [new(queueArn1)
                        {
                            BatchSize = 10,
                            MaximumConcurrency = 30,
                            Filters = "SOME-FILTER1; SOME-FILTER2",
                            MaximumBatchingWindowInSeconds = 15,
                            Enabled = false
                        }],
                    TypeFullNames.SQSBatchResponse);
            }
        }

        public class SqsEventAttributeModelTest(string queue)
        {
            public string Queue { get; init; } = queue;
            public bool? Enabled { get; init; }
            public uint? MaximumConcurrency { get; init; }
            public uint? BatchSize { get; init; }
            public uint? MaximumBatchingWindowInSeconds { get; init; }
            public string? Filters { get; init; }
        }
    }
}
