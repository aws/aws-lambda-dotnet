using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.S3Events
{
    /// <summary>
    /// Class representing the S3 Object Lambda event.
    /// 
    /// S3 Developer Guide explaining the event data.
    /// https://docs.aws.amazon.com/AmazonS3/latest/userguide/olap-writing-lambda.html
    /// </summary>
    public class S3ObjectLambdaEvent
    {
        /// <summary>
        /// The Amazon S3 request ID for this request. We recommend that you log this value to help with debugging.
        /// </summary>
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("xAmzRequestId")]
#endif
        public string XAmzRequestId { get; set; }

        /// <summary>
        /// The input and output details for connections to Amazon S3 and S3 Object Lambda.
        /// </summary>
        public GetObjectContextType GetObjectContext { get; set; }

        /// <summary>
        /// Configuration information about the S3 Object Lambda access point.
        /// </summary>
        public ConfigurationType Configuration { get; set; }

        /// <summary>
        /// Information about the original call to S3 Object Lambda.
        /// </summary>
        public UserRequestType UserRequest { get; set; }

        /// <summary>
        /// Details about the identity that made the call to S3 Object Lambda.
        /// </summary>
        public UserIdentityType UserIdentity { get; set; }

        /// <summary>
        /// The version ID of the context provided. The format of this field is {Major Version}.{Minor Version}.
        /// </summary>
        public string ProtocolVersion { get; set; }

        /// <summary>
        /// The input and output details for connections to Amazon S3 and S3 Object Lambda.
        /// </summary>
        public class GetObjectContextType
        {
            /// <summary>
            /// A presigned URL that can be used to fetch the original object from Amazon S3. The URL is signed 
            /// using the original caller’s identity, and their permissions will apply when the URL is used. 
            /// If there are signed headers in the URL, the Lambda function must include these in the call to 
            /// Amazon S3, except for the Host.
            /// </summary>
            public string InputS3Url { get; set; }

            /// <summary>
            /// A presigned URL that can be used to fetch the original object from Amazon S3. The URL is signed 
            /// using the original caller’s identity, and their permissions will apply when the URL is used. If 
            /// there are signed headers in the URL, the Lambda function must include these in the call to 
            /// Amazon S3, except for the Host.
            /// </summary>
            public string OutputRoute { get; set; }

            /// <summary>
            /// An opaque token used by S3 Object Lambda to match the WriteGetObjectResponse call with the 
            /// original caller.
            /// </summary>
            public string OutputToken { get; set; }
        }

        /// <summary>
        /// Configuration information about the S3 Object Lambda access point.
        /// </summary>
        public class ConfigurationType
        {
            /// <summary>
            /// The Amazon Resource Name (ARN) of the S3 Object Lambda access point that received this request.
            /// </summary>
            public string AccessPointArn { get; set; }

            /// <summary>
            /// The ARN of the supporting access point that is specified in the S3 Object Lambda access point configuration.
            /// </summary>
            public string SupportingAccessPointArn { get; set; }

            /// <summary>
            /// ustom data that is applied to the S3 Object Lambda access point configuration. S3 Object Lambda treats 
            /// this as an opaque string, so it might need to be decoded before use.
            /// </summary>
            public string Payload { get; set; }

        }

        /// <summary>
        /// Information about the original call to S3 Object Lambda.
        /// </summary>
        public class UserRequestType
        {
            /// <summary>
            /// The decoded URL of the request as received by S3 Object Lambda, 
            /// excluding any authorization-related query parameters.
            /// </summary>
            public string Url { get; set; }

            /// <summary>
            /// A map of string to strings containing the HTTP headers and their values from the original call, excluding 
            /// any authorization-related headers. If the same header appears multiple times, their values are 
            /// combined into a comma-delimited list.
            /// </summary>
            public IDictionary<string, string> Headers { get; set; }
        }

        /// <summary>
        /// Details about the identity that made the call to S3 Object Lambda.
        /// </summary>
        public class UserIdentityType
        {
            /// <summary>
            /// The type of identity.
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// The unique identifier for the identity that made the call.
            /// </summary>
            public string PrincipalId { get; set; }

            /// <summary>
            /// The ARN of the principal that made the call. The last section of the ARN contains the user or role that made the call.
            /// </summary>
            public string Arn { get; set; }

            /// <summary>
            /// The AWS account to which the identity belongs.
            /// </summary>
            public string AccountId { get; set; }

            /// <summary>
            /// The AWS Access Key Id for the identity.
            /// </summary>
            public string AccessKeyId { get; set; }

            /// <summary>
            /// If the request was made with temporary security credentials, this element provides information about the 
            /// session that was created for those credentials.
            /// </summary>
            public SessionContextType SessionContext { get; set; }
        }

        /// <summary>
        /// The information about temporary session credentials used by the identity.
        /// </summary>
        public class SessionContextType
        {
            /// <summary>
            /// Attributes for the temporary session credentials
            /// </summary>
            public SessionContextAttributesType Attributes { get; set; }

            /// <summary>
            /// If the request was made with temporary security credentials, this element provides information about how the credentials were obtained.
            /// </summary>
            public SessionIssuerType SessionIssuer { get; set; }
        }

        /// <summary>
        /// Attributes of the temporary session credentials
        /// </summary>
        public class SessionContextAttributesType
        {
            /// <summary>
            /// Identifies whether MFA authentication was used when obtaining temporary credentials. 
            /// </summary>
            public string MfaAuthenticated { get; set; }

            /// <summary>
            /// The create date of the temporary session credentials.
            /// </summary>
            public string CreationDate { get; set; }
        }

        /// <summary>
        /// Information about the issuer of the temporary session credentials.
        /// </summary>
        public class SessionIssuerType
        {
            /// <summary>
            /// The type of issuer of the temporary session credentials.
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// The principal id of the issuer of the temporary session credentials.
            /// </summary>
            public string PrincipalId { get; set; }

            /// <summary>
            /// The arn of the issuer of the temporary session credentials.
            /// </summary>
            public string Arn { get; set; }

            /// <summary>
            /// The account id of the issuer of the temporary session credentials.
            /// </summary>
            public string AccountId { get; set; }

            /// <summary>
            /// The user name of the issuer of the temporary session credentials.
            /// </summary>
            public string UserName { get; set; }
        }
    }
}
