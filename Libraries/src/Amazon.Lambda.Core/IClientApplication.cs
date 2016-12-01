namespace Amazon.Lambda.Core
{
    /// <summary>
    ///  Information about the client application that invoked the Lambda function.
    /// </summary>
    public interface IClientApplication
    {
        /// <summary>
        /// The application's package name.
        /// </summary>
        string AppPackageName { get; }
        
        /// <summary>
        /// The application's title.
        /// </summary>
        string AppTitle { get; }
        
        /// <summary>
        /// The application's version code.
        /// </summary>
        string AppVersionCode { get; }
        
        /// <summary>
        /// The application's version.
        /// </summary>
        string AppVersionName { get; }

        /// <summary>
        /// The application's installation id.
        /// </summary>
        string InstallationId { get; }
    }
}