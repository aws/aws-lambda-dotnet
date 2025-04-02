// // Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// // SPDX-License-Identifier: Apache-2.0

// using Amazon.Runtime;
// using Amazon.Lambda.Model;
// using Amazon.Lambda.TestTool.Services;
// using Amazon.Lambda.RuntimeSupport;
// using Amazon.Lambda.Serialization.SystemTextJson;
// using Amazon.Lambda.Core;
// using Amazon.Lambda.TestTool.Processes;
// using Amazon.Lambda.TestTool.Commands.Settings;
// using Amazon.Lambda.TestTool.Tests.Common.Helpers;
// using Amazon.Lambda.TestTool.Tests.Common.Retries;
// using Microsoft.Extensions.DependencyInjection;
// using Xunit;
// using Environment = System.Environment;

// namespace Amazon.Lambda.TestTool.UnitTests;

// public class RuntimeApiTests
// {
// #if DEBUG
//     [Fact]
// #else
//     [Fact(Skip = "Skipping this test as it is not working properly.")]
// #endif
//     public async Task AddEventToDataStore()
//     {
//         const string functionName = "FunctionFoo";

//         var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
//         var cancellationTokenSource = new CancellationTokenSource();
//         cancellationTokenSource.CancelAfter(15_000);
//         var options = new RunCommandSettings();
//         options.LambdaEmulatorPort = lambdaPort;
//         Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
//         var testToolProcess = TestToolProcess.Startup(options, cancellationTokenSource.Token);
//         try
//         {
//             var lambdaClient = ConstructLambdaServiceClient(testToolProcess.ServiceUrl);
//             var invokeFunction = new InvokeRequest
//             {
//                 FunctionName = functionName,
//                 Payload = "\"hello\"",
//                 InvocationType = InvocationType.Event
//             };

//             await lambdaClient.InvokeAsync(invokeFunction, cancellationTokenSource.Token);

//             var dataStoreManager = testToolProcess.Services.GetRequiredService<IRuntimeApiDataStoreManager>();
//             var dataStore = dataStoreManager.GetLambdaRuntimeDataStore(functionName);
//             Assert.NotNull(dataStore);
//             Assert.Single(dataStore.QueuedEvents);
//             Assert.Single(dataStoreManager.GetListOfFunctionNames());
//             Assert.Equal(functionName, dataStoreManager.GetListOfFunctionNames().First());

//             var handlerCalled = false;
//             var handler = (string input, ILambdaContext context) =>
//             {
//                 handlerCalled = true;
//                 Thread.Sleep(1000); // Add a sleep to prove the LambdaRuntimeApi waited for the completion.
//                 return input.ToUpper();
//             };

//             _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
//                 .ConfigureOptions(x => x.RuntimeApiEndpoint = $"{options.LambdaEmulatorHost}:{options.LambdaEmulatorPort}/{functionName}")
//                 .Build()
//                 .RunAsync(cancellationTokenSource.Token);

//             await Task.Delay(2_000, cancellationTokenSource.Token);
//             Assert.True(handlerCalled);
//         }
//         finally
//         {
//             await cancellationTokenSource.CancelAsync();
//         }
//     }

//     [RetryFact]
//     public async Task InvokeRequestResponse()
//     {
//         const string functionName = "FunctionFoo";

//         var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
//         var cancellationTokenSource = new CancellationTokenSource();
//         cancellationTokenSource.CancelAfter(15_000);
//         var options = new RunCommandSettings();
//         options.LambdaEmulatorPort = lambdaPort;
//         Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
//         var testToolProcess = TestToolProcess.Startup(options, cancellationTokenSource.Token);
//         try
//         {
//             var handler = (string input, ILambdaContext context) =>
//             {
//                 Thread.Sleep(1000); // Add a sleep to prove the LambdaRuntimeApi waited for the completion.
//                 return input.ToUpper();
//             };

//             _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
//                 .ConfigureOptions(x => x.RuntimeApiEndpoint = $"{options.LambdaEmulatorHost}:{options.LambdaEmulatorPort}/{functionName}")
//                 .Build()
//                 .RunAsync(cancellationTokenSource.Token);

//             var lambdaClient = ConstructLambdaServiceClient(testToolProcess.ServiceUrl);

//             // Test with relying on the default value of InvocationType
//             var invokeFunction = new InvokeRequest
//             {
//                 FunctionName = functionName,
//                 Payload = "\"hello\""
//             };

//             var response = await lambdaClient.InvokeAsync(invokeFunction, cancellationTokenSource.Token);
//             var responsePayloadString = System.Text.Encoding.Default.GetString(response.Payload.ToArray());
//             Assert.Equal("\"HELLO\"", responsePayloadString);

//             // Test with InvocationType explicilty set
//             invokeFunction = new InvokeRequest
//             {
//                 FunctionName = functionName,
//                 Payload = "\"hello\"",
//                 InvocationType = InvocationType.RequestResponse
//             };

//             response = await lambdaClient.InvokeAsync(invokeFunction, cancellationTokenSource.Token);
//             responsePayloadString = System.Text.Encoding.Default.GetString(response.Payload.ToArray());
//             Assert.Equal("\"HELLO\"", responsePayloadString);
//         }
//         finally
//         {
//             await cancellationTokenSource.CancelAsync();
//         }
//     }

//     private IAmazonLambda ConstructLambdaServiceClient(string url)
//     {
//         var config = new AmazonLambdaConfig
//         {
//             ServiceURL = url,
//             MaxErrorRetry = 0
//         };

//         // We don't need real credentials because we are not calling the real Lambda service.
//         var credentials = new BasicAWSCredentials("accessKeyId", "secretKey");
//         return new AmazonLambdaClient(credentials, config);
//     }
// }
