using System;

namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// Indicates this method should be exposed as a Lambda function
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class LambdaFunctionAttribute : Attribute
    {
        /// <summary>
        /// The serializer to use.
        /// </summary>
        public string Serializer { get; set; }
        
        /// <summary>
        /// The name of the CloudFormation resource that is associated with the Lambda function.
        /// </summary>
        public string ResourceName { get; set; }

        /// <summary>
        /// The amount of time in seconds that Lambda allows a function to run before stopping it.
        /// </summary>
        public uint Timeout { get; set; }

        /// <summary>
        /// The amount of memory available to your Lambda function at runtime.
        /// </summary>
        public uint MemorySize { get; set; }

        /// <summary>
        /// The IAM Role assumed by the Lambda function during its execution.
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Resource based policies that grants permissions to access other AWS resources.
        /// </summary>
        public string Policies { get; set; }

        /// <inheritdoc cref="LambdaPackageType" />
        public LambdaPackageType PackageType { get; set; }
    }
}