using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="LambdaFunctionAttributeData"/>.
    /// </summary>
    public static class LambdaFunctionAttributeDataBuilder
    {
        public static LambdaFunctionAttribute Build(AttributeData att)
        {
            var data = new LambdaFunctionAttribute();

            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.Name) && pair.Value.Value is string value)
                {
                    data.Name = value;
                }

                if (pair.Key == nameof(data.Policies) && pair.Value.Value is string policies)
                {
                    data.Policies = policies;
                }

                if (pair.Key == nameof(data.Role) && pair.Value.Value is string role)
                {
                    data.Role = role;
                }

                if (pair.Key == nameof(data.Timeout) && pair.Value.Value is uint timeout)
                {
                    data.Timeout = timeout;
                }

                if (pair.Key == nameof(data.MemorySize) && pair.Value.Value is uint memorySize)
                {
                    data.MemorySize = memorySize;
                }

                if (pair.Key == nameof(data.PackageType) && pair.Value.Value is int)
                {
                    data.PackageType = (LambdaPackageType)pair.Value.Value;
                }
            }

            return data;
        }
    }
}