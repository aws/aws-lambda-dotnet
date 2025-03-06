using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    /// <summary>
    /// should not be public
    /// this is public to allow LambdaRuntimeSupportServer access,
    /// otherwise it would need access to <see cref="InvocationRequest"/> and
    /// <see cref="LambdaContext"/>
    ///
    /// TODO - inline to LambdaSnapstartExecuteRequestsBeforeSnapshotHelper
    /// </summary>
    public static class SnapstartHelperLambdaRequests
    {
        private static InternalLogger _logger = InternalLogger.GetDefaultLogger();

        private static readonly RuntimeApiHeaders _fakeRuntimeApiHeaders = new(new Dictionary<string, IEnumerable<string>>
        {
            { RuntimeApiHeaders.HeaderAwsRequestId, new List<string>() },
            { RuntimeApiHeaders.HeaderTraceId, new List<string>() },
            { RuntimeApiHeaders.HeaderClientContext, new List<string>() },
            { RuntimeApiHeaders.HeaderCognitoIdentity, new List<string>() },
            { RuntimeApiHeaders.HeaderDeadlineMs, new List<string>() },
            { RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string>() },
        });

        public static async Task ExecuteSnapstartInitRequests(string jsonRequest, int times, HandlerWrapper handlerWrapper)
        {
            var dummyRequest = new InvocationRequest
            {
                InputStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonRequest)),
                LambdaContext = new LambdaContext(
                    _fakeRuntimeApiHeaders,
                    new LambdaEnvironment(),
                    new SimpleLoggerWriter())
            };

            for (var i = 0; i < times; i++)
            {
                try
                {
                    _ = await handlerWrapper.Handler.Invoke(dummyRequest);
                }
                catch (Exception e)
                {
                    Console.WriteLine("StartAsync: " + e.Message + e.StackTrace);
                    _logger.LogError(e, "StartAsync: Custom Warmup Failure: " + e.Message + e.StackTrace);
                }
            }
        }
    }
}
