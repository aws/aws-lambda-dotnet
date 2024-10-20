using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Extensions
{
    public static class ParameterListExtension
    {
        public static bool HasConvertibleParameter(this IList<ParameterModel> parameters)
        {
            return parameters.Any(p =>
            {
                // All request types are forwarded to lambda method if specified, there is no parameter conversion required.
                if (TypeFullNames.Requests.Contains(p.Type.FullName))
                {
                    return false;
                }

                // ILambdaContext is forwarded to lambda method if specified, there is no parameter conversion required.
                if (p.Type.FullName == TypeFullNames.ILambdaContext)
                {
                    return false;
                }

                // Body parameter with target type as string doesn't require conversion because body is string by nature.
                if (p.Attributes.Any(att => att.Type.FullName == TypeFullNames.FromBodyAttribute) && p.Type.IsString())
                {
                    return false;
                }

                // All other parameters either have From* attribute or considered as FromRoute parameter which require type conversion
                return true;
            });
        }
    }
}