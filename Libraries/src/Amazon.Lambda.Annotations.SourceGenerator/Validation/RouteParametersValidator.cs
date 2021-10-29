using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;

namespace Amazon.Lambda.Annotations.SourceGenerator.Validation
{
    public static class RouteParametersValidator
    {
        public static (bool isValid, IList<string> missingParameters) Validate(HashSet<string> routeParameters, IList<ParameterModel> lambdaMethodParameters)
        {
            var missingRouteParams = new List<string>();
            var isValid = true;

            foreach (var routeParam in routeParameters)
            {
                var fromRouteMethodParam = lambdaMethodParameters
                    .FirstOrDefault(p => p.Attributes.Any(att => (att as AttributeModel<FromRouteAttribute>)?.Data.Name == routeParam));

                // Route parameter valid because there is a matching signature parameter with a FromRoute attribute
                if (fromRouteMethodParam != null)
                {
                    continue;
                }

                var nameMatchingMethodParam = lambdaMethodParameters.FirstOrDefault(p => p.Name == routeParam);

                // Route parameter invalid because there is no matching parameter name
                if (nameMatchingMethodParam == null)
                {
                    missingRouteParams.Add(routeParam);
                    isValid = false;
                    continue;
                }

                // There exists a route parameter matching with method parameter name but also has one of conflicting From attribute
                var conflictingAttributes = nameMatchingMethodParam.Attributes.Where(att => att.Type.FullName.StartsWith(Namespaces.Annotations)).ToList();
                if (conflictingAttributes.Count != 0)
                {
                    throw new InvalidOperationException($"Conflicting attribute(s) {string.Join(",", conflictingAttributes.Select(att => att.Type.FullName))} found on {nameMatchingMethodParam.Name} method parameter.");
                }
            }

            return (isValid, missingRouteParams);
        }
    }
}