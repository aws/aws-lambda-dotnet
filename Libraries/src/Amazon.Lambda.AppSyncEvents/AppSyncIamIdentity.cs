using System.Collections.Generic;

namespace Amazon.Lambda.AppSyncEvents
{
    /// <summary>
    /// Represents AWS IAM authorization identity for AppSync
    /// </summary>
    public class AppSyncIamIdentity
    {
        /// <summary>
        /// The source IP address of the caller received by AWS AppSync
        /// </summary>
        public List<string> SourceIp { get; set; }

        /// <summary>
        /// The username of the authenticated user (IAM user principal)
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The AWS account ID of the caller
        /// </summary>
        public string AccountId { get; set; }

        /// <summary>
        /// The Amazon Cognito identity pool ID associated with the caller
        /// </summary>
        public string CognitoIdentityPoolId { get; set; }

        /// <summary>
        /// The Amazon Cognito identity ID of the caller
        /// </summary>
        public string CognitoIdentityId { get; set; }

        /// <summary>
        /// The ARN of the IAM user
        /// </summary>
        public string UserArn { get; set; }

        /// <summary>
        /// Either authenticated or unauthenticated based on the identity type
        /// </summary>
        public string CognitoIdentityAuthType { get; set; }

        /// <summary>
        /// A comma separated list of external identity provider information used in obtaining the credentials used to sign the request
        /// </summary>
        public string CognitoIdentityAuthProvider { get; set; }
    }
}