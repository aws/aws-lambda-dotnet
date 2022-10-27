using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.TestTool.Runtime.LambdaMocks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Amazon.Lambda.TestTool.BlazorTester.Controllers
{
    [Route("[controller]")]
    public class InvokeApiController : ControllerBase
    {
        private readonly LocalLambdaOptions _lambdaOptions;
        private LambdaConfigInfo _lambdaConfig;

        public InvokeApiController(LocalLambdaOptions lambdaOptions)
        {
            _lambdaOptions = lambdaOptions;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteFunction()
        {
            if (!TryGetConfigFile(out var lambdaConfig))
            {
                return StatusCode((int)HttpStatusCode.InternalServerError,
                    new InternalException("ServiceException", "Error while loading function configuration"));
            }

            if (lambdaConfig.FunctionInfos.Count == 0)
            {
                return NotFound(new InternalException("ResourceNotFoundException", "Default function not found"));
            }

            return Ok(await ExecuteFunctionInternal(lambdaConfig, lambdaConfig.FunctionInfos[0]));
        }

        [HttpPost("execute/{functionName}")]
        [HttpPost("2015-03-31/functions/{functionName}/invocations")]
        public async Task<object> ExecuteFunction(string functionName)
        {
            if (!TryGetConfigFile(out var lambdaConfig))
            {
                return StatusCode((int)HttpStatusCode.InternalServerError,
                    new InternalException("ServiceException", "Error while loading function configuration"));
            }

            var functionInfo = lambdaConfig.FunctionInfos.FirstOrDefault(f => f.Name == functionName);
            if (functionInfo == null)
            {
                return NotFound(new InternalException("ResourceNotFoundException",
                    $"Function not found: {functionName}"));
            }

            return Ok(await ExecuteFunctionInternal(lambdaConfig, functionInfo));
        }

        private bool TryGetConfigFile(out LambdaConfigInfo configInfo)
        {
            configInfo = null;

            if (_lambdaConfig != null)
            {
                configInfo = _lambdaConfig;
                return true;
            }

            if (_lambdaOptions.LambdaConfigFiles.Count == 0)
            {
                Console.Error.WriteLine("LambdaConfigFiles list is empty");
                return false;
            }

            var configPath = _lambdaOptions.LambdaConfigFiles[0];
            try
            {
                configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(configPath);
                _lambdaConfig = configInfo;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error loading lambda config from '{0}'", configPath);
                Console.Error.WriteLine(e.ToString());
            }

            return true;
        }

        private async Task<object> ExecuteFunctionInternal(LambdaConfigInfo lambdaConfig,
            LambdaFunctionInfo functionInfo)
        {
            var requestReader = new LambdaRequestReader(Request);
            var function = _lambdaOptions.LoadLambdaFuntion(lambdaConfig, functionInfo.Handler);

            var request = new ExecutionRequest
            {
                Function = function,
                AWSProfile = lambdaConfig.AWSProfile,
                AWSRegion = lambdaConfig.AWSRegion,
                Payload = await requestReader.ReadPayload(),
                ClientContext = requestReader.ReadClientContext()
            };

            var response = await _lambdaOptions.LambdaRuntime.ExecuteLambdaFunctionAsync(request);
            var responseWriter = new LambdaResponseWriter(Response);

            if (requestReader.ReadLogType() == "Tail")
            {
                responseWriter.WriteLogs(response.Logs);
            }

            if (!response.IsSuccess)
            {
                responseWriter.WriteError();
                return new LambdaException(response.Error);
            }

            return response.Response;
        }

        private class LambdaRequestReader
        {
            private const string LogTypeHeader = "X-Amz-Log-Type";
            private const string ClientContextHeader = "X-Amz-Client-Context";

            private readonly HttpRequest _request;

            public LambdaRequestReader(HttpRequest request)
            {
                _request = request;
            }

            public async Task<string> ReadPayload()
            {
                using var reader = new StreamReader(_request.Body);
                return await reader.ReadToEndAsync();
            }

            public string ReadLogType()
            {
                return _request.Headers.TryGetValue(LogTypeHeader, out var value)
                    ? value.ToString()
                    : string.Empty;
            }

            public IClientContext ReadClientContext()
            {
                if (!_request.Headers.TryGetValue(ClientContextHeader, out var contextString))
                {
                    return null;
                }

                var clientContext = JsonSerializer.Deserialize<LocalClientContext>(
                    Convert.FromBase64String(contextString),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return clientContext;
            }
        }

        private class LambdaResponseWriter
        {
            private const string FunctionErrorHeader = "X-Amz-Function-Error";
            private const string LogResultHeader = "X-Amz-Log-Result";

            private readonly HttpResponse _response;

            public LambdaResponseWriter(HttpResponse response)
            {
                _response = response;
            }

            public void WriteError()
            {
                _response.Headers[FunctionErrorHeader] = "Unhandled";
            }

            public void WriteLogs(string logs)
            {
                _response.Headers[LogResultHeader] = Convert.ToBase64String(Encoding.UTF8.GetBytes(logs));
            }
        }

        private class InternalException
        {
            public string ErrorCode { get; }

            public string ErrorMessage { get; }

            public InternalException(string errorCode, string errorMessage)
            {
                ErrorCode = errorCode;
                ErrorMessage = errorMessage;
            }
        }

        private class LambdaException
        {
            public string ErrorType { get; }

            public string ErrorMessage { get; }

            public string[] StackTrace { get; }

            public LambdaException(string error)
            {
                var errorLines = error.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (errorLines.Length == 0)
                {
                    StackTrace = Array.Empty<string>();
                    return;
                }

                StackTrace = errorLines.Skip(1).Select(s => s.Trim()).ToArray();

                var errorMessage = errorLines[0];
                var errorTypeDelimiterPos = errorMessage.IndexOf(':');
                if (errorTypeDelimiterPos > 0)
                {
                    ErrorType = errorMessage.Substring(0, errorTypeDelimiterPos).Trim();
                    ErrorMessage = errorMessage.Substring(errorTypeDelimiterPos + 1).Trim();
                }
                else
                {
                    ErrorMessage = errorMessage;
                }
            }
        }
    }
}