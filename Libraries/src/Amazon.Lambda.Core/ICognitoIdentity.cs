namespace Amazon.Lambda.Core
{
    /// <summary>
    /// Information related to Amazon Congnito identities.
    /// </summary>
    public interface ICognitoIdentity
    {
        /// <summary>
        /// The Amazon Cognito identity ID.
        /// </summary>
        string IdentityId { get; }

        /// <summary>
        /// The Amazon Cognito identity pool ID.
        /// </summary>
        string IdentityPoolId { get; }
    }
}