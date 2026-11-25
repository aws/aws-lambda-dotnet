// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace TestCustomServiceProviderApp;

/// <summary>
/// A minimal <see cref="IServiceProvider"/> that delegates to the default provider.
/// It stands in for an alternate IoC container (for example Autofac) in the tests.
/// </summary>
public class CustomServiceProvider : IServiceProvider
{
    private readonly IServiceProvider _inner;

    public CustomServiceProvider(IServiceProvider inner)
    {
        _inner = inner;
    }

    public object GetService(Type serviceType)
    {
        return _inner.GetService(serviceType);
    }
}
