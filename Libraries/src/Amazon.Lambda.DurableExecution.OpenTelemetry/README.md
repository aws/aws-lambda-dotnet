# Amazon.Lambda.DurableExecution.OpenTelemetry

OpenTelemetry tracing for [Amazon.Lambda.DurableExecution](../Amazon.Lambda.DurableExecution/README.md). It is a plugin for the SDK's [instrumentation plugin API](../Amazon.Lambda.DurableExecution/docs/core/instrumentation-plugins.md).

**Preview.** Only the Invocation view is available. `OtelView.Execution` throws `NotSupportedException`.

The plugin emits spans through `System.Diagnostics.ActivitySource`, so the package has no OpenTelemetry NuGet dependency. The OpenTelemetry SDK, the ADOT layer and `OpenTelemetry.Instrumentation.AWSLambda` listen to it once you add the source name.

## Quick start

```csharp
using Amazon.Lambda.DurableExecution.OpenTelemetry;

static readonly TracerProvider Tracer = Sdk.CreateTracerProviderBuilder()
    .AddSource(OtelPlugin.ActivitySourceName)      // "Amazon.Lambda.DurableExecution"
    .AddOtlpExporter()
    .Build();

static readonly DurableExecutionOptions Options = new DurableExecutionOptions()
    .AddOpenTelemetry(OtelView.Invocation, new OtelPluginOptions
    {
        // The plugin cannot flush by itself. Export before Lambda freezes the environment.
        FlushAsync = (timeout, _) => { Tracer.ForceFlush((int)timeout.TotalMilliseconds); return Task.CompletedTask; },
    });

public Task<DurableExecutionInvocationOutput> Handler(DurableExecutionInvocationInput input, ILambdaContext context)
    => DurableFunction.WrapAsync<Order, OrderResult>(Workflow, input, context, Options);
```

Create the options once and reuse them. Register exactly one OpenTelemetry view: two factories fail with `PluginLoadException` before the workflow runs.

With the ADOT Lambda layer, activate it with `AWS_LAMBDA_EXEC_WRAPPER=/opt/otel-instrument` and omit your own provider; the layer listens for the source.

## Trace structure

The whole execution is one trace, whichever invocation a span comes from.

```text
Execution ancestor (propagated remote parent, or a synthetic root derived from the execution ARN)
+-- Workflow            exported once, by the invocation that finishes the execution
+-- Invocation          one per Lambda invocation
    +-- operation       links Workflow
        +-- attempt     links Workflow
```

- **The ancestor** is the complete `Root` and `Parent` of the X-Ray header the service propagates (`ILambdaContext.TraceId`). Without a parent the plugin exports a synthetic `DurableExecutionRoot` span whose id comes from the execution ARN, once per invocation with the same id. Consumers deduplicate on span id. An ambient span of the same trace (the ADOT handler span) is used as the Invocation's parent instead.
- **Derived ids.** The Workflow span, the synthetic root and the first segment of every operation have ids derived from the execution ARN, so a span exported by one invocation can name one exported by another. They match the JavaScript, Python and Java SDKs.
- **Replay.** A step or child context reached again after it finished is exported as a new span that links the Workflow span and the operation's first segment. A wait, invoke or callback that finished while the execution was suspended is exported once, in the invocation that sees it. A wait that is only replayed is not exported again.
- **Open operations.** An operation still running when the invocation suspends is ended with `durable.operation.status=STARTED`.
- **Timing.** Spans use the invocation's own clock: the wall clock read at the start, advanced by the monotonic clock.

### Attributes and status

All spans are `SpanKind.Internal`.

| Span | Attributes |
|---|---|
| Workflow (`WorkflowSpanName`, default `Workflow`) | `durable.execution.arn`, `durable.execution.status` |
| Invocation | `durable.execution.arn`, `durable.invocation.first`, `durable.invocation.status` (`SUCCEEDED`, `FAILED`, `PENDING`, `RETRYING`) |
| Operation (named by the operation name, or its type) | `durable.execution.arn`, `durable.operation.id`, `.type`, `.name`, `.subtype`, `.status`; `durable.attempt.number` on steps and wait-for-condition |
| Attempt (`<name> attempt <n>`) | the operation identity, `durable.attempt.number`, `durable.attempt.outcome` |

Status: Workflow `Ok`/`Error`. Invocation `Ok` for succeeded and pending, `Error` for failed, unset for retrying. An operation is `Error` when it failed, timed out or was stopped, even with no error details, and `Ok` when it succeeded. An `exception` event is added only when error details exist, with a stored error type used as is.

## Sampling

An upstream decision in the header (`Sampled=1` or `0`) is authoritative: spans are created with the matching trace flag, and the standard `ParentBased` sampler follows it. Without one the execution is sampled and your sampler decides for each span.

## Chained invokes

The plugin supplies the `X-Amzn-Trace-Id` the SDK sends with a chained invoke: this execution's trace, with the calling operation's span as the parent and the upstream sampling decision. The invoked function continues the trace.

## Log correlation

`EnrichLogger` (default on) adds `otelTraceId`, `otelSpanId` and `otelTraceSampled` to the logging scope of durable log entries written while a span is current. The names avoid the `traceId` field the Lambda runtime already writes in JSON logs.

## Options

| Option | Default | |
|---|---|---|
| `InstrumentationName` | `Amazon.Lambda.DurableExecution` | The `ActivitySource` name. |
| `WorkflowSpanName` | `Workflow` | |
| `EnrichLogger` | `true` | |
| `FlushAsync` | none | Called at the end of every invocation, after its spans ended. |
| `FlushTimeout` | 1 second | Passed to `FlushAsync`; its token is cancelled after this time or when the invocation's time runs out. |
| `ContextExtractor` | `ContextExtractors.XRay` | Finds the execution's trace context. It must return the execution's own context: one that changes between invocations splits the execution across traces. |

## Span ids and `Activity`

`Activity` has no public way to choose its own span id. The plugin sets the private `_spanId` and `_id` fields through `[UnsafeAccessor]`, only for the spans whose ids other invocations must reproduce. It is generated at compile time and works under trimming and Native AOT. The fields are not a contract: if a runtime changes them the override switches itself off and spans get random ids. Parenting inside one invocation still works; references across invocations (the Workflow span, the link from a replayed step to its first segment) then dangle. A test fails when the fields are missing. Listeners notified when a span starts see the random id; export and child spans see the derived one.

## Not yet available

- The Execution view.
- Loading the plugin from an environment variable or a Lambda layer without code.
- The W3C `traceparent` extractor.
- `[DurableExecution(Tracing = ...)]` for Amazon.Lambda.Annotations.
