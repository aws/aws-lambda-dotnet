using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.TestTool.WebTester.Models;
using Amazon.Lambda.TestTool.SampleRequests;
using Microsoft.AspNetCore.Mvc;


namespace Amazon.Lambda.TestTool.WebTester.Controllers
{
    [Route("webtester-api/[controller]")]
    public class TesterController : Controller
    {
        public LocalLambdaOptions LambdaOptions { get; set; }

        public TesterController(LocalLambdaOptions lambdaOptions)
        {
            this.LambdaOptions = lambdaOptions;
        }

        [HttpGet("{configFile}")]
        public ConfigFileSummary GetFunctions(string configFile)
        {
            var fullConfigFilePath = this.LambdaOptions.LambdaConfigFiles.FirstOrDefault(x =>
                string.Equals(configFile, Path.GetFileName(x), StringComparison.OrdinalIgnoreCase));
            if (fullConfigFilePath == null)
            {
                throw new Exception($"{configFile} is not a config file for this project");
            }
            
            var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(fullConfigFilePath);
            var functions = this.LambdaOptions.LambdaRuntime.LoadLambdaFunctions(configInfo.FunctionInfos);

            var summary = new ConfigFileSummary
            {
                AWSProfile = configInfo.AWSProfile,
                AWSRegion = configInfo.AWSRegion,
                Functions = new List<FunctionSummary>()
            };
            
            foreach (var function in functions)
            {
                summary.Functions.Add(new FunctionSummary()
                {
                    FunctionName = function.FunctionInfo.Name,
                    FunctionHandler = function.FunctionInfo.Handler,
                    ErrorMessage =  function.ErrorMessage
                });                
            }

            return summary;
        }

        [HttpPost("{configFile}/{functionHandler}")]
        public async Task<ExecutionResponse> ExecuteFunction(string configFile, string functionHandler)
        {
            var function = this.LambdaOptions.LoadLambdaFuntion(configFile, functionHandler);
            string functionInvokeParams = null;
            if (this.Request.ContentLength > 0)
            {
                using (var reader = new StreamReader(this.Request.Body))
                {
                    functionInvokeParams = await reader.ReadToEndAsync();
                }
            }

            var request = new ExecutionRequest()
            {
                Function = function
            };

            if (!string.IsNullOrEmpty(functionInvokeParams))
            {
                var parameters = System.Text.Json.JsonSerializer.Deserialize<InvokeParameters>(functionInvokeParams, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                request.Payload = parameters.Payload;
                request.AWSProfile = parameters.Profile;
                request.AWSRegion = parameters.Region;
            }
            



            var response = await this.LambdaOptions.LambdaRuntime.ExecuteLambdaFunctionAsync(request);
            return response;
        }

        [HttpGet("request/{requestName}")]
        public string GetLambdaRequest(string requestName)
        {
            var manager = new SampleRequestManager(this.LambdaOptions.GetPreferenceDirectory(false));
            return manager.GetRequest(requestName);
        }

        [HttpPost("request/{requestName}")]
        public async Task<string> SaveLambdaRequest(string requestName)
        {
            using (var reader = new StreamReader(this.Request.Body))
            {
                var content = await reader.ReadToEndAsync();
                var manager = new SampleRequestManager(this.LambdaOptions.GetPreferenceDirectory(true));
                return manager.SaveRequest(requestName, content);
            }
        }
    }
}
