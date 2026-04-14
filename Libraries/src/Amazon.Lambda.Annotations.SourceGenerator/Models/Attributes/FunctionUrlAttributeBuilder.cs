// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using Amazon.Lambda.Annotations.APIGateway;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    public static class FunctionUrlAttributeBuilder
    {
        public static FunctionUrlAttribute Build(AttributeData att)
        {
            var authType = att.NamedArguments.FirstOrDefault(arg => arg.Key == "AuthType").Value.Value;

            var data = new FunctionUrlAttribute
            {
                AuthType = authType == null ? FunctionUrlAuthType.NONE : (FunctionUrlAuthType)authType
            };

            var allowOrigins = att.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowOrigins").Value;
            if (!allowOrigins.IsNull)
                data.AllowOrigins = allowOrigins.Values.Select(v => v.Value as string).ToArray();

            var allowMethods = att.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowMethods").Value;
            if (!allowMethods.IsNull)
                data.AllowMethods = allowMethods.Values.Select(v => (LambdaHttpMethod)(int)v.Value).ToArray();

            var allowHeaders = att.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowHeaders").Value;
            if (!allowHeaders.IsNull)
                data.AllowHeaders = allowHeaders.Values.Select(v => v.Value as string).ToArray();

            var exposeHeaders = att.NamedArguments.FirstOrDefault(arg => arg.Key == "ExposeHeaders").Value;
            if (!exposeHeaders.IsNull)
                data.ExposeHeaders = exposeHeaders.Values.Select(v => v.Value as string).ToArray();

            var allowCredentials = att.NamedArguments.FirstOrDefault(arg => arg.Key == "AllowCredentials").Value.Value;
            if (allowCredentials != null)
                data.AllowCredentials = (bool)allowCredentials;

            var maxAge = att.NamedArguments.FirstOrDefault(arg => arg.Key == "MaxAge").Value.Value;
            if (maxAge != null)
                data.MaxAge = (int)maxAge;

            return data;
        }
    }
}
