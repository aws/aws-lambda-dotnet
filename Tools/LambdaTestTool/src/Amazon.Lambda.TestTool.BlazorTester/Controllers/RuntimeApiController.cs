using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.TestTool.BlazorTester.Services;
using Microsoft.AspNetCore.Mvc;

namespace Amazon.Lambda.TestTool.BlazorTester.Controllers
{
#if NET6_0_OR_GREATER
    public class RuntimeApiController : ControllerBase
    {
        private const string HEADER_BREAK = "-----------------------------------";
        private readonly IRuntimeApiDataStore _runtimeApiDataStore;

        public RuntimeApiController(IRuntimeApiDataStore runtimeApiDataStore)
        {
            _runtimeApiDataStore = runtimeApiDataStore;
        }

        [HttpPost("/runtime/test-event")]
        public async Task<IActionResult> PostTestEvent()
        {
            using var reader = new StreamReader(Request.Body);
            var testEvent = await reader.ReadToEndAsync();
            _runtimeApiDataStore.QueueEvent(testEvent);

            return Accepted();
        }

        [HttpPost("/2015-03-31/functions/{functionName}/invocations")]
        public async Task<IActionResult> PostTestInvokeEvent(string functionName)
        {
            using var reader = new StreamReader(Request.Body);
            var testEvent = await reader.ReadToEndAsync();
            var eventContainer = _runtimeApiDataStore.QueueEvent(testEvent);

            // Need a task completion source so we can block until the event is executed.
            var tcs = new TaskCompletionSource();

            eventContainer.OnSuccess += () =>
            {
                tcs.SetResult();
            };
            
            eventContainer.OnError += () =>
            {
                tcs.SetResult();
            };

            // Wait for our event to process
            await tcs.Task;

            var response = new 
            {
                StatusCode = 200, // Accepted
                // FunctionError = null, // TODO: Set this if there was an error
                // LogResult = null, // TODO: Set this to the base64-encoded last 4 KB of log data produced by the function
                Payload = eventContainer.Response, // Set this to the response from the function
                // ExecutedVersion = null // TODO: Set this to the version of the function that was executed
            };

            return Ok(response);
        }
        
        [HttpPost("/2018-06-01/runtime/init/error")]
        public IActionResult PostInitError([FromHeader(Name = "Lambda-Runtime-Function-Error-Type")] string errorType, [FromBody] string error)
        {
            Console.Error.WriteLine("Init Error Type: " + errorType);
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(HEADER_BREAK);
            return Accepted(new StatusResponse{Status = "success"});
        }
        
        [HttpGet("/2018-06-01/runtime/invocation/next")]
        public async Task GetNextInvocation()
        {
            IEventContainer activeEvent;
            while (!_runtimeApiDataStore.TryActivateEvent(out activeEvent))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            Console.WriteLine(HEADER_BREAK);
            Console.WriteLine($"Next invocation returned: {activeEvent.AwsRequestId}");
            
            Response.Headers["Lambda-Runtime-Aws-Request-Id"] = activeEvent.AwsRequestId;
            Response.Headers["Lambda-Runtime-Trace-Id"] = Guid.NewGuid().ToString();
            Response.Headers["Lambda-Runtime-Invoked-Function-Arn"] = activeEvent.FunctionArn;
            Response.StatusCode = 200;

            if (activeEvent.EventJson?.Length != 0)
            {
                // The event is written directly to the response stream to avoid ASP.NET Core attempting any
                // encoding on content passed in the Ok() method.
                Response.Headers["Content-Type"] = "application/json";
                var buffer = UTF8Encoding.UTF8.GetBytes(activeEvent.EventJson);
                await Response.Body.WriteAsync(buffer, 0, buffer.Length);
                Response.Body.Close();
            }
        }
        
        [HttpPost("/2018-06-01/runtime/invocation/{awsRequestId}/response")]
        public async Task<IActionResult> PostInvocationResponse(string awsRequestId)
        {
            using var reader = new StreamReader(Request.Body);
            var response = await reader.ReadToEndAsync();
            
            _runtimeApiDataStore.ReportSuccess(awsRequestId, response);
            
            Console.WriteLine(HEADER_BREAK);
            Console.WriteLine($"Response for request {awsRequestId}");
            Console.WriteLine(response);

            return Accepted(new StatusResponse{Status = "success"});
        }

        [HttpPost("/2018-06-01/runtime/invocation/{awsRequestId}/error")]
        public async Task<IActionResult> PostError(string awsRequestId, [FromHeader(Name = "Lambda-Runtime-Function-Error-Type")] string errorType)
        {
            using var reader = new StreamReader(Request.Body);
            var errorBody = await reader.ReadToEndAsync();
            
            _runtimeApiDataStore.ReportError(awsRequestId, errorType, errorBody);
            await Console.Error.WriteLineAsync(HEADER_BREAK);
            await Console.Error.WriteLineAsync($"Request {awsRequestId} Error Type: {errorType}");
            await Console.Error.WriteLineAsync(errorBody);
            
            return Accepted(new StatusResponse{Status = "success"});
        }
    }
    
    internal class StatusResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; }
    }
#endif
}