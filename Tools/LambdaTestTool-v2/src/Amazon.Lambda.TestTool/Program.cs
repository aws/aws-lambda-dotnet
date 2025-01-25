// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool;
using Amazon.Lambda.TestTool.Commands;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Services;
using Spectre.Console.Cli;

var serviceCollection = new ServiceCollection();

serviceCollection.AddCustomServices();

var registrar = new TypeRegistrar(serviceCollection);

var app = new CommandApp<RunCommand>(registrar);
app.Configure(config =>
{
    config.SetApplicationName(Constants.ToolName);
});

var arguments = new List<string>(args);

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAMBDA_RUNTIME_API_PORT")))
{
    arguments.Add("--port");
    arguments.Add(Environment.GetEnvironmentVariable("LAMBDA_RUNTIME_API_PORT")!);
}
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("API_GATEWAY_EMULATOR_PORT")))
{
    arguments.Add("--api-gateway-emulator-port");
    arguments.Add(Environment.GetEnvironmentVariable("API_GATEWAY_EMULATOR_PORT")!);
}

return await app.RunAsync(arguments);