using Amazon.Lambda.Annotations.APIGateway;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    public class FromCustomAuthorizerAttributeBuilder
    {
        public static FromCustomAuthorizerAttribute Build(AttributeData att)
        {
            var data = new FromCustomAuthorizerAttribute();
            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.Name) && pair.Value.Value is string value)
                {
                    data.Name = value;
                }
            }

            return data;
        }
    }
}
