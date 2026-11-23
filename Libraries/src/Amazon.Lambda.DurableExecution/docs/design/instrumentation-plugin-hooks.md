# Design note: instrumentation plugin hooks

Status: accepted. User guide: [instrumentation-plugins.md](../core/instrumentation-plugins.md).

## Context

The Python, JavaScript and Java durable execution SDKs added OpenTelemetry support as a plugin built on a generic instrumentation API in the core SDK. This SDK follows the same split so the core knows nothing about tracing, and tracing (and anything else) can be added as a plugin.

## Decisions

1. **A factory creates one plugin per invocation.** Plugin state is per invocation, so concurrent invocations on Lambda Managed Instances cannot overwrite each other. Whatever outlives an invocation lives on the factory.
2. **Operation and attempt hooks are synchronous and never awaited.** The SDK decides to suspend an invocation from the state of its operations, and it cannot see work a plugin has started. Awaiting a hook on a path where no operation is executing lets the invocation suspend while the plugin is still working, so the code after that point does not run. The JS SDK hit this and documents it in its ADR 0001. Work that must finish before the response belongs in `OnInvocationEndAsync`, which is awaited once within the remaining invocation time.
3. **Context is applied by `WrapAsync`, not by start and end hooks.** `Activity.Current` is an `AsyncLocal`, so a value set in a hook does not reach the caller. A hook that wraps the user's `Func<Task>` is the only way to set it for the code that follows. The wrap takes a non-generic `Func<Task>` and the call site captures its own result, which avoids a generic virtual method on the interface.
4. **The SDK defends the workflow from plugins.** Every hook exception is contained. `WrapAsync` always runs the user code exactly once and always lets its exception through.
5. **The service's attempt count is normalized.** `StepDetails.Attempt` counts finished attempts, so a finished record already names its last attempt and an unfinished one is on the next. `OperationInfo.Attempt` hides that.
6. **A callback ends when its result is read.** `CreateCallbackAsync` returns a handle immediately, so the operation is not finished when the call returns.
7. **Trace propagation is a hook, not a feature.** The SDK collects a header from plugins and writes it to `ChainedInvokeOptions.XAmznTraceId`. Header format and meaning stay in the plugin. This needs `AWSSDK.Lambda` 4.0.110.1 or later.
8. **No reflection-based discovery.** Plugins are registered in code. The library is trimmable and AOT-safe, and loading plugins by name from an environment variable would need reflection.

## Consequences

- A plugin cannot rely on its asynchronous work finishing before the next operation runs.
- With no plugin registered the SDK takes its previous code path.
- An invocation whose end hook is cut off at the deadline may lose its last flush.
- The hook set is public API. A hook can be added later without breaking plugins (default interface methods); changing or removing one is a breaking change.
