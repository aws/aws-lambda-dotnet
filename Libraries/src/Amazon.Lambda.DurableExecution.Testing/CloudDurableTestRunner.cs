// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Services;
using Amazon.Lambda.Model;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Cloud test runner that invokes a real deployed durable Lambda function
/// and polls for results. Provides the same <see cref="IDurableTestRunner{TInput, TOutput}"/>
/// interface as the local runner for portable test code.
/// </summary>
public sealed class CloudDurableTestRunner<TInput, TOutput> : IDurableTestRunner<TInput, TOutput>, IAsyncDisposable
{
    private readonly string _functionArn;
    private readonly IAmazonLambda _lambdaClient;
    private readonly ILambdaSerializer _serializer;
    private readonly CloudTestRunnerOptions _options;

    /// <summary>
    /// Creates a cloud test runner targeting a deployed durable function.
    /// </summary>
    /// <param name="functionArn">Qualified function ARN (with alias, version, or $LATEST).</param>
    /// <param name="lambdaClient">AWS Lambda client. If null, creates a default client.</param>
    /// <param name="options">Cloud runner options. If null, uses defaults.</param>
    public CloudDurableTestRunner(
        string functionArn,
        IAmazonLambda? lambdaClient = null,
        CloudTestRunnerOptions? options = null)
    {
        _functionArn = functionArn ?? throw new ArgumentNullException(nameof(functionArn));
        _lambdaClient = lambdaClient ?? new AmazonLambdaClient();
        _options = options ?? new CloudTestRunnerOptions();
        _serializer = _options.Serializer ?? new DefaultLambdaJsonSerializer();
    }

    /// <inheritdoc />
    public async Task<TestResult<TOutput>> RunAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var arn = await StartAsync(input, timeout, cancellationToken);
        return await WaitForResultAsync(arn, timeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> StartAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var payload = SerializeToString(input);

        var response = await _lambdaClient.InvokeAsync(new InvokeRequest
        {
            FunctionName = _functionArn,
            Payload = payload,
        }, cancellationToken);

        var arn = ExtractDurableExecutionArn(response);
        if (arn is null)
        {
            throw new CloudTestException(
                "Lambda response did not include a DurableExecutionArn. " +
                "Verify the function is configured with [DurableExecution].");
        }

        return arn;
    }

    /// <inheritdoc />
    public async Task<TestResult<TOutput>> WaitForResultAsync(
        string durableExecutionArn,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? _options.DefaultTimeout);

        var serviceClient = new LambdaDurableServiceClient(_lambdaClient);
        string? marker = "";
        var operations = new Dictionary<string, Operation>();

        while (true)
        {
            timeoutCts.Token.ThrowIfCancellationRequested();

            var (page, nextMarker) = await serviceClient.GetExecutionStateAsync(
                durableExecutionArn, null, marker ?? "", timeoutCts.Token);

            foreach (var op in page)
                operations[op.Id!] = op;

            marker = nextMarker;

            var execOp = operations.Values.FirstOrDefault(o => o.Type == OperationTypes.Execution);
            if (execOp?.Status is OperationStatuses.Succeeded or OperationStatuses.Failed)
            {
                return BuildTestResult(durableExecutionArn, execOp, operations.Values);
            }

            if (string.IsNullOrEmpty(marker))
            {
                await Task.Delay(_options.PollInterval, timeoutCts.Token);
            }
        }
    }

    /// <inheritdoc />
    public async Task<string> WaitForCallbackAsync(
        string durableExecutionArn,
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? _options.DefaultTimeout);

        var serviceClient = new LambdaDurableServiceClient(_lambdaClient);

        while (true)
        {
            timeoutCts.Token.ThrowIfCancellationRequested();

            var (page, _) = await serviceClient.GetExecutionStateAsync(
                durableExecutionArn, null, "", timeoutCts.Token);

            foreach (var op in page)
            {
                if (op.Type == OperationTypes.Callback
                    && op.Status == OperationStatuses.Started
                    && op.CallbackDetails?.CallbackId is { } cbId)
                {
                    if (name is null || MatchesCallbackName(op.Name, name))
                        return cbId;
                }
            }

            await Task.Delay(_options.PollInterval, timeoutCts.Token);
        }
    }

    /// <inheritdoc />
    public async Task SendCallbackSuccessAsync<TResult>(
        string callbackId,
        TResult result,
        CancellationToken cancellationToken = default)
    {
        var serialized = SerializeToString(result);
        await _lambdaClient.SendDurableExecutionCallbackSuccessAsync(
            new SendDurableExecutionCallbackSuccessRequest
            {
                CallbackId = callbackId,
                Result = new MemoryStream(Encoding.UTF8.GetBytes(serialized)),
            }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendCallbackFailureAsync(
        string callbackId,
        ErrorObject? error = null,
        CancellationToken cancellationToken = default)
    {
        await _lambdaClient.SendDurableExecutionCallbackFailureAsync(
            new SendDurableExecutionCallbackFailureRequest
            {
                CallbackId = callbackId,
                Error = error is not null ? new Amazon.Lambda.Model.ErrorObject
                {
                    ErrorType = error.ErrorType,
                    ErrorMessage = error.ErrorMessage,
                } : null,
            }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendCallbackHeartbeatAsync(
        string callbackId,
        CancellationToken cancellationToken = default)
    {
        await _lambdaClient.SendDurableExecutionCallbackHeartbeatAsync(
            new SendDurableExecutionCallbackHeartbeatRequest
            {
                CallbackId = callbackId,
            }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private TestResult<TOutput> BuildTestResult(string arn, Operation execOp, IEnumerable<Operation> allOps)
    {
        var status = execOp.Status switch
        {
            OperationStatuses.Succeeded => InvocationStatus.Succeeded,
            OperationStatuses.Failed => InvocationStatus.Failed,
            _ => InvocationStatus.Pending,
        };

        TOutput? result = default;
        if (status == InvocationStatus.Succeeded && execOp.ExecutionDetails?.InputPayload is { } payload)
        {
            // The execution result is stored differently — check ContextDetails or direct result
            // For cloud runner, the result comes from the execution operation
        }

        var steps = allOps
            .Where(o => o.Type != OperationTypes.Execution)
            .Select(o => new TestStep(o, _serializer))
            .ToList();

        return new TestResult<TOutput>(
            status: status,
            result: result,
            error: execOp.ContextDetails?.Error,
            durableExecutionArn: arn,
            invocationCount: -1,
            steps: steps);
    }

    private static string? ExtractDurableExecutionArn(InvokeResponse response)
    {
        // The durable execution ARN is returned in the response headers/payload.
        // Exact extraction depends on the SDK version; try known locations.
        if (response.ResponseMetadata?.Metadata is { } metadata
            && metadata.TryGetValue("x-amz-durable-execution-arn", out var arnFromHeader))
        {
            return arnFromHeader;
        }

        // Fallback: parse from the payload if the service embeds it there
        if (response.Payload?.Length > 0)
        {
            try
            {
                response.Payload.Position = 0;
                using var reader = new System.IO.StreamReader(response.Payload, Encoding.UTF8, leaveOpen: true);
                var body = reader.ReadToEnd();
                if (body.Contains("DurableExecutionArn"))
                {
                    var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("DurableExecutionArn", out var arnProp))
                        return arnProp.GetString();
                }
            }
            catch
            {
                // Parsing failed — fall through to null
            }
        }

        return null;
    }

    private static bool MatchesCallbackName(string? opName, string name)
    {
        if (opName is null) return false;
        if (string.Equals(opName, name, StringComparison.Ordinal)) return true;
        if (string.Equals(opName, $"{name}-callback", StringComparison.Ordinal)) return true;
        return false;
    }

    private string SerializeToString<T>(T value)
    {
        using var stream = new MemoryStream();
        _serializer.Serialize(value, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
