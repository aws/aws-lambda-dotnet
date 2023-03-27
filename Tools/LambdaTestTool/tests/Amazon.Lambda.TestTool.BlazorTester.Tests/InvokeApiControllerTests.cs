using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Runtime;
using Amazon.Runtime.Endpoints;
using GreetingFunc;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Amazon.Lambda.TestTool.BlazorTester.Tests;

public class InvokeApiControllerTests
{
    private const string FunctionFileName = "GreetingFunc.json";
    private const string FunctionName = "GreetingFunc::GreetingFunc.Function::FunctionHandler";
    private const int FunctionPort = 10222;

    [Fact]
    public async Task InvokeFunctionSuccessfully()
    {
        const string expectedUserName = "John";
        var expectedOutput = Output.BuildGreeting(expectedUserName);

        using var session = await CreateSession();
        var input = new Input(expectedUserName);
        var response = await session.Client.PostAsJsonAsync($"invokeapi/execute/{FunctionName}", input);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actualOutput = await response.Content.ReadFromJsonAsync<Output>();
        Assert.Equal(expectedOutput.GreetingMessage, actualOutput!.GreetingMessage);
    }

    [Fact]
    public async Task InvokeFunctionUsingAwsSdkSuccessfully()
    {
        const string expectedUserName = "John";
        var expectedOutput = Output.BuildGreeting(expectedUserName);

        using var session = await CreateSession();
        var input = new Input(expectedUserName);

        var lambdaClient = new AmazonLambdaClient(new AmazonLambdaConfig
        {
            EndpointProvider = new StaticEndpointProvider(session.HostUri + "/invokeapi/")
        });

        var response = await lambdaClient.InvokeAsync(new InvokeRequest
        {
            FunctionName = FunctionName,
            Payload = JsonSerializer.Serialize(input),
            LogType = LogType.Tail
        });

        Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);

        var actualOutput = JsonSerializer.Deserialize<Output>(response.Payload);
        Assert.Equal(expectedOutput.GreetingMessage, actualOutput!.GreetingMessage);

        var logs = Encoding.UTF8.GetString(Convert.FromBase64String(response.LogResult));
        Assert.False(string.IsNullOrEmpty(logs));
    }

    [Fact]
    public async Task InvokeFunctionUsingAwsSdkWithFailure()
    {
        const string expectedUserName = "";

        using var session = await CreateSession();
        var input = new Input(expectedUserName);

        var lambdaClient = new AmazonLambdaClient(new AmazonLambdaConfig
        {
            EndpointProvider = new StaticEndpointProvider(session.HostUri + "/invokeapi/")
        });

        var response = await lambdaClient.InvokeAsync(new InvokeRequest
        {
            FunctionName = FunctionName,
            Payload = JsonSerializer.Serialize(input),
            LogType = LogType.Tail
        });

        Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
        Assert.Equal("Unhandled", response.FunctionError);

        var error = JsonSerializer.Deserialize<LambdaException>(response.Payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.False(string.IsNullOrEmpty(error!.ErrorType));
        Assert.False(string.IsNullOrEmpty(error!.ErrorMessage));
        Assert.NotEmpty(error!.StackTrace);
    }

    private async Task<TestSession> CreateSession(string functionFile = FunctionFileName) =>
        await TestSession.CreateSessionAsync("*", FunctionPort, functionFile);

    private record LambdaException(string ErrorCode, string ErrorType, string ErrorMessage, string[] StackTrace);

    private class TestSession : IDisposable
    {
        private bool _disposedValue;

        private CancellationTokenSource Source { get; set; }

        private IWebHost WebHost { get; set; }

        public string HostUri { get; private set; }

        public HttpClient Client { get; private set; }

        public static async Task<TestSession> CreateSessionAsync(string host, int port, string configFile)
        {
            var session = new TestSession
            {
                Source = new CancellationTokenSource()
            };

            var lambdaOptions = new LocalLambdaOptions
            {
                Host = host,
                Port = port,
                LambdaConfigFiles = new List<string> { configFile },
                LambdaRuntime = LocalLambdaRuntime.Initialize(Directory.GetCurrentDirectory())
            };

            session.WebHost = await Startup.StartWebTesterAsync(lambdaOptions, false, session.Source.Token);

            session.HostUri = Utils.DetermineLaunchUrl(host, port, Constants.DEFAULT_HOST);
            session.Client = new HttpClient
            {
                BaseAddress = new Uri(session.HostUri)
            };

            return session;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            if (disposing)
            {
                Client.Dispose();
                Source.Cancel();
                WebHost.StopAsync().GetAwaiter().GetResult();
            }

            _disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}