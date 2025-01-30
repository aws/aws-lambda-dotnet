// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Services.IO;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amazon.Lambda.TestTool.Extensions;

/// <summary>
/// A class that contains extension methods for the <see cref="IServiceCollection"/> interface.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a set of services for the .NET CLI portion of this application.
    /// </summary>
    public static void AddCustomServices(this IServiceCollection serviceCollection,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        serviceCollection.TryAdd(new ServiceDescriptor(typeof(IToolInteractiveService), typeof(ConsoleInteractiveService), lifetime));
        serviceCollection.TryAdd(new ServiceDescriptor(typeof(IDirectoryManager), typeof(DirectoryManager), lifetime));
        serviceCollection.TryAdd(new ServiceDescriptor(typeof(IEnvironmentManager), typeof(EnvironmentManager), lifetime));
    }

    /// <summary>
    /// Adds a set of services for the API Gateway emulator portion of this application.
    /// </summary>
    public static void AddApiGatewayEmulatorServices(this IServiceCollection serviceCollection,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        serviceCollection.TryAdd(new ServiceDescriptor(typeof(IApiGatewayRouteConfigService), typeof(ApiGatewayRouteConfigService), lifetime));
        serviceCollection.TryAdd(new ServiceDescriptor(typeof(IEnvironmentManager), typeof(EnvironmentManager), lifetime));
    }
}
