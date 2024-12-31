using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private static readonly ConcurrentDictionary<string, string> _registeredExtensions = new ();

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

        [HttpPost("/2020-01-01/extension/register")]
        public async Task<IActionResult> PostRegisterExtension()
        {
            using var reader = new StreamReader(Request.Body);
            // Read and discard - do not need the body
            await reader.ReadToEndAsync();

            var extensionId = Guid.NewGuid().ToString();
            var extensionName = Request.Headers["Lambda-Extension-Name"];
            _registeredExtensions.TryAdd(extensionId, extensionName);

            Response.Headers["Lambda-Extension-Identifier"] = extensionId;
            return Ok();
        }

        [HttpGet("/2020-01-01/extension/event/next")]
        public async Task<IActionResult> GetNextExtensionEvent()
        {
            var extensionId = Request.Headers["Lambda-Extension-Identifier"];
            if (_registeredExtensions.ContainsKey(extensionId)) {
                await Task.Delay(TimeSpan.FromSeconds(15));
                Console.WriteLine(HEADER_BREAK);
                Console.WriteLine($"Extension ID: {extensionId} - Returning NoContent");
                return NoContent();
            } else {
                Console.WriteLine(HEADER_BREAK);
                Console.Error.WriteLine($"Extension ID: {extensionId} - Was not found - Returning 404 - ");
                return NotFound();
            }
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
                try {
                    tcs.SetResult();
                } catch (InvalidOperationException) {
                    // This can happen if both OnSuccess and OnError are called
                    // Oddly, this does happen 1 time in 50 million requests
                    // We can't check a variable because it's a race condition
                }
            };
            
            eventContainer.OnError += () =>
            {
                try {
                    tcs.SetResult();
                } catch (InvalidOperationException) {
                    // See note above
                }
            };

            // Wait for our event to process
            // TODO: This is where we can timeout if the event was not processed in time
            try {
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(60));
            } catch (TimeoutException e) {
                eventContainer.ReportErrorResponse("Task Timed Out", "Error");

                return Ok(new {
                    StatusCode = 202,
                    FunctionError = "Unhandled",
                    ExecutedVersion = "$LATEST",
                    Payload = "{\"errorMessage\":\"Task Timed Out\",\"errorType\":\"Error\"}"
                });
            } catch (Exception e) {
                // This is a catch all for any other exceptions
                // We should not get here
                eventContainer.ReportErrorResponse("Unhandled Error", "Error");

                return Ok(new {
                    StatusCode = 202,
                    FunctionError = "Unhandled",
                    ExecutedVersion = "$LATEST",
                    Payload = "{\"errorMessage\":\"Unhandled Error\",\"errorType\":\"Error\"}"
                });
            }

            if (eventContainer.ErrorResponse != null)
            {
                if (eventContainer.ErrorType == "Throttled")
                {
                    return Ok(new {
                        StatusCode = 429,
                        FunctionError = "Throttled",
                        ExecutedVersion = "$LATEST",
                        Payload = "{\"errorMessage\":\"Rate Exceeded.\"}"
                    });
                }
                return Ok(new {
                    StatusCode = 200,
                    FunctionError = "Unhandled",
                    ExecutedVersion = "$LATEST",
                    Payload = "{\"errorMessage\":\"An error occurred.\",\"errorType\":\"Error\"}"
                });
            }

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
            
            // Set Deadline to 5 minutes from now in unix epoch ms
            Response.Headers["Lambda-Runtime-Deadline-Ms"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds().ToString();
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

            // TODO: The response from this function is the active event
            // The event gets run by the lambda that called this endpoint

            // This is where we need to setup a timeout for the event
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