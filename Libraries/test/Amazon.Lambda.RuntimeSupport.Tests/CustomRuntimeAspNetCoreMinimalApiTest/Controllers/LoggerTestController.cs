using Microsoft.AspNetCore.Mvc;
using Amazon.Lambda.Core;

namespace CustomRuntimeAspNetCoreMinimalApiTest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LoggerTestController : ControllerBase
    {
        [HttpGet()]
        public long Get()
        {
            var lambdaContext = this.HttpContext.Items["LambdaContext"] as ILambdaContext;

            const int maxLogs = 10000;
            long actualLogsWritten = 0;
            Action stdOutTest = () =>
            {
                long index = 0;
                while (index < maxLogs)
                {
                    Console.WriteLine($"StdOut: {index++}");
                    Interlocked.Increment(ref actualLogsWritten);
                    Thread.Yield();
                }
            };

            Action stdErrTest = () =>
            {
                long index = 0;
                while (index < maxLogs)
                {
                    Console.Error.WriteLine($"StdErr: {index++}");
                    Interlocked.Increment(ref actualLogsWritten);
                    Thread.Yield();
                }
            };

            Action contextLoggerTest = () =>
            {
                long index = 0;
                while (index < maxLogs)
                {
                    lambdaContext!.Logger.LogWarning($"ContextLogger: {index++}");
                    Interlocked.Increment(ref actualLogsWritten);
                    Thread.Yield();
                }
            };


            var tasks = new List<Task>();
            for (int i = 0; i < 3; i++)
            {
                tasks.Add(Task.Run(stdOutTest));
                tasks.Add(Task.Run(stdErrTest));
                tasks.Add(Task.Run(contextLoggerTest));
            }

            Task.WaitAll(tasks.ToArray());

            return actualLogsWritten;
        }
    }
}
