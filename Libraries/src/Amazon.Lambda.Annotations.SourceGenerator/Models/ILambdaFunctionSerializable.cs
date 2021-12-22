using System;
using System.Collections.Generic;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public interface ILambdaFunctionSerializable
    {
        /// <summary>
        /// <para>
        /// The name of the method within your code that Lambda calls to execute your function.
        /// The format includes the file name. It can also include namespaces and other qualifiers, depending on the runtime.
        /// For more information, see <a href="https://docs.aws.amazon.com/lambda/latest/dg/csharp-handler.html">here</a>
        /// </para>
        /// </summary>
        string Handler { get; }

        /// <summary>
        /// The name of the Lambda function which is used to uniquely identify the function within an AWS region.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The amount of time in seconds that Lambda allows a function to run before stopping it.
        /// </summary>
        uint? Timeout { get; }

        /// <summary>
        /// The amount of memory available to your Lambda function at runtime.
        /// </summary>
        uint? MemorySize { get; }

        /// <summary>
        /// The IAM Role assumed by the Lambda function during its execution.
        /// </summary>
        string Role { get; }

        /// <summary>
        /// Resource based policies that grants permissions to access other AWS resources.
        /// </summary>
        string Policies { get; }

        /// <summary>
        /// The deployment package type of the Lambda function. The supported values are Zip and Image.
        /// For more information, see <a href="https://docs.aws.amazon.com/lambda/latest/dg/gettingstarted-package.html">here</a>
        /// </summary>
        LambdaPackageType PackageType { get; }

        /// <summary>
        /// List of attributes applied to the Lambda method that are used to generate serverless.template.
        /// There always exists <see cref="Annotations.LambdaFunctionAttribute"/> in the list.
        /// </summary>
        IList<AttributeModel> Attributes { get; }

        /// <summary>
        /// The assembly version of the Amazon.Lambda.Annotations.SourceGenerator package.
        /// </summary>
        string SourceGeneratorVersion { get; set; }
    }
}