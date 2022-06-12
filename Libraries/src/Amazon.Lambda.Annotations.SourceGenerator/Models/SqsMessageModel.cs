using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Represents container class for the Lambda function.
    /// </summary>
    public class SqsMessageModel : ISqsMessageSerializable
    {
        private AttributeModel<SqsMessageAttribute> _sqsMessageAttribute;


        /// <summary>
        /// Gets or sets the attributes of original method. There always exist <see cref="Annotations.LambdaFunctionAttribute"/> in the list.
        /// </summary>
        public IList<AttributeModel> Attributes { get; set; }
        
        public AttributeModel<SqsMessageAttribute> SqsMessageAttribute
        {
            get
            {
                if (_sqsMessageAttribute == null)
                {
                    var model = Attributes.First(att => att.Type.FullName == TypeFullNames.LambdaFunctionAttribute);
                    if (model is AttributeModel<SqsMessageAttribute> lambdaFunctionAttributeModel)
                    {
                        _sqsMessageAttribute = lambdaFunctionAttributeModel;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Lambda method must has a {TypeFullNames.LambdaFunctionAttribute} attribute");
                    }
                }

                return _sqsMessageAttribute;
            }
        }

        public string LogicalId { get; set; }
        public string QueueName { get; set; }
        public int BatchSize { get; set; }
        public string SourceGeneratorVersion { get; set; }
    }
}