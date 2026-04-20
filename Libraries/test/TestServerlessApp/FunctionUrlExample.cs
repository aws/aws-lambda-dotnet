// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class FunctionUrlExample
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [FunctionUrl(AuthType = FunctionUrlAuthType.NONE)]
        public IHttpResult GetItems([FromQuery] string category, ILambdaContext context)
        {
            context.Logger.LogLine($"Getting items for category: {category}");
            return HttpResults.Ok(new { items = new[] { "item1", "item2" }, category });
        }
    }
}
