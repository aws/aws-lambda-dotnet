using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="SqsMessageModel"/> builder.
    /// </summary>
    public static class SqsMessageModelBuilder
    {
        public static SqsMessageModel Build(IMethodSymbol lambdaMethodSymbol, IMethodSymbol configureMethodSymbol, GeneratorExecutionContext context, AttributeModel sqsMessageAttribute)
        {
            var lambdaMethod = LambdaMethodModelBuilder.Build(lambdaMethodSymbol, configureMethodSymbol, context);
            var model = new SqsMessageModel()
            {
                QueueName = lambdaMethodSymbol.Name + "QueueName",
                LogicalId = lambdaMethodSymbol.Name + "QueueLogicalId",
                SourceGeneratorVersion = context.Compilation
                    .ReferencedAssemblyNames.FirstOrDefault(x => string.Equals(x.Name, "Amazon.Lambda.Annotations"))
                    ?.Version.ToString()
            };

            return model;
        }
    }
}