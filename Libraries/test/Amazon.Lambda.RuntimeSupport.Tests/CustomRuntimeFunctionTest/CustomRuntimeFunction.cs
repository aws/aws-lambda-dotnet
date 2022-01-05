using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CustomRuntimeFunctionTest
{
    class CustomRuntimeFunction
    {
        private const string FailureResult = "FAILURE";
        private const string SuccessResult = "SUCCESS";
        private const string TestUrl = "https://www.amazon.com";

        private static readonly Lazy<string> SixMBString = new Lazy<string>(() => { return new string('X', 1024 * 1024 * 6); });
        private static readonly Lazy<string> SevenMBString = new Lazy<string>(() => { return new string('X', 1024 * 1024 * 7); });

        private static MemoryStream ResponseStream = new MemoryStream();
        private static DefaultLambdaJsonSerializer JsonSerializer = new DefaultLambdaJsonSerializer();
        private static LambdaEnvironment LambdaEnvironment = new LambdaEnvironment();

        private static async Task Main(string[] args)
        {
            var handler = LambdaEnvironment.Handler;
            LambdaBootstrap bootstrap = null;
            HandlerWrapper handlerWrapper = null;

            try
            {
                switch (handler)
                {
                    case nameof(LoggingStressTest):
                        bootstrap = new LambdaBootstrap(LoggingStressTest);
                        break;
                    case nameof(LoggingTest):
                        bootstrap = new LambdaBootstrap(LoggingTest);
                        break;
                    case nameof(ToUpperAsync):
                        bootstrap = new LambdaBootstrap(ToUpperAsync);
                        break;
                    case nameof(PingAsync):
                        bootstrap = new LambdaBootstrap(PingAsync);
                        break;
                    case nameof(HttpsWorksAsync):
                        bootstrap = new LambdaBootstrap(HttpsWorksAsync);
                        break;
                    case nameof(CertificateCallbackWorksAsync):
                        bootstrap = new LambdaBootstrap(CertificateCallbackWorksAsync);
                        break;
                    case nameof(NetworkingProtocolsAsync):
                        bootstrap = new LambdaBootstrap(NetworkingProtocolsAsync);
                        break;
                    case nameof(HandlerEnvVarAsync):
                        bootstrap = new LambdaBootstrap(HandlerEnvVarAsync);
                        break;
                    case nameof(AggregateExceptionUnwrappedAsync):
                        bootstrap = new LambdaBootstrap(AggregateExceptionUnwrappedAsync);
                        break;
                    case nameof(AggregateExceptionUnwrapped):
                        handlerWrapper = HandlerWrapper.GetHandlerWrapper((Action)AggregateExceptionUnwrapped);
                        bootstrap = new LambdaBootstrap(handlerWrapper);
                        break;
                    case nameof(AggregateExceptionNotUnwrappedAsync):
                        bootstrap = new LambdaBootstrap(AggregateExceptionNotUnwrappedAsync);
                        break;
                    case nameof(AggregateExceptionNotUnwrapped):
                        handlerWrapper = HandlerWrapper.GetHandlerWrapper((Action)AggregateExceptionNotUnwrapped);
                        bootstrap = new LambdaBootstrap(handlerWrapper);
                        break;
                    case nameof(TooLargeResponseBodyAsync):
                        bootstrap = new LambdaBootstrap(TooLargeResponseBodyAsync);
                        break;
                    case nameof(LambdaEnvironmentAsync):
                        bootstrap = new LambdaBootstrap(LambdaEnvironmentAsync);
                        break;
                    case nameof(LambdaContextBasicAsync):
                        bootstrap = new LambdaBootstrap(LambdaContextBasicAsync);
                        break;
                    case nameof(GetPidDllImportAsync):
                        bootstrap = new LambdaBootstrap(GetPidDllImportAsync);
                        break;
                    case nameof(GetTimezoneNameAsync):
                        bootstrap = new LambdaBootstrap(GetTimezoneNameAsync);
                        break;
                    default:
                        throw new Exception($"Handler {handler} is not supported.");
                }
                await bootstrap.RunAsync();
            }
            finally
            {
                handlerWrapper?.Dispose();
                bootstrap?.Dispose();
            }
        }

        private static Task<InvocationResponse> LoggingStressTest(InvocationRequest invocation)
        {
            var source = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = source.Token;

            Task UseWriteAsync()
            {
                return Task.Run(() =>
                {
                    int i = 0;
                    while (!token.IsCancellationRequested)
                    {
                        Thread.Sleep(0);
                        Console.Write($"|Write+{i++}|");
                    }
                });
            }

            Task UseWriteLineAsync()
            {
                return Task.Run(() =>
                {
                    int i = 0;
                    while (!token.IsCancellationRequested)
                    {
                        Thread.Sleep(0);
                        Console.WriteLine($"|WriteLine+{i++}|");
                    }
                });
            }


            Task UseLoggerAsync()
            {
                return Task.Run(() =>
                {
                    int i = 0;
                    while (!token.IsCancellationRequested)
                    {
                        Thread.Sleep(0);
                        invocation.LambdaContext.Logger.LogInformation($"|FormattedWriteLine+{i++}|");
                    }
                });
            }


            var task1 = UseWriteAsync();
            var task2 = UseWriteLineAsync();
            var task3 = UseLoggerAsync();

            Task.WaitAll(task1, task2, task3);

            return Task.FromResult(GetInvocationResponse(nameof(LoggingStressTest), "success"));
        }

        private static Task<InvocationResponse> LoggingTest(InvocationRequest invocation)
        {
            invocation.LambdaContext.Logger.LogTrace("A trace log");
            invocation.LambdaContext.Logger.LogDebug("A debug log");
            invocation.LambdaContext.Logger.LogInformation("A information log");
            invocation.LambdaContext.Logger.LogWarning("A warning log");
            invocation.LambdaContext.Logger.LogError("A error log");
            invocation.LambdaContext.Logger.LogCritical("A critical log");

            Console.WriteLine("A stdout info message");
            Console.Error.WriteLine("A stderror error message");

            Amazon.Lambda.Core.LambdaLogger.Log("A fake message level");

            return Task.FromResult(GetInvocationResponse(nameof(LoggingTest), true));
        }

        private static Task<InvocationResponse> ToUpperAsync(InvocationRequest invocation)
        {
            var input = JsonSerializer.Deserialize<string>(invocation.InputStream);
            return Task.FromResult(GetInvocationResponse(nameof(ToUpperAsync), input.ToUpper()));
        }

        private static Task<InvocationResponse> PingAsync(InvocationRequest invocation)
        {
            var input = JsonSerializer.Deserialize<string>(invocation.InputStream);

            if (input == "ping")
            {
                return Task.FromResult(GetInvocationResponse(nameof(PingAsync), "pong"));
            }
            else
            {
                throw new Exception("Expected input: ping");
            }
        }

        private static async Task<InvocationResponse> HttpsWorksAsync(InvocationRequest invocation)
        {
            var isSuccess = false;

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(TestUrl);
                if (response.IsSuccessStatusCode)
                {
                    isSuccess = true;
                }
                Console.WriteLine($"Response from HTTP get: {response}");
            }

            return GetInvocationResponse(nameof(HttpsWorksAsync), isSuccess);
        }

        private static async Task<InvocationResponse> CertificateCallbackWorksAsync(InvocationRequest invocation)
        {
            var isSuccess = false;

            using (var httpClientHandler = new HttpClientHandler())
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                using (var client = new HttpClient(httpClientHandler))
                {
                    var response = await client.GetAsync(TestUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        isSuccess = true;
                    }
                    Console.WriteLine($"Response from HTTP get: {response}");
                }
            }

            return GetInvocationResponse(nameof(CertificateCallbackWorksAsync), isSuccess);
        }

        private static Task<InvocationResponse> NetworkingProtocolsAsync(InvocationRequest invocation)
        {
            var type = typeof(Socket).GetTypeInfo().Assembly.GetType("System.Net.SocketProtocolSupportPal");
            var method = type.GetMethod("IsSupported", BindingFlags.NonPublic | BindingFlags.Static);
            var ipv4Supported = method.Invoke(null, new object[] { AddressFamily.InterNetwork });
            var ipv6Supported = method.Invoke(null, new object[] { AddressFamily.InterNetworkV6 });

            Exception ipv4SocketCreateException = null;
            try
            {
                using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) { }
            }
            catch (Exception e)
            {
                ipv4SocketCreateException = e;
            }

            Exception ipv6SocketCreateException = null;
            try
            {
                using (Socket s = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)) { }
            }
            catch (Exception e)
            {
                ipv6SocketCreateException = e;
            }

            string returnValue = "";
            if (!(bool)ipv4Supported)
            {
                returnValue += "For System.Net.SocketProtocolSupportPal.IsProtocolSupported(AddressFamily.InterNetwork) Expected true, Actual false" + Environment.NewLine;
            }

            if ((bool)ipv6Supported)
            {
                returnValue += "For System.Net.SocketProtocolSupportPal.IsProtocolSupported(AddressFamily.InterNetworkV6) Expected false, Actual true" + Environment.NewLine;
            }

            if (ipv4SocketCreateException != null)
            {
                returnValue += "Error creating IPV4 Socket: " + ipv4SocketCreateException + Environment.NewLine;
            }

            if (ipv6SocketCreateException == null)
            {
                returnValue += "When creating IPV6 Socket expected exception, got none." + Environment.NewLine;
            }

            if (ipv6SocketCreateException != null && ipv6SocketCreateException.Message != "Address family not supported by protocol")
            {
                returnValue += "When creating IPV6 Socket expected exception 'Address family not supported by protocol', actual '" + ipv6SocketCreateException.Message + "'" + Environment.NewLine;
            }

            if (String.IsNullOrEmpty(returnValue))
            {
                returnValue = "SUCCESS";
            }

            return Task.FromResult(GetInvocationResponse(nameof(NetworkingProtocolsAsync), returnValue));
        }

        private static Task<InvocationResponse> HandlerEnvVarAsync(InvocationRequest invocation)
        {
            return Task.FromResult(GetInvocationResponse(nameof(HandlerEnvVarAsync), LambdaEnvironment.Handler));
        }

        private static async Task<InvocationResponse> AggregateExceptionUnwrappedAsync(InvocationRequest invocation)
        {
            // do something async so this function is compiled as async
            var dummy = await Task.FromResult("xyz");
            throw new Exception("Exception thrown from an async handler.");
        }

        private static void AggregateExceptionUnwrapped()
        {
            throw new Exception("Exception thrown from a synchronous handler.");
        }

        private static async Task<InvocationResponse> AggregateExceptionNotUnwrappedAsync(InvocationRequest invocation)
        {
            // do something async so this function is compiled as async
            var dummy = await Task.FromResult("xyz");
            throw new AggregateException("AggregateException thrown from an async handler.");
        }

        private static void AggregateExceptionNotUnwrapped()
        {
            throw new AggregateException("AggregateException thrown from a synchronous handler.");
        }

        private static Task<InvocationResponse> TooLargeResponseBodyAsync(InvocationRequest invocation)
        {
            return Task.FromResult(GetInvocationResponse(nameof(TooLargeResponseBodyAsync), SevenMBString.Value));
        }

        private static Task<InvocationResponse> LambdaEnvironmentAsync(InvocationRequest invocation)
        {
            AssertNotNull(LambdaEnvironment.FunctionMemorySize, nameof(LambdaEnvironment.FunctionMemorySize));
            AssertNotNull(LambdaEnvironment.FunctionName, nameof(LambdaEnvironment.FunctionName));
            AssertNotNull(LambdaEnvironment.FunctionVersion, nameof(LambdaEnvironment.FunctionVersion));
            AssertNotNull(LambdaEnvironment.Handler, nameof(LambdaEnvironment.Handler));
            AssertNotNull(LambdaEnvironment.LogGroupName, nameof(LambdaEnvironment.LogGroupName));
            AssertNotNull(LambdaEnvironment.LogStreamName, nameof(LambdaEnvironment.LogStreamName));
            AssertNotNull(LambdaEnvironment.RuntimeServerHostAndPort, nameof(LambdaEnvironment.RuntimeServerHostAndPort));
            AssertNotNull(LambdaEnvironment.XAmznTraceId, nameof(LambdaEnvironment.XAmznTraceId));

            return Task.FromResult(GetInvocationResponse(nameof(LambdaEnvironmentAsync), true));
        }

        private static Task<InvocationResponse> LambdaContextBasicAsync(InvocationRequest invocation)
        {
            AssertNotNull(invocation.LambdaContext.AwsRequestId, nameof(invocation.LambdaContext.AwsRequestId));
            AssertNotNull(invocation.LambdaContext.ClientContext, nameof(invocation.LambdaContext.ClientContext));
            AssertNotNull(invocation.LambdaContext.FunctionName, nameof(invocation.LambdaContext.FunctionName));
            AssertNotNull(invocation.LambdaContext.FunctionVersion, nameof(invocation.LambdaContext.FunctionVersion));
            AssertNotNull(invocation.LambdaContext.Identity, nameof(invocation.LambdaContext.Identity));
            AssertNotNull(invocation.LambdaContext.InvokedFunctionArn, nameof(invocation.LambdaContext.InvokedFunctionArn));
            AssertNotNull(invocation.LambdaContext.Logger, nameof(invocation.LambdaContext.Logger));
            AssertNotNull(invocation.LambdaContext.LogGroupName, nameof(invocation.LambdaContext.LogGroupName));
            AssertNotNull(invocation.LambdaContext.LogStreamName, nameof(invocation.LambdaContext.LogStreamName));

            AssertTrue(invocation.LambdaContext.MemoryLimitInMB >= 128,
                $"{nameof(invocation.LambdaContext.MemoryLimitInMB)}={invocation.LambdaContext.MemoryLimitInMB} is not >= 128");
            AssertTrue(invocation.LambdaContext.RemainingTime > TimeSpan.Zero,
                $"{nameof(invocation.LambdaContext.RemainingTime)}={invocation.LambdaContext.RemainingTime} is not >= 0");

            return Task.FromResult(GetInvocationResponse(nameof(LambdaContextBasicAsync), true));
        }

        #region GetPidDllImportAsync
        [DllImport("libc", EntryPoint = "getpid", CallingConvention = CallingConvention.Cdecl)]
        private static extern int getpid();

        private static Task<InvocationResponse> GetPidDllImportAsync(InvocationRequest invocation)
        {
            var isSuccess = getpid() > 0;
            return Task.FromResult(GetInvocationResponse(nameof(GetPidDllImportAsync), isSuccess));
        }
        #endregion

        private static Task<InvocationResponse> GetTimezoneNameAsync(InvocationRequest invocation)
        {
            return Task.FromResult(GetInvocationResponse(nameof(GetTimezoneNameAsync), TimeZoneInfo.Local.Id));
        }

        #region Helpers
        private static void AssertNotNull(object value, string valueName)
        {
            if (value == null)
            {
                throw new Exception($"{valueName} cannot be null.");
            }
        }

        private static void AssertTrue(bool value, string errorMessage)
        {
            if (!value)
            {
                throw new Exception(errorMessage);
            }
        }

        private static InvocationResponse GetInvocationResponse(string testName, bool isSuccess)
        {
            return GetInvocationResponse($"{testName}-{(isSuccess ? SuccessResult : FailureResult)}");
        }

        private static InvocationResponse GetInvocationResponse(string testName, string result)
        {
            return GetInvocationResponse($"{testName}-{result}");
        }

        private static InvocationResponse GetInvocationResponse(string result)
        {

            ResponseStream.SetLength(0);
            JsonSerializer.Serialize(result, ResponseStream);
            ResponseStream.Position = 0;

            return new InvocationResponse(ResponseStream, false);
        }
        #endregion

    }
}
