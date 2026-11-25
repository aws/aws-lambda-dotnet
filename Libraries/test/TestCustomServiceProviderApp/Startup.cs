// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using System;

namespace TestCustomServiceProviderApp;

[Amazon.Lambda.Annotations.LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICalculatorService>(new CalculatorService());
    }

    /// <summary>
    /// Builds the service provider used by the generated Lambda function handler.
    /// Returning a custom <see cref="IServiceProvider"/> here lets the function use
    /// an alternate IoC container instead of the default Microsoft.Extensions.DependencyInjection one.
    /// </summary>
    public IServiceProvider BuildServiceProvider(IServiceCollection services)
    {
        return new CustomServiceProvider(services.BuildServiceProvider());
    }
}
