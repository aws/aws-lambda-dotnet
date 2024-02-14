using Amazon.Lambda.Annotations;

namespace TestServerlessApp.NET8;

/// <summary>
/// Sample Lambda functions
/// </summary>
public class Functions
{
    [LambdaFunction]
    public string ToUpper(string text)
    {
        return text.ToUpper();
    }
}