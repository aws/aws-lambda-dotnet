// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Tracks registered sibling function handlers for use by the local test runner
/// when a workflow calls InvokeAsync.
/// </summary>
internal sealed class FunctionRegistry
{
    private readonly List<FunctionEntry> _entries = new();
    private readonly TestRunnerOptions _options;

    public FunctionRegistry(TestRunnerOptions? options = null)
    {
        _options = options ?? new TestRunnerOptions();
    }

    public void RegisterPlain<TPayload, TResult>(
        string functionNameOrArn,
        Func<TPayload, ILambdaContext, Task<TResult>> handler)
    {
        _entries.Add(new FunctionEntry(
            ExtractName(functionNameOrArn),
            functionNameOrArn,
            IsDurable: false,
            InvokeDelegate: (payload, serializer, lambdaContext) =>
                InvokePlain(handler, payload, serializer, lambdaContext)));
    }

    public void RegisterDurable<TPayload, TResult>(
        string functionNameOrArn,
        Func<TPayload, IDurableContext, Task<TResult>> handler)
    {
        _entries.Add(new FunctionEntry(
            ExtractName(functionNameOrArn),
            functionNameOrArn,
            IsDurable: true,
            InvokeDelegate: (payload, serializer, lambdaContext) =>
                InvokeDurable(handler, payload, serializer)));
    }

    public async Task<(string? Result, ErrorObject? Error)> InvokeAsync(
        string functionNameOrArn,
        string serializedPayload,
        ILambdaSerializer serializer,
        ILambdaContext lambdaContext)
    {
        var entry = Lookup(functionNameOrArn)
            ?? throw new UnregisteredSiblingFunctionException(functionNameOrArn);

        try
        {
            return await entry.InvokeDelegate(serializedPayload, serializer, lambdaContext);
        }
        catch (Exception ex)
        {
            return (null, ErrorObject.FromException(ex));
        }
    }

    public bool IsRegistered(string functionNameOrArn) => Lookup(functionNameOrArn) is not null;

    private FunctionEntry? Lookup(string functionNameOrArn)
    {
        foreach (var entry in _entries)
        {
            if (string.Equals(entry.OriginalName, functionNameOrArn, StringComparison.Ordinal))
                return entry;
        }

        var extractedName = ExtractName(functionNameOrArn);
        foreach (var entry in _entries)
        {
            if (string.Equals(entry.ShortName, extractedName, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    private static string ExtractName(string functionNameOrArn)
    {
        if (!functionNameOrArn.StartsWith("arn:", StringComparison.OrdinalIgnoreCase))
            return functionNameOrArn;

        var parts = functionNameOrArn.Split(':');
        // ARN format: arn:aws:lambda:region:account:function:name[:qualifier]
        if (parts.Length >= 7)
            return parts[6];

        return functionNameOrArn;
    }

    private static async Task<(string? Result, ErrorObject? Error)> InvokePlain<TPayload, TResult>(
        Func<TPayload, ILambdaContext, Task<TResult>> handler,
        string serializedPayload,
        ILambdaSerializer serializer,
        ILambdaContext lambdaContext)
    {
        var payload = Deserialize<TPayload>(serializedPayload, serializer);
        var result = await handler(payload, lambdaContext);
        return (Serialize(result, serializer), null);
    }

    private async Task<(string? Result, ErrorObject? Error)> InvokeDurable<TPayload, TResult>(
        Func<TPayload, IDurableContext, Task<TResult>> handler,
        string serializedPayload,
        ILambdaSerializer serializer)
    {
        // A durable sibling is itself a workflow, so drive it to completion in a
        // nested runner that shares this runner's options (time-skipping,
        // serializer, registered siblings) but gets its own isolated store/ARN.
        var payload = Deserialize<TPayload>(serializedPayload, serializer);

        var nestedOptions = _options with
        {
            DurableExecutionArn = _options.DurableExecutionArn + ":nested:" + Guid.NewGuid().ToString("N"),
        };
        var nested = new DurableTestRunner<TPayload, TResult>(handler, nestedOptions, registry: this);

        var result = await nested.RunAsync(payload);
        if (result.Status == InvocationStatus.Succeeded)
            return (Serialize(result.Result, serializer), null);

        return (null, result.Error ?? new ErrorObject
        {
            ErrorType = typeof(InvokeException).FullName,
            ErrorMessage = $"Durable sibling '{typeof(TPayload).Name}->{typeof(TResult).Name}' did not succeed (status: {result.Status}).",
        });
    }

    private static T Deserialize<T>(string serialized, ILambdaSerializer serializer)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized));
        return serializer.Deserialize<T>(stream);
    }

    private static string? Serialize<T>(T value, ILambdaSerializer serializer)
    {
        if (value is null) return null;
        using var stream = new MemoryStream();
        serializer.Serialize(value, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed record FunctionEntry(
        string ShortName,
        string OriginalName,
        bool IsDurable,
        Func<string, ILambdaSerializer, ILambdaContext, Task<(string? Result, ErrorObject? Error)>> InvokeDelegate);
}
