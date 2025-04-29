// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool;
using Amazon.Lambda.TestTool.Commands;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Services;
using Spectre.Console.Cli;

// Till we do the full inspection for collection maintain the S3 behavior for initializing collections.
Amazon.AWSConfigs.InitializeCollections = true;

var serviceCollection = new ServiceCollection();

serviceCollection.AddCustomServices();

var registrar = new TypeRegistrar(serviceCollection);

var app = new CommandApp(registrar);
app.Configure(config =>
{
    config.AddCommand<RunCommand>("start")
        .WithDescription("Start the Lambda and/or API Gateway emulator.");
    config.AddCommand<ToolInfoCommand>("info")
        .WithDescription("Display information about the tool including the version number.");

    config.SetApplicationName(Constants.ToolName);
});

return await app.RunAsync(args);
