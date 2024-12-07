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

return await app.RunAsync(args);