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
            AttributeModel<SqsMessageAttribute> sqsAttributeModel = sqsMessageAttribute as AttributeModel<SqsMessageAttribute>;
            var model = new SqsMessageModel()
            {
                QueueName = sqsAttributeModel?.Data?.QueueName,
                LogicalId = sqsAttributeModel?.Data?.LogicalId ?? "QueueFor" + lambdaMethodSymbol.Name,
                SourceGeneratorVersion = context.Compilation
                    .ReferencedAssemblyNames.FirstOrDefault(x => string.Equals(x.Name, "Amazon.Lambda.Annotations"))
                    ?.Version.ToString()
            };

            return model;
        }
    }
}