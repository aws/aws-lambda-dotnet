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
        public int Timeout { get; set; }

        /// <summary>
        /// The amount of memory available to your Lambda function at runtime.
        /// </summary>
        public int MemorySize { get; set; }

        /// <summary>
        /// The IAM Role assumed by the Lambda function during its execution.
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Resource based policies that grants permissions to access other AWS resources.
        /// </summary>
        public string Policies { get; set; }
    }
}