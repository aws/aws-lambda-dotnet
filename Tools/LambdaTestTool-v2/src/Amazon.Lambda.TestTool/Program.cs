using Amazon.Lambda.TestTool;
using Amazon.Lambda.TestTool.Extensions;

var builder = Host.CreateApplicationBuilder();

builder.Services.AddCustomServices();

var serviceProvider = builder.Build();

var appRunner = serviceProvider.Services.GetService<AppRunner>();
if (appRunner == null)
{
    throw new Exception($"{nameof(AppRunner)} dependencies aren't injected correctly." +
                        $" Verify {nameof(ServiceCollectionExtensions)} has all the required dependencies to instantiate {nameof(AppRunner)}.");
}

return await appRunner.Run(args);