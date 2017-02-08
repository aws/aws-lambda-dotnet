using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Runtime;

namespace Amazon.Lambda.Tools
{
    /// <summary>
    /// The deploy tool exception. This is used to throw back an error to the user but is considerd a known error
    /// so the stack trace will not be displayed.
    /// </summary>
    public class LambdaToolsException : Exception
    {
        public enum ErrorCode {

            BucketInDifferentRegionThenStack,

            CloudFormationCreateChangeSet,
            CloudFormationCreateStack,
            CloudFormationDeleteStack,
            CloudFormationDescribeChangeSet,
            CloudFormationDescribeStack,
            CloudFormationDescribeStackEvents,

            CommandLineParseError,
            DefaultsParseFail,
            DotnetPublishFail,
            InvalidPackage,
            FrameworkNewerThanRuntime,
            HandlerValidation,
            ProfileNotFound,
            ProfileNotCreateable,
            PersistConfigError,
            RegionNotConfigured,
            RoleNotFound,

            IAMAttachRole,
            IAMCreateRole,
            IAMGetRole,


            LambdaCreateFunction,
            LambdaDeleteFunction,
            LambdaGetConfiguration,
            LambdaInvokeFunction,
            LambdaListFunctions,
            LambdaUpdateFunctionCode,
            LambdaUpdateFunctionConfiguration,

            S3GetBucketLocation,
            S3UploadError,

            ServerlessTemplateNotFound,
            ServerlessTemplateParseError,
            WaitingForStackError
        }

        public LambdaToolsException(string message, ErrorCode code) : base(message)
        {
            this.Code = code;
        }

        public LambdaToolsException(string message, ErrorCode code, Exception e) : this(message, code)
        {
            var ae = e as AmazonServiceException;
            if (ae != null)
            {
                this.ServiceCode = $"{ae.ErrorCode}-{ae.StatusCode}";
            }
        }

        public ErrorCode Code { get; }

        public string ServiceCode { get; }
    }

    public class ValidateHandlerException : LambdaToolsException
    {
        public string ProjectLocation { get; }
        public string Handler { get; }
        public ValidateHandlerException(string projectLocation, string handler, string message) : base(message, LambdaToolsException.ErrorCode.HandlerValidation)
        {
            this.ProjectLocation = projectLocation;
            this.Handler = handler;
        }
    }
}
