using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    public class LambdaFunctionParameterCodeGenerator
    {
        public string Generate(GeneratorExecutionContext context,
            IMethodSymbol methodSymbol)
        {
            var parameters = new List<string>();
            context.Compilation.GetTypeByMetadataName(nameof(FromServicesAttribute));
            foreach (var parameter in methodSymbol.Parameters)
            {
                var fromServicesAttribute = parameter.GetAttributes()
                    .FirstOrDefault(attr => attr?.AttributeClass?.Name.Equals(nameof(FromServicesAttribute)) ?? false);

                if (fromServicesAttribute != null)
                {
                    parameters.Add($"serviceProvider.GetRequiredService<{parameter.Type}>()");
                }
            }

            return string.Join(",", parameters);
        }
    }
}