using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.TestTool.Services;

namespace Amazon.Lambda.TestTool.Runtime
{
    public interface ILocalLambdaRuntime : IDisposable
    {
        string LambdaAssemblyDirectory { get; }

        LambdaFunction LoadLambdaFunction(LambdaFunctionInfo functionInfo);
        IList<LambdaFunction> LoadLambdaFunctions(IList<LambdaFunctionInfo> configInfos);

        Task<ExecutionResponse> ExecuteLambdaFunctionAsync(ExecutionRequest request);

        IAWSService AWSService { get; }
    }

    /// <summary>
    /// A mock Lambda runtime to execute Lambda functions.
    /// </summary>
    public class LocalLambdaRuntime : ILocalLambdaRuntime
    {
        private LambdaAssemblyLoadContext LambdaContext { get; }
        public string LambdaAssemblyDirectory { get; }

        public IAWSService AWSService { get; }


        private LocalLambdaRuntime(LambdaAssemblyLoadContext lambdaContext, string lambdaAssemblyDirectory, IAWSService awsService)
        {
            LambdaContext = lambdaContext;
            this.LambdaAssemblyDirectory = lambdaAssemblyDirectory;
            this.AWSService = awsService;
        }

        public static ILocalLambdaRuntime Initialize(string directory)
        {
            return Initialize(directory, new AWSServiceImpl());
        }

        public static ILocalLambdaRuntime Initialize(string directory, IAWSService awsService)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Directory containing built Lambda project does not exist {directory}");
            }

            var depsFile = Directory.GetFiles(directory, "*.deps.json").FirstOrDefault();
            if (depsFile == null)
            {
                throw new Exception($"Failed to find a deps.json file in the specified directory ({directory})");
            }

            var fileName = depsFile.Substring(0, depsFile.Length - ".deps.json".Length) + ".dll";
            if (!File.Exists(fileName))
            {
                throw new Exception($"Failed to find Lambda project entry assembly in the specified directory ({directory})");
            }

            // The resolver provides the ability to load the assemblies containing the select Lambda function.
            var resolver = new LambdaAssemblyLoadContext(fileName);

            var runtime = new LocalLambdaRuntime(resolver, directory, awsService);
            return runtime;
        }

        public IList<LambdaFunction> LoadLambdaFunctions(IList<LambdaFunctionInfo> configInfos)
        {
            var functions = new List<LambdaFunction>();

            foreach (var configInfo in configInfos)
            {
                functions.Add(LoadLambdaFunction(configInfo));
            }

            return functions;
        }

        /// <summary>
        /// Find the reflection objects for the code that will be executed for the Lambda function based on the
        /// Lambda function handler.
        /// </summary>
        /// <param name="functionInfo"></param>
        /// <returns></returns>
        public LambdaFunction LoadLambdaFunction(LambdaFunctionInfo functionInfo)
        {
            var function = new LambdaFunction(functionInfo);
            var handlerTokens = functionInfo.Handler.Split("::");

            if (handlerTokens.Length != 3)
            {
                function.ErrorMessage = $"Invalid format for function handler string {functionInfo.Handler}. Format is <assembly>::<type-name>::<method>.";
                return function;
            }

            // Using our custom Assembly resolver load the target Assembly.
            function.LambdaAssembly = this.LambdaContext.LoadFromAssemblyName(new AssemblyName(handlerTokens[0]));
            if (function.LambdaAssembly == null)
            {
                function.ErrorMessage = $"Failed to find assembly {handlerTokens[0]}";
                return function;
            }

            function.LambdaType = function.LambdaAssembly.GetType(handlerTokens[1]);
            if (function.LambdaType == null)
            {
                function.ErrorMessage = $"Failed to find type {handlerTokens[1]}";
                return function;
            }

            var methodInfos = function.LambdaType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(x => string.Equals(x.Name, handlerTokens[2])).ToArray();

            if (methodInfos.Length == 1)
            {
                function.LambdaMethod = methodInfos[0];
            }
            else
            {
                // TODO: Handle method overloads
                if (methodInfos.Length > 1)
                {
                    function.ErrorMessage = $"More then one method called {handlerTokens[2]} was found. This tool does not currently support method overloading.";
                }
                else
                {
                    function.ErrorMessage = $"Failed to find method {handlerTokens[2]}";
                }
                return function;
            }

            // Search to see if a Lambda serializer is registered.
            var attribute = function.LambdaMethod.GetCustomAttribute(typeof(LambdaSerializerAttribute)) as LambdaSerializerAttribute ??
                            function.LambdaAssembly.GetCustomAttribute(typeof(LambdaSerializerAttribute)) as LambdaSerializerAttribute;


            if (attribute != null)
            {
                function.Serializer = Activator.CreateInstance(attribute.SerializerType) as ILambdaSerializer;
            }


            return function;
        }

        /// <summary>
        /// Execute the Lambda function.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>        
        public async Task<ExecutionResponse> ExecuteLambdaFunctionAsync(ExecutionRequest request)
        {
            return await (new LambdaExecutor()).ExecuteAsync(request);
        }


        public void Dispose()
        {
        }
    }
}