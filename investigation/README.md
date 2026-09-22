# Issue #2350 — `LambdaLogger.ConfigureStructuredLogging` is a no-op on the managed runtime (class library model)

Issue: https://github.com/aws/aws-lambda-dotnet/issues/2350

## Summary

The last comment on the issue (from `madmox`) reports that `LambdaLogger.ConfigureStructuredLogging`,
though listed as "deployed to the managed runtime" in the 2026-07-29 release notes, has no effect for
**class library** Lambda functions on the managed `dotnet10` runtime. A destructured `{@ScheduledDate}`
(`NodaTime.Instant`) still renders as `"ScheduledDate":{}` — the custom `JsonSerializerOptions` converters
never reach the formatter. The same call works when using `Amazon.Lambda.RuntimeSupport` directly (the
executable / custom runtime model).

The reporter's read is correct: the callback that configures the formatter is not wired up correctly when
`Amazon.Lambda.RuntimeSupport` runs as the executable host for a class-library function on the managed runtime.

## Root cause

`ConfigureStructuredLogging` was added in PR #2383. The wiring lives in
`JsonLogMessageFormatter`'s constructor:

```csharp
ConfigureJsonLogMessageFormatterIsolated.ConfigureCallbackInCore(ConfigureStructuredLogging);
```

and `ConfigureCallbackInCore` used a **compile-time** reference:

```csharp
Amazon.Lambda.Core.LambdaLogger.SetConfigureStructuredLoggingAction(coreOptions => { ... });
```

`Amazon.Lambda.Core.LambdaLogger` exposes a private static field / internal setter that the runtime replaces so
that `ConfigureStructuredLogging` reaches the active `JsonLogMessageFormatter`.

There are two very different ways `Amazon.Lambda.RuntimeSupport` obtains its `Amazon.Lambda.Core` reference:

* **Executable / custom runtime model** — the customer references both `Amazon.Lambda.Core` and
  `Amazon.Lambda.RuntimeSupport` from the same deployment bundle. The compile-time reference resolves to the
  *same* `Amazon.Lambda.Core` assembly the customer code uses, so `SetConfigureStructuredLoggingAction` lands on
  the correct `LambdaLogger`. **This is why the reporter's control experiment against the 2.2.0 NuGet package
  works.**

* **Class library model on the managed runtime** — `Amazon.Lambda.RuntimeSupport` is baked into the managed
  runtime and compiled against **its own** bundled `Amazon.Lambda.Core`. The customer's `Amazon.Lambda.Core`
  (e.g. 3.3.0 from the deployment bundle) is loaded *separately* by
  `Amazon.Lambda.RuntimeSupport.Bootstrap.UserCodeLoader`. These are **two distinct assembly instances** with two
  distinct `LambdaLogger` types and two distinct static callback fields.

  In this model the compile-time `SetConfigureStructuredLoggingAction` call registers the formatter callback on
  the **RuntimeSupport-bundled** `LambdaLogger`, while the customer's code calls `ConfigureStructuredLogging` on
  the **customer's** `LambdaLogger`. The two never meet, so the customer's `JsonSerializerOptions` are silently
  dropped and the formatter keeps its default options — exactly the observed `"ScheduledDate":{}` behavior.

`UserCodeLoader` already knows about this two-assembly problem for plain logging: it redirects the customer's
`LambdaLogger._loggingAction` (and the level variants) via **reflection** against the loaded customer assembly
(`SetCustomerLoggerLogAction`). The structured-logging callback added in #2383 was simply never given the same
reflection-based treatment, so it worked only in the executable model.

### Evidence

`investigation/repro/TwoAssemblyRepro.cs` loads a second copy of `Amazon.Lambda.Core` into a separate
`AssemblyLoadContext` and prints:

```
Same assembly instance? False
```

confirming the customer's `Amazon.Lambda.Core` is a different assembly than the compile-time reference used by
the original wiring.

## Fix

Give the structured-logging callback the same reflection-based wiring that plain logging already uses.

1. `Helpers/Logging/ConfigureJsonLogMessageFormatterIsolated.cs`
   - Added an overload `ConfigureCallbackInCore(Assembly coreAssembly, Action<StructuredLoggingOptions> callback)`
     that registers the callback against a **specific** (reflection-loaded) `Amazon.Lambda.Core` assembly. It
     reflectively finds `LambdaLogger.SetConfigureStructuredLoggingAction`, builds an
     `Action<customerStructuredLoggingOptions>` bound to an adapter that reads `OverrideSerializerOptions` off the
     customer options instance, and invokes the setter. It degrades gracefully (logs debug, no throw) when the
     customer's `Amazon.Lambda.Core` predates the API.

2. `Helpers/Logging/JsonLogMessageFormatter.cs`
   - Tracks constructed formatter instances in a static list and adds
     `WireStructuredLoggingCallbacksToCustomerCore(Assembly customerCoreAssembly)`, which registers every
     formatter's callback against the customer's `Amazon.Lambda.Core`. Instance tracking makes the fix robust to
     construction ordering (the formatter may be created before or after the customer's `Amazon.Lambda.Core` loads).

3. `Bootstrap/UserCodeLoader.cs`
   - In the existing `AssemblyLoad` handler that already redirects logging when the customer's
     `Amazon.Lambda.Core` loads, also call `WireStructuredLoggingCallbacksToCustomerCore(...)`, wrapped in a
     defensive try/catch.

The executable-model path (compile-time `ConfigureCallbackInCore`) is unchanged, so that scenario keeps working.

## Verification

- `Amazon.Lambda.RuntimeSupport` builds clean for `net8.0` (`TreatWarningsAsErrors` is on) — 0 warnings, 0 errors.
- New regression tests in
  `Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/Amazon.Lambda.RuntimeSupport.UnitTests/StructuredLoggingCustomerCoreTests.cs`:
  - `ReflectionWiring_DeliversOverrideSerializerOptions_ToFormatter` — proves the reflection path delivers the
    customer's `JsonSerializerOptions` (camelCase) to the formatter.
  - `ReflectionWiring_AssemblyWithoutStructuredLoggingTypes_DoesNotThrow` — resilience for older `Amazon.Lambda.Core`.
  - `ReflectionWiring_NullAssembly_Throws` — argument validation.
- Full logging/formatter test set passes: 46 passed, 0 failed (`net8.0`).
- `investigation/repro/TwoAssemblyRepro.cs` demonstrates the two-assembly divergence that is the root cause.

## Notes / follow-ups

- The real end-to-end confirmation requires a managed-runtime deployment picking up the updated
  `Amazon.Lambda.RuntimeSupport`; that is outside a local build. The unit tests model the two-assembly wiring
  the managed runtime exercises.
- The reflection path is only reached in the class library model, which already does not support trimming/AOT
  (`UserCodeLoader` is annotated `RequiresUnreferencedCode`); the new API carries the same annotation.
