using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    internal class SqsMessageAttributeBuilder
    {
        public static SqsMessageAttribute Build(AttributeData att)
        {
            //if (att.ConstructorArguments.Length != 2)
            //{
            //    throw new NotSupportedException($"{TypeFullNames.RestApiAttribute} must have constructor with 2 arguments.");
            //}

            //var method = (LambdaHttpMethod)att.ConstructorArguments[0].Value;
            //var template = att.ConstructorArguments[1].Value as string;

            var data = new SqsMessageAttribute();
            foreach (var attNamedArgument in att.NamedArguments)
            {
                switch (attNamedArgument.Key)
                {
                    case nameof(ISqsMessage.QueueName):
                        data.QueueName = attNamedArgument.Value.Value.ToString();
                        break;
                }
            }

            return data;
        }
    }
}
