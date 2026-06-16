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
            var result = await entry.InvokeDelegate(serializedPayload, serializer, lambdaContext);
            return (result, null);
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

    private static async Task<string?> InvokePlain<TPayload, TResult>(
        Func<TPayload, ILambdaContext, Task<TResult>> handler,
        string serializedPayload,
        ILambdaSerializer serializer,
        ILambdaContext lambdaContext)
    {
        var payload = Deserialize<TPayload>(serializedPayload, serializer);
        var result = await handler(payload, lambdaContext);
        return Serialize(result, serializer);
    }

    private static async Task<string?> InvokeDurable<TPayload, TResult>(
        Func<TPayload, IDurableContext, Task<TResult>> handler,
        string serializedPayload,
        ILambdaSerializer serializer)
    {
        // For durable siblings, we'd need to spin up a nested DurableTestRunner.
        // This is wired in commit 4 when the full runner exists. For now, the
        // registry captures the handler; actual durable invocation is deferred.
        throw new NotImplementedException(
            "Durable sibling invocation requires the full DurableTestRunner, wired in a subsequent commit.");
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
        Func<string, ILambdaSerializer, ILambdaContext, Task<string?>> InvokeDelegate);
}
