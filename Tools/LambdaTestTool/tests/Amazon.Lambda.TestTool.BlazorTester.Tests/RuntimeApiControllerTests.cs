using Amazon.Lambda.TestTool.BlazorTester.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Amazon.Lambda.TestTool.BlazorTester.Tests
{
    public class RuntimeApiControllerTests
    {
        [Fact]
        public async Task ExecuteEventSuccessfully()
        {
            using var session = await TestSession.CreateSessionAsync(10111);

            using var queueResponse = await session.Client.SendAsync(new HttpRequestMessage
            {
                RequestUri = new Uri("/runtime/test-event", UriKind.Relative),
                Method = HttpMethod.Post,
                Content = new StringContent("\"raw data\"")
            });
            Assert.Equal(HttpStatusCode.Accepted, queueResponse.StatusCode);
            Assert.Single(session.Store.QueuedEvents);

            using var getNextInvokeResponse = await session.Client.SendAsync(new HttpRequestMessage
            {
                RequestUri = new Uri("/2018-06-01/runtime/invocation/next", UriKind.Relative),
                Method = HttpMethod.Get,
            });
            Assert.Equal(HttpStatusCode.OK, getNextInvokeResponse.StatusCode);
            Assert.Empty(session.Store.QueuedEvents);

            var awsRequestId = getNextInvokeResponse.Headers.GetValues("Lambda-Runtime-Aws-Request-Id").FirstOrDefault();
            Assert.Equal(session.Store.ActiveEvent.AwsRequestId, awsRequestId);

            Assert.Single(getNextInvokeResponse.Headers.GetValues("Lambda-Runtime-Trace-Id"));
            Assert.Single(getNextInvokeResponse.Headers.GetValues("Lambda-Runtime-Invoked-Function-Arn"));

            var responseBody = await getNextInvokeResponse.Content.ReadAsStringAsync();
            Assert.Equal("\"raw data\"", responseBody);

            using var postSuccessResponse = await session.Client.SendAsync(new HttpRequestMessage
            {
                RequestUri = new Uri($"/2018-06-01/runtime/invocation/{awsRequestId}/response", UriKind.Relative),
                Method = HttpMethod.Post,
                Content = new StringContent("\"Everything is good here\"")
            });
            Assert.Equal(HttpStatusCode.Accepted, postSuccessResponse.StatusCode);
            Assert.Equal(IEventContainer.Status.Success, session.Store.ActiveEvent.EventStatus);
            Assert.Equal("\"Everything is good here\"", session.Store.ActiveEvent.Response);
        }

        [Fact]
        public async Task ExecuteEventFailure()
        {
            using var session = await TestSession.CreateSessionAsync(10112);

            using var queueResponse = await session.Client.SendAsync(new HttpRequestMessage
            {
                RequestUri = new Uri("/runtime/test-event", UriKind.Relative),
                Method = HttpMethod.Post,
                Content = new StringContent("\"this event will fail\"")
            });
            Assert.Equal(HttpStatusCode.Accepted, queueResponse.StatusCode);
            Assert.Single(session.Store.QueuedEvents);

            using var getNextInvokeResponse = await session.Client.SendAsync(new HttpRequestMessage
            {
                RequestUri = new Uri("/2018-06-01/runtime/invocation/next", UriKind.Relative),
                Method = HttpMethod.Get,
            });
            Assert.Equal(HttpStatusCode.OK, getNextInvokeResponse.StatusCode);
            Assert.Empty(session.Store.QueuedEvents);

            var awsRequestId = getNextInvokeResponse.Headers.GetValues("Lambda-Runtime-Aws-Request-Id").FirstOrDefault();
            Assert.Equal(session.Store.ActiveEvent.AwsRequestId, awsRequestId);

            Assert.Single(getNextInvokeResponse.Headers.GetValues("Lambda-Runtime-Trace-Id"));
            Assert.Single(getNextInvokeResponse.Headers.GetValues("Lambda-Runtime-Invoked-Function-Arn"));

            var responseBody = await getNextInvokeResponse.Content.ReadAsStringAsync();
            Assert.Equal("\"this event will fail\"", responseBody);

            var postFailureRequest = new HttpRequestMessage
            {
                RequestUri = new Uri($"/2018-06-01/runtime/invocation/{awsRequestId}/error", UriKind.Relative),
                Method = HttpMethod.Post,
                Content = new StringContent("\"Big long exception stacktrace\"")
            };
            postFailureRequest.Headers.Add("Lambda-Runtime-Function-Error-Type", "Company.MyException");

            using var postFailureResponse = await session.Client.SendAsync(postFailureRequest);
            Assert.Equal(HttpStatusCode.Accepted, postFailureResponse.StatusCode);
            Assert.Equal(IEventContainer.Status.Failure, session.Store.ActiveEvent.EventStatus);
            Assert.Equal("Company.MyException", session.Store.ActiveEvent.ErrorType);
            Assert.Equal("\"Big long exception stacktrace\"", session.Store.ActiveEvent.ErrorResponse);
        }

        [Fact]
        public async Task InitError()
        {
            using var session = await TestSession.CreateSessionAsync(10113);

            using var queueResponse = await session.Client.SendAsync(new HttpRequestMessage
            {
                RequestUri = new Uri("/2015-03-31/functions/function/invocations", UriKind.Relative),
                Method = HttpMethod.Post,
                Content = new StringContent("\"Failed to startup\"")
            });
            Assert.Equal(HttpStatusCode.Accepted, queueResponse.StatusCode);
        }

        public class TestSession : IDisposable
        {
            private bool disposedValue;

            private CancellationTokenSource Source { get; set; }

            private int Port { get; set; }

            private IWebHost Host { get; set; }

            public HttpClient Client { get; private set; }

            public IRuntimeApiDataStore Store { get; private set; }

            public static async Task<TestSession> CreateSessionAsync(int port)
            {
                var session = new TestSession()
                {
                    Port = port,
                    Source = new CancellationTokenSource()
                };

                session.Host = await Startup.StartWebTesterAsync(new LocalLambdaOptions { Port = port }, false, session.Source.Token);

                session.Client = new HttpClient()
                {
                    BaseAddress = new Uri("http://localhost:" + session.Port)
                };

                session.Store = session.Host.Services.GetService<IRuntimeApiDataStore>();

                return session;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        this.Client.Dispose();
                        this.Source.Cancel();
                        this.Host.StopAsync().GetAwaiter().GetResult();
                    }

                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}
