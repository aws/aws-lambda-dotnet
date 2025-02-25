// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.Services;

public class LambdaRuntimeApiTests
{
    private readonly Mock<IRuntimeApiDataStore> _mockRuntimeDataStore;
    private readonly WebApplication _app;
    private readonly Mock<RuntimeApiDataStore> _mockRuntimeApiDataStore;

    public LambdaRuntimeApiTests()
    {
        Mock<IRuntimeApiDataStoreManager> mockDataStoreManager = new();
        _mockRuntimeDataStore = new Mock<IRuntimeApiDataStore>();
        _mockRuntimeApiDataStore = new Mock<RuntimeApiDataStore>();

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(mockDataStoreManager.Object);
        _app = builder.Build();

        mockDataStoreManager
            .Setup(x => x.GetLambdaRuntimeDataStore(It.IsAny<string>()))
            .Returns(_mockRuntimeDataStore.Object);

        LambdaRuntimeApi.SetupLambdaRuntimeApiEndpoints(_app);
    }

    [Fact]
    public async Task PostEvent_RequestResponse_Success()
    {
        // Arrange
        var functionName = "testFunction";
        var testEvent = "{\"key\":\"value\"}";
        var response = "{\"result\":\"success\"}";

        var eventContainer = new EventContainer(_mockRuntimeApiDataStore.Object, 1, testEvent, true);
        eventContainer.ReportSuccessResponse(response);

        _mockRuntimeDataStore
            .Setup(x => x.QueueEvent(testEvent, true))
            .Returns(eventContainer);

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(testEvent));
        context.Response.Body = new MemoryStream();

        // Act
        await new LambdaRuntimeApi(_app).PostEvent(context, functionName);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.Headers.ContentType);
        Assert.Equal(response.Length, context.Response.ContentLength);

        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(response, responseBody);
    }

    [Fact]
    public async Task PostEvent_Event_Async()
    {
        // Arrange
        var functionName = "testFunction";
        var testEvent = "{\"key\":\"value\"}";

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(testEvent));
        context.Request.Headers["X-Amz-Invocation-Type"] = "Event";
        context.Response.Body = new MemoryStream();

        // Act
        await new LambdaRuntimeApi(_app).PostEvent(context, functionName);

        // Assert
        Assert.Equal(202, context.Response.StatusCode);
    }

    [Fact]
    public async Task PostEvent_RequestResponse_Error()
    {
        // Arrange
        var functionName = "testFunction";
        var testEvent = "{\"key\":\"value\"}";
        var errorType = "Function.Error";
        var errorResponse = "{\"errorMessage\":\"Something went wrong\"}";

        var eventContainer = new EventContainer(_mockRuntimeApiDataStore.Object, 1, testEvent, true);
        eventContainer.ReportErrorResponse(errorType, errorResponse);

        _mockRuntimeDataStore
            .Setup(x => x.QueueEvent(testEvent, true))
            .Returns(eventContainer);

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(testEvent));
        context.Response.Body = new MemoryStream();

        // Act
        await new LambdaRuntimeApi(_app).PostEvent(context, functionName);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(errorType, context.Response.Headers["X-Amz-Function-Error"]);
        Assert.Equal("application/json", context.Response.Headers.ContentType);
        Assert.Equal(errorResponse.Length, context.Response.ContentLength);

        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(errorResponse, responseBody);
    }

    [Fact]
    public async Task PostEvent_RequestResponse_ErrorWithoutBody()
    {
        // Arrange
        var functionName = "testFunction";
        var testEvent = "{\"key\":\"value\"}";
        var errorType = "Function.Error";
        string? errorResponse = null;

        var eventContainer = new EventContainer(_mockRuntimeApiDataStore.Object, 1, testEvent, true);
        eventContainer.ReportErrorResponse(errorType, errorResponse);

        _mockRuntimeDataStore
            .Setup(x => x.QueueEvent(testEvent, true))
            .Returns(eventContainer);

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(testEvent));
        context.Response.Body = new MemoryStream();

        // Act
        await new LambdaRuntimeApi(_app).PostEvent(context, functionName);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(errorType, context.Response.Headers["X-Amz-Function-Error"]);
        Assert.Equal(0, context.Response.Headers.ContentType.Count);
        Assert.Null(context.Response.ContentLength);

        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Empty(responseBody);
    }

    [Fact]
    public async Task GetNextInvocation_Returns_Event()
    {
        // Arrange
        var functionName = "testFunction";
        var testEvent = "{\"key\":\"value\"}";
        var eventContainer = new EventContainer(_mockRuntimeApiDataStore.Object, 1, testEvent, true);

        _mockRuntimeDataStore
            .Setup(x => x.TryActivateEvent(out eventContainer))
            .Returns(true);

        var context = new DefaultHttpContext();
        var memoryStream = new NonClosingMemoryStream(); // Using custom non closing memory stream because if we use regular memory stream its closed after GetBextInvocation and we can't read it.
        context.Response.Body = memoryStream;

        // Act
        await new LambdaRuntimeApi(_app).GetNextInvocation(context, functionName);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.True(context.Response.Headers.ContainsKey("Lambda-Runtime-Aws-Request-Id"));
        Assert.True(context.Response.Headers.ContainsKey("Lambda-Runtime-Trace-Id"));
        Assert.True(context.Response.Headers.ContainsKey("Lambda-Runtime-Invoked-Function-Arn"));
        Assert.Equal("application/json", context.Response.Headers["Content-Type"]);

        memoryStream.Position = 0;
        using (var reader = new StreamReader(memoryStream, leaveOpen: true))
        {
            var responseBody = await reader.ReadToEndAsync();
            Assert.Equal(testEvent, responseBody);
        }
    }

    [Fact]
    public void PostInitError_Logs_Error()
    {
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);
            // Arrange
            var functionName = "testFunction";
            var errorType = "InitializationError";
            var error = "Failed to initialize";

            // Act
            var result = new LambdaRuntimeApi(_app).PostInitError(functionName, errorType, error);

            // Assert
            Assert.NotNull(result);
            var statusResponse = Assert.IsType<StatusResponse>((result as IValueHttpResult)?.Value);
            Assert.Equal("success", statusResponse.Status);
        }
        finally
        {
            Console.SetError(consoleError);
        }
    }

    [Fact]
    public async Task PostInvocationResponse_Reports_Success()
    {
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);
            // Arrange
            var functionName = "testFunction";
            var awsRequestId = "request123";
            var response = "{\"result\":\"success\"}";

            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(response));
            context.Response.Body = new MemoryStream();

            _mockRuntimeDataStore
                .Setup(x => x.ReportSuccess(awsRequestId, response));

            // Act
            var result = await new LambdaRuntimeApi(_app).PostInvocationResponse(context, functionName, awsRequestId);

            // Assert
            Assert.NotNull(result);
            var statusResponse = Assert.IsType<StatusResponse>((result as IValueHttpResult)?.Value);
            Assert.Equal("success", statusResponse.Status);

            _mockRuntimeDataStore.Verify(x => x.ReportSuccess(awsRequestId, response), Times.Once);
        }
        finally
        {
            Console.SetError(consoleError);
        }
    }

    [Fact]
    public async Task PostError_Reports_Error()
    {
        var consoleError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);
            // Arrange
            var functionName = "testFunction";
            var awsRequestId = "request123";
            var errorType = "HandlerError";
            var errorBody = "Function execution failed";

            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(errorBody));
            context.Response.Body = new MemoryStream();

            _mockRuntimeDataStore
                .Setup(x => x.ReportError(awsRequestId, errorType, errorBody));

            // Act
            var result = await new LambdaRuntimeApi(_app).PostError(context, functionName, awsRequestId, errorType);

            // Assert
            Assert.NotNull(result);
            var statusResponse = Assert.IsType<StatusResponse>((result as IValueHttpResult)?.Value);
            Assert.Equal("success", statusResponse.Status);

            _mockRuntimeDataStore.Verify(x => x.ReportError(awsRequestId, errorType, errorBody), Times.Once);
        }
        finally
        {
            Console.SetError(consoleError);
        }
    }

    [Fact]
    public async Task PostEvent_RequestTooLarge_Returns413()
    {
        // Arrange
        var functionName = "testFunction";
        // Create a large payload that exceeds 6MB
        var largePayload = new string('x', 6 * 1024 * 1024 + 1);

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(largePayload));
        context.Response.Body = new MemoryStream();

        // Act
        await new LambdaRuntimeApi(_app).PostEvent(context, functionName);

        // Assert
        Assert.Equal(413, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.Headers.ContentType);
        Assert.Equal("RequestEntityTooLargeException", context.Response.Headers["X-Amzn-Errortype"]);

        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Request must be smaller than", responseBody);
        Assert.Contains("bytes for the InvokeFunction operation", responseBody);
    }

    [Fact]
    public async Task PostInvocationResponse_ResponseTooLarge_ReportsError()
    {
        var consoleOut = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            // Arrange
            var functionName = "testFunction";
            var awsRequestId = "request123";
            // Create a large response that exceeds 6MB
            var largeResponse = new string('x', 6 * 1024 * 1024 + 1);

            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(largeResponse));
            context.Response.Body = new MemoryStream();

            _mockRuntimeDataStore
                .Setup(x => x.ReportError(
                    awsRequestId,
                    "ResponseSizeTooLarge",
                    It.Is<string>(s => s.Contains("Response payload size exceeded maximum allowed payload size"))))
                .Verifiable();

            // Act
            var result = await new LambdaRuntimeApi(_app).PostInvocationResponse(context, functionName, awsRequestId);

            // Assert
            Assert.NotNull(result);
            var statusResponse = Assert.IsType<StatusResponse>((result as IValueHttpResult)?.Value);
            Assert.Equal("success", statusResponse.Status);

            _mockRuntimeDataStore.Verify(
                x => x.ReportError(
                    awsRequestId,
                    "ResponseSizeTooLarge",
                    It.Is<string>(s => s.Contains("Response payload size exceeded maximum allowed payload size"))),
                Times.Once);
        }
        finally
        {
            Console.SetOut(consoleOut);
        }
    }
}

// Helper class to prevent stream from being closed
public class NonClosingMemoryStream : MemoryStream
{
    public override void Close() { }
    protected override void Dispose(bool disposing) { }
}

