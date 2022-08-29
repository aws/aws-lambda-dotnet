namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// The deployment package type of the Lambda function. The supported values are Zip or Image. The default value is Zip.
    /// For more information, see <a href="https://docs.aws.amazon.com/lambda/latest/dg/gettingstarted-package.html">here</a>
    /// </summary>
    public enum LambdaPackageType
    {
        Zip=0,
        Image=1
    }
}