namespace TestServerlessApp.Sub1
{
    using Amazon.Lambda.Annotations;
    using Amazon.Lambda.Core;

    public class Functions
    {
        [LambdaFunction(ResourceName = "ToUpper", PackageType = LambdaPackageType.Image)]
        public string ToUpper(string text)
        {
            return text.ToUpper();
        }
    }
}