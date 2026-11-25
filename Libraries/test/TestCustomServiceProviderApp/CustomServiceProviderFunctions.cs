// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TestCustomServiceProviderApp;

public class CustomServiceProviderFunctions
{
    private ICalculatorService _calculatorService;

    public CustomServiceProviderFunctions(ICalculatorService calculatorService)
    {
        _calculatorService = calculatorService;
    }

    [LambdaFunction()]
    [HttpApi(LambdaHttpMethod.Get, "/add/{x}/{y}")]
    public int Add(int x, int y, ILambdaContext context)
    {
        var sum = _calculatorService.Add(x, y);
        return sum;
    }
}
