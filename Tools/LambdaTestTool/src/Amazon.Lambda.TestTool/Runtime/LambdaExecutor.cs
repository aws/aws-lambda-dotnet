using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestTool.Runtime.LambdaMocks;

namespace Amazon.Lambda.TestTool.Runtime
{
    public class LambdaExecutor
    {        
        private static readonly SemaphoreSlim executeSlim = new SemaphoreSlim(1, 1);
        
        public async Task<ExecutionResponse> ExecuteAsync(ExecutionRequest request)
        {
            var logger = new LocalLambdaLogger();
            var response = new ExecutionResponse();

            if (!string.IsNullOrEmpty(request.Function.ErrorMessage))
            {
                response.Error = request.Function.ErrorMessage;
                return response;
            }

            try
            {
                if (!string.IsNullOrEmpty(request.AWSRegion))
                {
                    Environment.SetEnvironmentVariable("AWS_REGION", request.AWSRegion);
                }
                if (!string.IsNullOrEmpty(request.AWSProfile))
                {
                    Environment.SetEnvironmentVariable("AWS_PROFILE", request.AWSProfile);
                }


                var context = new LocalLambdaContext()
                {
                    Logger = logger
                };

                object instance = null;
                if (!request.Function.LambdaMethod.IsStatic)
                {
                    instance = Activator.CreateInstance(request.Function.LambdaType);
                }

                var parameters = BuildParameters(request, context);

                // Because a Lambda compute environment never executes more then one event at a time
                // create a lock around the execution to match that environment.
                await executeSlim.WaitAsync();
                try
                {
                    using (var wrapper = new ConsoleOutWrapper(logger))
                    {
                        var lambdaReturnObject = request.Function.LambdaMethod.Invoke(instance, parameters);
                        response.Response = await ProcessReturnAsync(request, lambdaReturnObject);
                    }
                }
                finally
                {
                    executeSlim.Release();
                }
            }
            catch (TargetInvocationException e)
            {
                response.Error = GenerateErrorMessage(e.InnerException);
            }
            catch (Exception e)
            {
                response.Error = GenerateErrorMessage(e);
            }

            response.Logs = logger.Buffer;

            return response;
        }

        private static string SeachForDllNotFoundException(Exception e)
        {
            var excep = e;
            do
            {
                if (excep is DllNotFoundException)
                    return excep.Message;
                excep = excep.InnerException;
            } while (excep != null);

            return null;
        }

        public static string GenerateErrorMessage(Exception e)
        {
            var dllNotFoundMessage = SeachForDllNotFoundException(e);
            if (!string.IsNullOrEmpty(dllNotFoundMessage))
                return dllNotFoundMessage;

            StringBuilder sb = new StringBuilder();

            if (e is AggregateException)
                e = e.InnerException;


            var exceptionDepth = 0;
            while (e != null)
            {
                if (sb.Length > 0)
                    sb.AppendLine($"---------------- Inner {exceptionDepth} Exception ------------");
                
                sb.AppendLine($"{e.GetType().FullName}: {e.Message}");
                sb.AppendLine(e.StackTrace);


                e = e.InnerException;
                exceptionDepth++;
            }
            
            
            
            return sb.ToString();
        }

        public static async Task<string> ProcessReturnAsync(ExecutionRequest request, object lambdaReturnObject)
        {
            Stream lambdaReturnStream = null;

            if (lambdaReturnObject == null)
                return null;

            // If the return was a Task then wait till the task is complete.
            if (lambdaReturnObject is Task task)
            {
                await task;

                // Check to see if the Task returns back an object.
                if (task.GetType().IsGenericType)
                {
                    var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                    if (resultProperty != null)
                    {
                        var taskResult = resultProperty.GetMethod.Invoke(task, null);
                        if (taskResult is Stream stream)
                        {
                            lambdaReturnStream = stream;
                        }
                        else
                        {
                            lambdaReturnStream = new MemoryStream();
                            request.Function.Serializer.Serialize(taskResult, lambdaReturnStream);
                        }
                    }
                }
            }
            else
            {
                lambdaReturnStream = new MemoryStream();
                request.Function.Serializer.Serialize(lambdaReturnObject, lambdaReturnStream);
            }

            if (lambdaReturnStream == null)
                return null;

            lambdaReturnStream.Position = 0;
            using (var reader = new StreamReader(lambdaReturnStream))
            {
                return reader.ReadToEnd();
            }

        }

        /// <summary>
        /// Create the parameter array that will be passed into the Invoke for the Lambda function.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static object[] BuildParameters(ExecutionRequest request, ILambdaContext context)
        {
            var parms = request.Function.LambdaMethod.GetParameters();
            var parmValues = new object[parms.Length];
            
            if(parmValues.Length > 2)
                throw new Exception($".NET Method has too many parameters, {parmValues.Length}. Methods called by Lambda can have at most 2 parameters. The first is the input object and the second is an ILambdaContext.");
            
            for (var i = 0; i < parms.Length; i++)
            {
                if (parms[i].ParameterType == typeof(ILambdaContext))
                {
                    parmValues[i] = context;
                }
                else
                {
                    var bytes = Encoding.UTF8.GetBytes((request.Payload != null) ? request.Payload : "{}");
                    var stream = new MemoryStream(bytes);
                    if (request.Function.Serializer != null)
                    {
                        var genericMethodInfo = request.Function.Serializer.GetType()
                            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(x => string.Equals(x.Name, "Deserialize"));

                        var methodInfo = genericMethodInfo.MakeGenericMethod(new Type[]{parms[i].ParameterType});

                        try
                        {
                            parmValues[i] = methodInfo.Invoke(request.Function.Serializer, new object[] {stream});
                        }
                        catch (Exception e)
                        {
                            throw new Exception($"Error deserializing the input JSON to type {parms[i].ParameterType.Name}", e);
                        }
                    }
                    else
                    {
                        parmValues[i] = stream;
                    }
                }
            }

            return parmValues;
        }
    }
}