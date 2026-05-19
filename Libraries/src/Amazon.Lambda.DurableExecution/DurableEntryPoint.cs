using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.DurableExecution.Services;
using Amazon.Lambda.Model;
using Amazon.Runtime;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// AOT-friendly entry point for a durable workflow. Owns (de)serialization of
/// the wire envelope so users only register their own POCO types in their
/// <c>JsonSerializerContext</c> — the library's <see cref="DurableEnvelopeJsonContext"/>
/// handles envelope JSON, the user's <see cref="ILambdaSerializer"/> (read from
/// <see cref="ILambdaContext.Serializer"/>) handles only <typeparamref name="TInput"/>
/// and <typeparamref name="TOutput"/>.
/// </summary>
/// <typeparam name="TInput">The workflow's input payload type.</typeparam>
/// <typeparam name="TOutput">The workflow's return type.</typeparam>
/// <example>
/// <code>
/// private static readonly DurableEntryPoint&lt;OrderEvent, OrderResult&gt; _entry = new(MyWorkflow);
///
/// static async Task Main()
/// {
///     await LambdaBootstrapBuilder
///         .Create(_entry.InvokeAsync, new SourceGeneratorLambdaJsonSerializer&lt;MyJsonContext&gt;())
///         .Build()
///         .RunAsync();
/// }
/// </code>
/// </example>
public sealed class DurableEntryPoint<TInput, TOutput>
{
    private static readonly Lazy<IAmazonLambda> _cachedLambdaClient =
        new(() => new AmazonLambdaClient(), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Func<TInput, IDurableContext, Task<TOutput>> _workflow;
    private readonly IAmazonLambda _lambdaClient;

    /// <summary>
    /// Creates an entry point that uses a default <see cref="AmazonLambdaClient"/>,
    /// constructed lazily and cached process-wide.
    /// </summary>
    public DurableEntryPoint(Func<TInput, IDurableContext, Task<TOutput>> workflow)
        : this(workflow, _cachedLambdaClient.Value)
    {
    }

    /// <summary>
    /// Creates an entry point that uses the supplied <see cref="IAmazonLambda"/> client
    /// for checkpoint and state-fetch calls.
    /// </summary>
    public DurableEntryPoint(Func<TInput, IDurableContext, Task<TOutput>> workflow, IAmazonLambda lambdaClient)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _lambdaClient = lambdaClient ?? throw new ArgumentNullException(nameof(lambdaClient));
    }

    /// <summary>
    /// Lambda handler entry point. Register this method with <c>LambdaBootstrapBuilder</c>
    /// alongside an <see cref="ILambdaSerializer"/> that knows how to (de)serialize
    /// <typeparamref name="TInput"/> / <typeparamref name="TOutput"/>.
    /// </summary>
    public async Task<Stream> InvokeAsync(Stream input, ILambdaContext context)
    {
        var output = await DurableEntryPointCore.InvokeAsync(_workflow, input, context, _lambdaClient);
        var ms = new MemoryStream();
        JsonSerializer.Serialize(ms, output, DurableEnvelopeJsonContext.Default.DurableExecutionInvocationOutput);
        ms.Position = 0;
        return ms;
    }
}

/// <summary>
/// AOT-friendly entry point for a void durable workflow.
/// See <see cref="DurableEntryPoint{TInput,TOutput}"/> for details.
/// </summary>
public sealed class DurableEntryPoint<TInput>
{
    private readonly DurableEntryPoint<TInput, object?> _inner;

    /// <summary>
    /// Creates an entry point that uses a default <see cref="AmazonLambdaClient"/>,
    /// constructed lazily and cached process-wide.
    /// </summary>
    public DurableEntryPoint(Func<TInput, IDurableContext, Task> workflow)
    {
        if (workflow == null) throw new ArgumentNullException(nameof(workflow));
        _inner = new DurableEntryPoint<TInput, object?>(async (i, c) => { await workflow(i, c); return null; });
    }

    /// <summary>
    /// Creates an entry point that uses the supplied <see cref="IAmazonLambda"/> client
    /// for checkpoint and state-fetch calls.
    /// </summary>
    public DurableEntryPoint(Func<TInput, IDurableContext, Task> workflow, IAmazonLambda lambdaClient)
    {
        if (workflow == null) throw new ArgumentNullException(nameof(workflow));
        _inner = new DurableEntryPoint<TInput, object?>(
            async (i, c) => { await workflow(i, c); return null; },
            lambdaClient);
    }

    /// <inheritdoc cref="DurableEntryPoint{TInput,TOutput}.InvokeAsync"/>
    public Task<Stream> InvokeAsync(Stream input, ILambdaContext context)
        => _inner.InvokeAsync(input, context);
}
