namespace Amazon.Lambda.TestTool.SampleRequests;

public class LambdaRequest
{
    public required string Name { get; init; }
    public required string Group { get; init; }
    public required string Filename { get; init; }
}