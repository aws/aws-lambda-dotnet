using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    public class LambdaFunctionAttribute : Attribute
    {
        /// <summary>
        /// The name of the Lambda function which is used to uniquely identify the function within an AWS region.
        /// </summary>
        public string Name { get; set; }

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

        /// <summary>
        /// The deployment package type of the Lambda function. The supported values are Zip or Image. The default value is Zip.
        /// For more information, see <a href="https://docs.aws.amazon.com/lambda/latest/dg/gettingstarted-package.html">here</a>
        /// </summary>
        public LambdaPackageType PackageType { get; set; }
    }
}