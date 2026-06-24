# Implementation Plan: Integrating `[DurableExecution]` with the Amazon.Lambda.Annotations Source Generator

> Status: **Ready with must-fixes.** This plan folds in every adversarial-reviewer blocker. Items that depend on undefined infrastructure (the runtime string, the IAM-shape decision) are flagged inline and gated behind explicit pre-merge confirmations rather than buried.

> **Note on history.** An early draft of this plan gated `[DurableExecution]` to the *executable* programming model only, behind an `OutputKind`/`isExecutable` check and a `DurableExecutionRequiresExecutable` diagnostic (`AWSLambda0140`). That gate was **dropped** before merge: the shipped implementation supports **both** the executable and class-library programming models on the managed runtime, with no `OutputKind`/executable check and no executable-only diagnostic. `AWSLambda0140` and `AWSLambda0141` were never allocated, so the durable descriptors are `AWSLambda0142`–`AWSLambda0144`. This document has been updated to describe the merged behavior; see `LambdaFunctionValidator.ValidateDurableExecution` for the source of truth.

## Verified ground truth

All load-bearing claims confirmed against the codebase:

- IAM action names `lambda:CheckpointDurableExecution` and `lambda:GetDurableExecutionState` verified in the reference template (lines 52-53). Note the reference uses an inline `PolicyName: DurableExecutionPolicy` role-attached policy, not a SAM `Policies` array entry — relevant to the IAM section.
- Durable functions support **both** the executable and class-library programming models on the managed runtime; the generator does not gate on `OutputKind`/`isExecutable`. The same wrapper is emitted for both — only the deployed `Handler` string differs (assembly name vs. `Assembly::Type::Method`), which `LambdaFunctionModel.Handler` already derives from `IsExecutable`.
- Durable functions run on **either `dotnet8` or `dotnet10`** (user-confirmed 2026-06-08) — the generator does not force a runtime. The README confirms the `HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>` typed contract.
- Package multi-targets net8.0 + net10.0.

---

## 1. Goal & Scope

### Goal
Let a developer annotate a method with `[DurableExecution]` (alongside `[LambdaFunction]`) and have the Amazon.Lambda.Annotations source generator emit:
1. A **typed-envelope handler wrapper** that delegates to `Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync`.
2. A `serverless.template` resource carrying durable-specific config (`DurableConfig`) and the IAM permissions the function needs to call the checkpoint APIs.

### In scope
- New public attribute `Amazon.Lambda.Annotations.DurableExecutionAttribute` (in the Annotations package).
- Source-generator recognition (TypeFullNames, EventType, builders).
- Generated wrapper shape (typed in/typed out).
- CloudFormation/SAM `DurableConfig` + inline checkpoint IAM policy emission with orphan removal.
- Diagnostics, snapshot tests, change file, docs.

### Out of scope
- Changes to `Amazon.Lambda.DurableExecution` runtime behavior (`DurableFunction`, `DurableContext`, the wire format). These ship independently; this work consumes them.
- Scoped (least-privilege) checkpoint ARNs — deferred until the service publishes a scopable ARN format (see Risks).

### Programming model: both executable and class-library are supported (no gate)
Durable functions work on the managed runtime under **both** programming models, and the generator emits the **same** wrapper for each:
- **Executable model** — the function is an executable assembly hosting its own bootstrap loop; the generated wrapper delegates to `DurableFunction.WrapAsync`, which reads the serializer off the `ILambdaContext` the bootstrap populates.
- **Class-library model** — the managed `dotnet10` runtime hosts its own bootstrap, resolves `[assembly: LambdaSerializer]`, and populates `ILambdaContext.Serializer` the same way, so `WrapAsync` finds the serializer there too.

Only the deployed `Handler` string differs (assembly name vs. `Assembly::Type::Method`), which `LambdaFunctionModel.Handler` already derives from `IsExecutable`. There is therefore **no `OutputKind`/`isExecutable` gate and no executable-only diagnostic** in the shipped code — see `LambdaFunctionValidator.ValidateDurableExecution`.

---

## 2. The `[DurableExecution]` Attribute Design

**Placement (REVISED 2026-06-08): `Amazon.Lambda.Annotations` package, top-level namespace `Amazon.Lambda.Annotations`** — file `Libraries/src/Amazon.Lambda.Annotations/DurableExecutionAttribute.cs`. This matches every other annotation attribute (`LambdaFunctionAttribute`, `ScheduleEventAttribute`, …) and lets the generator use the standard strongly-typed `AttributeModel<DurableExecutionAttribute>` pattern (the generator already references `Amazon.Lambda.Annotations` and reaches its internals via `InternalsVisibleTo`, so it can call `Validate()`/`IsXxxSet` directly). The attribute holds only `int` values, so this adds no dependency from `Amazon.Lambda.Annotations` onto the DurableExecution SDK.

> **Superseded earlier design:** an initial draft placed the attribute in the `Amazon.Lambda.DurableExecution` package. That was wrong — the generator must target `netstandard2.0` and cannot reference that package (net8/net10 + AWSSDK.Lambda), which made the generic `AttributeModel<T>` pattern impossible and forced an awkward string-keyed POCO workaround. Moving the attribute to `Amazon.Lambda.Annotations` removes the problem entirely. The matching style follows `LambdaFunctionAttribute` (block namespace, no nullable), not the file-scoped style of the DurableExecution package.

Implemented shape (matches `LambdaFunctionAttribute`'s block-namespace, non-nullable style):

```csharp
using System;
using System.Collections.Generic;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DurableExecutionAttribute : Attribute
    {
        private int _retentionPeriodInDays;
        public int RetentionPeriodInDays
        {
            get => _retentionPeriodInDays;
            set { _retentionPeriodInDays = value; IsRetentionPeriodInDaysSet = true; }
        }
        internal bool IsRetentionPeriodInDaysSet { get; private set; }

        private int _executionTimeout; // seconds
        public int ExecutionTimeout
        {
            get => _executionTimeout;
            set { _executionTimeout = value; IsExecutionTimeoutSet = true; }
        }
        internal bool IsExecutionTimeoutSet { get; private set; }

        internal List<string> Validate()
        {
            var validationErrors = new List<string>();
            if (IsRetentionPeriodInDaysSet && RetentionPeriodInDays <= 0)
                validationErrors.Add($"{nameof(RetentionPeriodInDays)} = {RetentionPeriodInDays}. It must be a positive integer.");
            if (IsExecutionTimeoutSet && ExecutionTimeout <= 0)
                validationErrors.Add($"{nameof(ExecutionTimeout)} = {ExecutionTimeout}. It must be a positive integer.");
            return validationErrors;
        }
    }
}
```

Design notes:
- **Parameterless** — `[DurableExecution]` with no args is valid (unlike `[SQSEvent]`'s required queue arg).
- **`IsXxxSet` flags are `internal`** (consumed by the generator via `InternalsVisibleTo`), following the `ScheduleEventAttribute` convention so unset values are omitted from CFN.
- **No `WorkflowName`/`Input`/`ResourceName` argument.** Input is carried by the durable envelope (the EXECUTION op — verified in `DurableFunction.ExtractUserPayload`, lines 200-221); the function name derives from `[LambdaFunction]`. A second name source would create a duplicate-key hazard.
- **No signature change** to the user method. The user method stays `(TInput, IDurableContext) -> Task<TOutput>` or `(TInput, IDurableContext) -> Task`, enforced by `DurableExecutionInvalidSignature`.
- Validate rejects `<= 0` now; exact upper bounds are a follow-up once service limits are confirmed.

---

## 3. Source-Generator Recognition (Models, TypeFullNames)

**MUST-FIX (reviewer): exact namespace match or silent skip.** The string below must match the attribute's real namespace exactly, or `EventTypeBuilder`/`AttributeModelBuilder` silently skip it and the method routes to `NoEventMethodBody`. A dedicated test (Component H) covers discovery.

1. **`TypeFullNames.cs`** — add four constants (note the attribute is now in the Annotations namespace; the invocation envelopes + `DurableFunction` remain in the SDK namespace because the **user's** compilation references them and the generator only matches them by string):
   - `DurableExecutionAttribute = "Amazon.Lambda.Annotations.DurableExecutionAttribute"`
   - `DurableExecutionInvocationInput = "Amazon.Lambda.DurableExecution.DurableExecutionInvocationInput"`
   - `DurableExecutionInvocationOutput = "Amazon.Lambda.DurableExecution.DurableExecutionInvocationOutput"`
   - `DurableFunction = "Amazon.Lambda.DurableExecution.DurableFunction"`

2. **`Models/EventType.cs`** — add `DurableExecution` enum member.

3. **`Models/EventTypeBuilder.cs`** — add `else if (attribute.AttributeClass.ToDisplayString() == TypeFullNames.DurableExecutionAttribute) events.Add(EventType.DurableExecution);`.

4. **`Models/Attributes/AttributeModelBuilder.cs`** (IMPLEMENTED) — add an `else if` case (`SymbolEqualityComparer` against `GetTypeByMetadataName(TypeFullNames.DurableExecutionAttribute)`) constructing the standard strongly-typed `AttributeModel<DurableExecutionAttribute>` via `DurableExecutionAttributeBuilder.Build`. Because the attribute now lives in `Amazon.Lambda.Annotations` (which the generator references), this is the same generic pattern every other attribute uses — no workaround needed.

5. **`Models/Attributes/DurableExecutionAttributeBuilder.cs` (NEW, IMPLEMENTED):** returns a real `DurableExecutionAttribute`, reading `att.NamedArguments` by `nameof` (`RetentionPeriodInDays` / `ExecutionTimeout`); assigning each property also flips its `IsXxxSet` flag (so unset values are omitted from the template). Mirrors `ScheduleEventAttributeBuilder` but with no constructor args (the attribute is parameterless).

6. **`Models/GeneratedMethodModelBuilder.cs`** — early branches gated on `Events.Contains(EventType.DurableExecution)`, placed **BEFORE** the API/HttpApi/ALB branches:
   - `BuildParameters` → exactly `[ __request__ : DurableExecutionInvocationInput, __context__ : ILambdaContext ]`
   - `BuildResponseType` → `Task<DurableExecutionInvocationOutput>` (auto-async)
   - `BuildUsings` → conditionally add `Amazon.Lambda.DurableExecution`.
   - The wrapper DOES need `TInput`/`TOutput` to emit **explicit** generic arguments (see Section 4 correction) — read from `LambdaMethod.Parameters[0].Type.FullName` and `LambdaMethod.ReturnType.TaskTypeArgument`. No new model fields are required; the existing model already carries these.

**Branch-ordering is load-bearing** (reviewer): if these run after the API/ALB checks, a method routes to the wrong template. A test must assert a file containing both a durable and an API method produces the durable wrapper for the durable method.

---

## 4. Generated Handler Wrapper

The wrapper is a **typed-envelope** method (matches README line 53's `HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>` contract), **NOT** Stream→Stream.

**Why typed, not Stream→Stream (VERIFIED dual-serializer hazard):** `DurableFunction.WrapAsyncCore` (verified line 79) reads the serializer off the **context** via `LambdaSerializerHelper.GetRequired(lambdaContext)`, not off any wrapper field. A Stream→Stream wrapper that deserialized with its own `serializer` field (a different instance than the one the bootstrap attaches to the context) would be a real bug. So the wrapper does typed in/typed out and lets the runtime `HandlerWrapper` do envelope (de)serialization.

**Generated signature:**
```csharp
public async Task<Amazon.Lambda.DurableExecution.DurableExecutionInvocationOutput> <MethodName>(
    Amazon.Lambda.DurableExecution.DurableExecutionInvocationInput __request__,
    ILambdaContext __context__)
```

**Generated body (single delegation, bound method-group):**
```csharp
return await Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync(
    <resolvedInstance>.<UserMethod>, __request__, __context__);
```
- `<resolvedInstance>` = the `containingType` field (non-DI) or `scope.ServiceProvider.GetRequiredService<T>()` (DI). Both resolution paths already exist in `FieldsAndConstructor`.
- **Which overload (VERIFIED, four exist — DurableFunction.cs lines 36-71):** the wrapper uses the **three-argument** (no explicit client) overloads — `WrapAsync<TInput,TOutput>(Func<TInput,IDurableContext,Task<TOutput>>, …)` for a typed-returning method or `WrapAsync<TInput>(Func<TInput,IDurableContext,Task>, …)` for a void method. The lazy `_cachedLambdaClient` (line 30) backs the no-client path — correct for the generated case.
- **CORRECTION (2026-06-08, found by Component H): the wrapper MUST emit EXPLICIT generic type arguments.** The original plan said to emit none and rely on overload resolution — that is **wrong** and produces `CS0411` ("type arguments cannot be inferred"): C# cannot infer `TInput`/`TOutput` from a **method-group** argument bound to a `Func<,,>` parameter. Every real call site confirms this — README line 61 (`WrapAsync<Order, OrderResult>(Workflow, …)`) and all `DurableFunctionTests` use explicit generics. The generated wrapper therefore emits `WrapAsync<TInput, TOutput>(instance.Method, …)` for typed workflows and `WrapAsync<TInput>(instance.Method, …)` for void (`Task`) workflows, where `TInput` = the user method's first parameter type and `TOutput` = the `Task<TOutput>` argument. Verified by a compile test that the explicit-generic call binds and the inference-free form fails with `CS0411`.
- The wrapper does **not** deserialize a Stream, does **not** touch its own `serializer` field, and does **not** reconstruct `[FromX]` params.

**MUST-FIX (reviewer): signature constraint must be validated.** Method-group overload resolution assumes `Task` or `Task<T>`. A `ValueTask`-returning or wrong-shape user method produces a C# compile error in generated code. `LambdaFunctionValidator.ValidateFunction` must add a durable-specific check: the user method must be exactly `(TInput, IDurableContext) -> Task` or `-> Task<T>`; otherwise emit `DurableExecutionInvalidSignature` (Error) and set `IsValid=false`.

**MUST-FIX (reviewer): runtime serializer contract.** `WrapAsyncCore` calls `LambdaSerializerHelper.GetRequired(__context__)` and throws if no serializer is on the context. The generated wrapper assumes the bootstrap populated `ILambdaContext.Serializer`. This is a runtime contract not exercisable in generator snapshot tests; the `DurableExecutionInvoke.tt` template must carry a code comment stating the serializer is expected from the context, and Component A must include a serializer round-trip unit test (Section 8).

**Build note (IMPORTANT, discovered during Component C):** there is **no command-line T4 step**. The `<Generator>TextTemplatingFilePreprocessor</Generator>` entries are VS-design-time only; `dotnet build` compiles the **committed** `.cs` partials, not the `.tt`. So every template requires THREE checked-in files kept in sync: `X.tt` (source of truth), `X.cs` (the T4-style transform output — `TransformText()` + the generated boilerplate base class), and `XCode.cs` (the constructor partial holding `_model`). The durable body is a single delegation line, authored across all three for `DurableExecutionInvoke`.

**Template wiring (IMPLEMENTED):**
- `LambdaFunctionTemplate.tt` **and** `LambdaFunctionTemplate.cs` — durable branch placed **FIRST** in the dispatch chain (`if (Events.Contains(EventType.DurableExecution)) Write(new DurableExecutionInvoke(_model)...)`), before Authorizer/API/ALB/else. Both files edited (the `.cs` is what compiles).
- `DurableExecutionInvoke.tt` + `.cs` + `Code.cs` (NEW) — emits `return await Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync<TInput[, TOutput]>(<instance>.<Method>, __request__, __context__);` with **explicit** generic arguments (see Section 4 correction — `WrapAsync<TInput, TOutput>` for typed, `WrapAsync<TInput>` for void). `<instance>` is the camel-cased containing-type field (non-DI) or the DI-resolved local that `LambdaFunctionTemplate`'s shared prologue already sets up. csproj registered the new `.tt`/`.cs` pair like its siblings.
- `GeneratedMethodModelBuilder` (IMPLEMENTED) — durable branches in `BuildResponseType` (→ `Task<DurableExecutionInvocationOutput>`), `BuildParameters` (→ `DurableExecutionInvocationInput __request__, ILambdaContext __context__`), and `BuildUsings` (adds `Amazon.Lambda.DurableExecution`). The durable check is placed before the API/Authorizer/ALB checks in each.

**Original "separate template" wiring notes (superseded by the above):**
- `Templates/LambdaFunctionTemplate.tt` — add `else if (_model.LambdaMethod.Events.Contains(EventType.DurableExecution)) { Write(new DurableExecutionInvoke(_model).TransformText()); }` placed **FIRST**, before the Authorizer/API/ALB branches. The signature line already renders the forced params/return from `GeneratedMethod`, with `async` emitted because the return is a generic `Task`.
- `Templates/DurableExecutionInvoke.tt` (NEW, + checked-in `.cs` partial if the existing template convention requires one) — emits the single `WrapAsync` delegation, handling DI (`scope.ServiceProvider`) and non-DI (`containingType` field) resolution. **MUST-FIX: this template must be authored before snapshots can be produced.**
- `ExecutableAssembly.tt` — **no change.** Verified: it already emits `Func<{p.Type.FullName}, {ReturnType.FullName}>` generically and calls `LambdaBootstrapBuilder.Create(handler, new SerializerName())`. A regression test asserts no change is needed for durable return types.

**DI lifetime (reviewer gap):** the DI scope is **per-invocation**, matching existing API-Gateway scope semantics — the scope is created and disposed around a single Lambda invocation, NOT held open across a multi-hour suspended workflow (the service re-invokes; each invocation gets a fresh scope). Document this in the template comment.

---

## 5. CloudFormation / SAM Template Changes

`DurableConfig` is a function **Properties** block (not a SAM `Events` entry), tracked via a `Metadata` marker, modeled exactly on the verified `SyncedFunctionUrlConfig` pattern (`CloudFormationWriter.cs` lines 245-249 write the marker; lines 267+ do orphan removal).

In `ProcessLambdaFunctionEventAttributes` (verified switch at lines 220-262), add:
```csharp
case AttributeModel<DurableExecutionAttribute> durableModel:
    ProcessDurableExecutionAttribute(lambdaFunction, durableModel.Data);  // Data is DurableExecutionAttribute
    hasDurableExecution = true;   // initialized = false near line 218
    break;   // do NOT add to currentSyncedEvents — durable is not an event
```

`ProcessDurableExecutionAttribute` writes (only when the corresponding `IsXxxSet` flag is true):
- `Resources.<Fn>.Properties.DurableConfig.RetentionPeriodInDays`
- `Resources.<Fn>.Properties.DurableConfig.ExecutionTimeout`
- marker `Resources.<Fn>.Metadata.SyncedDurableConfig = true`

**Expected JSON shape** (snapshot expectation, resolving the reviewer's ambiguity):
```json
"Properties": {
  "DurableConfig": { "RetentionPeriodInDays": 7, "ExecutionTimeout": 300 }
}
```
YAML equivalent under `Properties: DurableConfig:`.

**Orphan removal** (mirroring the `FunctionUrl` block at lines 267+): when `!hasDurableExecution`, if `Metadata.SyncedDurableConfig` is true, `RemoveToken Properties.DurableConfig`, remove the injected checkpoint policy (Section 6), and remove the markers.

**Runtime:** NOT set here. Forced at model-build time (Section 7), because `ProcessPackageTypeProperty` line 185 (`SetToken …Runtime = lambdaFunction.Runtime`) would clobber any writer-side injection in the Zip branch.

**PackageType:** durable functions deploy as Zip. The earlier draft proposed a `DurableExecutionZipOnly` (`AWSLambda0141`) error to reject `PackageType.Image`; that diagnostic was **not shipped** (`AWSLambda0141` was never allocated). The generated wrapper and `DurableConfig`/IAM emission key off the `[DurableExecution]` attribute regardless of package type.

**Tool guard:** the existing `Metadata.Tool = Amazon.Lambda.Annotations` guard is preserved (DurableConfig only written/refreshed for generator-owned functions).

---

## 6. IAM Policy Statements for Checkpoint APIs

**Action names (VERIFIED against the reference template, 2026-06-08):** attested snapshot from `C:\dev\repos\aws-durable-execution-sdk-python\packages\aws-durable-execution-sdk-python-examples\template.yaml` (the file is JSON despite the `.yaml` extension), `DurableFunctionRole.Properties.Policies[0]`, lines 43-60:
```json
"Policies": [
  {
    "PolicyName": "DurableExecutionPolicy",
    "PolicyDocument": {
      "Version": "2012-10-17",
      "Statement": [
        {
          "Effect": "Allow",
          "Action": [
            "lambda:CheckpointDurableExecution",
            "lambda:GetDurableExecutionState"
          ],
          "Resource": "*"
        }
      ]
    }
  }
]
```
So the two checkpoint actions are confirmed: `lambda:CheckpointDurableExecution`, `lambda:GetDurableExecutionState`.

**FLAGGED — the reference IAM pattern diverges MORE than first assumed (corrected 2026-06-08 after reading the full reference template):**
- The reference does **not** put any IAM on the function resources at all. It defines a **single shared standalone `AWS::IAM::Role`** (`DurableFunctionRole`, lines 25-62) carrying `ManagedPolicyArns: [AWSLambdaBasicExecutionRole]` **plus** the inline `PolicyName: DurableExecutionPolicy` above, and **every `AWS::Serverless::Function` sets `Role: {Fn::GetAtt: [DurableFunctionRole, Arn]}`** (e.g. lines 69-74) — no function uses a SAM `Policies` array.
- **Consequence for this plan's design:** under the plan's own rule "when `lambdaFunction.Role` IS set, do NOT touch IAM," the reference pattern would never trigger the plan's injection — because in the reference, every function *does* set `Role`. The generator's auto-IAM path (no explicit `Role` → emit a SAM `Policies`-array inline statement) is therefore **a distinct, generator-idiomatic adaptation, not a reproduction of the reference**. The SAM transform expands a per-function `Policies` array into a generated per-function role, so it is functionally equivalent (each function gets the two actions), but the resulting template shape (N generated roles vs. one shared role) differs from the reference.
- **DECISION (2026-06-08, REVISED): append the AWS-managed policy ARN to the per-function SAM `Policies` array** — `arn:aws:iam::aws:policy/service-role/AWSLambdaBasicDurableExecutionRolePolicy`. This is the mechanism the generator already uses for `AWSLambdaBasicExecutionRole` (append a string to the `Policies` list), so it stays generator-idiomatic, but it references the AWS-published managed policy instead of injecting a hand-rolled inline statement. The managed policy bundles the basic-execution Logs actions **plus** `lambda:CheckpointDurableExecution` / `lambda:GetDurableExecutionState`. This is exactly what the durable integration tests attach to their function roles (`DurableFunctionDeployment.cs` — `AttachRolePolicyAsync(..., AWSLambdaBasicDurableExecutionRolePolicy)`).
  - **Why not an inline `Resource: "*"` statement** (the original draft, now superseded): the per-execution `DurableExecutionArn` is allocated at runtime and is unknowable at template-synth time, so an inline statement could only ever use `Resource: "*"`. Referencing the managed policy hands resource scoping to AWS — if/when the service publishes resource-level conditions, the managed policy updates with no template change required. It also avoids the heterogeneous string/object `Policies` array entirely (the array stays all-strings), removing the round-trip risk the inline approach carried.
  - ~~Shared standalone role (matches the Python reference exactly): generator emits one `DurableFunctionRole` resource and points every durable function's `Role` at it. Larger change to the writer (it does not emit standalone roles today) and interacts with user-specified `Role`.~~ Not chosen.

When `[DurableExecution]` is present AND `lambdaFunction.Role` is NOT set, after `ProcessLambdaFunctionProperties` has run (so the `Policies` array exists from the line 161-166 split), read-modify-write `Properties.Policies` via `GetToken`/`SetToken(TokenType.List)`, appending the managed-policy ARN string:
```json
"Policies": [
  "AWSLambdaBasicExecutionRole",
  "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicDurableExecutionRolePolicy"
]
```
The array stays all-strings. Track via `Metadata.SyncedDurablePolicy = true` for idempotent regeneration; remove the managed-policy entry + marker on orphan removal. Idempotency/orphan-removal recognize the entry by exact ARN match (no Sid bookkeeping needed).

**When `lambdaFunction.Role` IS set** (Role/Policies mutually exclusive — verified lines 155-166): do NOT touch IAM. Emit `DurableExecutionExplicitRoleNeedsCheckpointPolicy` (Info) instructing the user to attach the two actions manually. The diagnostic fires whenever both `[DurableExecution]` and `Role` are present at generation time.

**Note (was the highest regression risk under the inline-statement design):** the managed-policy approach keeps the `Policies` array all-strings, so the heterogeneous string/object round-trip concern no longer applies. A JSON+YAML test still asserts the managed-policy ARN appears alongside `AWSLambdaBasicExecutionRole` and survives re-parse (Section 8, Test G).

---

## 7. Component-by-Component Implementation Steps (real file paths)

All paths are absolute. `.tt` template changes require regenerating the corresponding `.cs` via the project's T4 step.

### Component A — `DurableExecutionAttribute` (public API)
- **NEW** `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.DurableExecution\DurableExecutionAttribute.cs` — the attribute from Section 2.
- Add a serializer round-trip unit test (Section 8).

### Component B — Attribute discovery + model wiring
- `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\TypeFullNames.cs` — four constants.
- `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\Models\EventType.cs` — `DurableExecution` member.
- `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\Models\EventTypeBuilder.cs` — mapping `else if`.
- `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\Models\Attributes\AttributeModelBuilder.cs` — `SymbolEqualityComparer` case + `using Amazon.Lambda.DurableExecution`.
- **NEW** `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\Models\Attributes\DurableExecutionAttributeBuilder.cs` — copied from `ScheduleEventAttributeBuilder.cs`.

### Component C — Generated wrapper shape
- `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\Models\GeneratedMethodModelBuilder.cs` — early `BuildParameters`/`BuildResponseType`/`BuildUsings` branches, ordered before API/ALB.
- **NEW** `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\Templates\DurableExecutionInvoke.tt` (+ generated `.cs`).
- `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\Templates\LambdaFunctionTemplate.tt` — durable branch placed FIRST.
- Verify `ExecutableAssembly.tt` needs no change (regression test).

### Component D — Package/model validation
- `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\Models\LambdaFunctionModelBuilder.cs`.

**Runtime: NO forcing (DECISION 2026-06-08).** Durable functions run on **either `dotnet8` or `dotnet10`**, so the generator does **not** force or override the runtime — the caller-supplied/default `runtime` flows through unchanged exactly like every other function. No `DurableRuntime` constant, no `model.Runtime` override. (This removes the former "MUST-FIX runtime contradiction" and BLOCKING risk #1 entirely.)

- Run the durable validation pass (attribute-value bounds, signature, explicit-role check) and force `IsValid=false` on any Error-severity finding. This is the substance of Component D now that runtime forcing is gone.

**IMPLEMENTED (2026-06-08, Components D+E):** added a `ValidateDurableExecution` method to `LambdaFunctionValidator` (called alongside the other `ValidateXxxEvents`), which adds Error diagnostics to the list — `ReportDiagnostics` already returns `IsValid=false` whenever any Error is present, so no separate gating wiring is needed. Checks: attribute property bounds (`RetentionPeriodInDays`/`ExecutionTimeout`) → `InvalidDurableExecutionAttribute` (0144); signature (param count, second param `== IDurableContext`, return classified via the model's existing `ReturnsVoidOrGenericTask`) → `DurableExecutionInvalidSignature` (0142); explicit `Role` set → `DurableExecutionExplicitRoleNeedsCheckpointPolicy` (0143, Info). **No `OutputKind`/executable gate and no Zip-only check** — both programming models are supported (see Component E). Added `TypeFullNames.IDurableContext`. Two build-system findings: (1) **RS1032** — a `messageFormat` ending in a `{0}` placeholder must use `: {0}` not `. {0}` (trailing-period rule); (2) the SourceGenerators.Tests project **cannot reference the DurableExecution package** (its AWSSDK.Core 4.x downgrades the test project's pinned 3.7.x → NU1605), so diagnostic tests supply minimal durable **stub types as source** (`IDurableContext` / the two envelopes) — the generator only needs them resolvable by metadata name. Diagnostic tests use the `VerifyCS.Test` harness with exact `WithSpan`/`WithArguments` (the framework demands precise locations and prints the expected `DiagnosticResult` on mismatch).

### Component E — Diagnostics set
- `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.Annotations.SourceGenerator\Diagnostics\DiagnosticDescriptors.cs`.

**RESOLVED (2026-06-08): concrete IDs allocated.** Verified against `DiagnosticDescriptors.cs`: the highest pre-existing id was `AWSLambda0139` (`InvalidScheduleEventAttribute`). (Note: `AWSLambda0126` is skipped in the existing file — 0125 jumps to 0127.) The durable descriptors are `AWSLambda0142`–`AWSLambda0144`; **`AWSLambda0140` and `AWSLambda0141` were never allocated** because the executable-only and Zip-only gates they were reserved for were dropped before merge (both programming models are supported — see Component D). All descriptors use `category: "AWSLambdaCSharpGenerator"` and `isEnabledByDefault: true`, matching the file's convention.

**Only THREE new descriptors.** The exclusive-event case is covered by the existing `MultipleEventsNotSupported` (AWSLambda0102): `LambdaFunctionValidator.ValidateFunction` already emits it and returns early with `IsValid=false` whenever `Events.Count > 1`. Component B added `DurableExecutionAttribute` to `TypeFullNames.Events` and `EventType.DurableExecution`, so `[DurableExecution] + [RestApi]` produces `Events.Count == 2` → fires AWSLambda0102 → halts generation. No new exclusive-event diagnostic is needed; just a **test** asserting the combination triggers AWSLambda0102. The durable descriptors:

| Name | Id | Severity | Gates generation? | Message (summary) |
|---|---|---|---|---|
| `DurableExecutionInvalidSignature` | `AWSLambda0142` | Error | Yes (`IsValid=false`) | A `[DurableExecution]` method must be `(TInput, IDurableContext) -> Task` or `-> Task<T>`. |
| `DurableExecutionExplicitRoleNeedsCheckpointPolicy` | `AWSLambda0143` | Info | No | Function uses an explicit Role; attach `lambda:CheckpointDurableExecution` and `lambda:GetDurableExecutionState` manually. |
| `InvalidDurableExecutionAttribute` | `AWSLambda0144` | Error | Yes | `RetentionPeriodInDays`/`ExecutionTimeout` must be positive integers. |

**Exclusive-event enforcement:** handled by the existing `MultipleEventsNotSupported` (AWSLambda0102) — see above. No new diagnostic.

**No executable gate (decision changed before merge):** an earlier draft kept an executable-only gate keyed off `context.Compilation.Options.OutputKind`. That gate was removed: durable functions are valid as both class libraries and executables on the managed runtime, and the generated wrapper is identical for both (only the deployed `Handler` string differs, which the model already derives from `IsExecutable`). The generator therefore performs **no `OutputKind`/`isExecutable` check** for durable functions.

### Component F — CFN `DurableConfig` writer (IMPLEMENTED 2026-06-08)
- `CloudFormationWriter.cs` — added a `case AttributeModel<DurableExecutionAttribute>` to the event-attribute switch that calls `ProcessDurableExecutionAttribute` and sets `hasDurableExecution = true` (and does NOT add to `currentSyncedEvents` — durable is a Properties/IAM concern, not an event). `ProcessDurableExecutionAttribute` clears any prior `DurableConfig`, re-emits `RetentionPeriodInDays`/`ExecutionTimeout` only when their `IsXxxSet` flags are true (creating an empty `DurableConfig` object via `TokenType.Object` when neither is set so the function is still marked durable), and sets the `Metadata.SyncedDurableConfig` marker. Orphan removal mirrors the verified `FunctionUrl` block.

### Component G — CFN checkpoint IAM writer (IMPLEMENTED 2026-06-08; REVISED to managed policy)
- `CloudFormationWriter.cs` — kept inline (no separate writer class), matching `ProcessFunctionUrlAttribute` style. When `Role` is empty, `AddDurableCheckpointPolicy` reads the existing `Policies` via `GetToken<List<object>>`, appends the managed-policy ARN string `arn:aws:iam::aws:policy/service-role/AWSLambdaBasicDurableExecutionRolePolicy` (constant `DurableCheckpointManagedPolicy`), and re-sets with `TokenType.List` — producing an all-strings array (`["AWSLambdaBasicExecutionRole", "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicDurableExecutionRolePolicy"]`). Idempotency + orphan removal use `IsDurableCheckpointPolicy` (exact ARN string match). When `Role` is set, IAM is left untouched and `AWSLambda0143` (Info) is emitted in the validator.
- **No heterogeneous-array risk:** because the entry is a managed-policy ARN string (not an inline statement object), the `Policies` array stays all-strings and round-trips trivially through both `JsonWriter` and `YamlWriter`. The earlier inline-statement design's mixed string/object round-trip concern is moot. Verified by `DurableExecution_AddsManagedCheckpointPolicy` (JSON + YAML) plus idempotency and orphan-removal tests.

### Component H — End-to-end / compile tests (IMPLEMENTED 2026-06-08)
- `DurableExecutionWrapperCompilesTests.cs` — compiles the exact generated wrapper shape against realistic `WrapAsync` overloads. **This layer found a real bug:** the planned no-explicit-generics call fails with `CS0411` (see Section 4 correction). Tests assert the typed (`WrapAsync<TInput,TOutput>`) and void (`WrapAsync<TInput>`) forms bind, and a guard test asserts the inference-free form fails with `CS0411`.
- Note on approach: a full `Microsoft.CodeAnalysis.Testing` snapshot E2E (committed `.g.cs` + `Program.g.cs` + RuntimeSupport sources) was attempted but is high-friction here (exact `AWSLambda0103` content match + the AWSSDK.Core 3.7.x/4.x conflict that blocks referencing the durable package). The compile-test approach covers the unique remaining risk (overload binding) without that friction; the wrapper *text* is pinned by Component C's template tests and the *template* output by F/G's writer tests.

### Component I — Change file + docs (IMPLEMENTED 2026-06-08)
- `.autover/changes/durable-execution-annotations-integration.json` — single `Amazon.Lambda.Annotations` Minor entry (that autover project spans both the attributes csproj and the SourceGenerator csproj, so it covers everything added here).
- `Amazon.Lambda.DurableExecution/README.md` — added a "Using Lambda Annotations" subsection showing the `[LambdaFunction]` + `[DurableExecution]` model that removes the manual handler/`WrapAsync` boilerplate.
- **NEW** `C:\dev\repos\aws-lambda-dotnet\.autover\changes\<guid>.json` — increment **Minor**, projects `Amazon.Lambda.Annotations.SourceGenerator` + `Amazon.Lambda.DurableExecution`. Create via `autover change`.
- Update `C:\dev\repos\aws-lambda-dotnet\Libraries\src\Amazon.Lambda.DurableExecution\README.md` to note that `[DurableExecution]` generates the handler wiring for both the executable and class-library models.

---

## 8. Test Strategy (snapshot tests)

Snapshot harness: `CSharpGeneratorDriver` against files in `Libraries\test\Amazon.Lambda.Annotations.SourceGenerators.Tests\Snapshots\`. CFN writer tests mirror `WriterTests\FunctionUrlTests.cs`, parameterized `[InlineData(CloudFormationTemplateFormat.Json)]` / `[InlineData(CloudFormationTemplateFormat.Yaml)]`.

**Unit (Component A)** — `Libraries\test\Amazon.Lambda.DurableExecution.Tests\` (or the existing durable test project): constructor defaults, `IsXxxSet` tracking, `Validate()` rejects `<= 0`. **Serializer round-trip:** the default `ILambdaSerializer` deserializes `DurableExecutionInvocationInput` and serializes `DurableExecutionInvocationOutput` including `UpperSnakeCaseEnumConverter` on `InvocationStatus` (Succeeded/Failed/Pending), and a nested `InitialExecutionState`/`Operations` round-trips without loss. This must pass before the typed-envelope wrapper is relied upon.

**Generated-wrapper snapshots (Component C):**
- A. Non-DI typed-output method → verify signature (`DurableExecutionInvocationInput`/`ILambdaContext` params, `Task<DurableExecutionInvocationOutput>` return) and single `WrapAsync(containingType.Method, __request__, __context__)` delegation, no Stream deserialization.
- B. DI variant → `scope.ServiceProvider.GetRequiredService<T>()` resolution.
- C. Void user method (`Task` return) → confirms overload resolution compiles without explicit generic args.
- D. **Branch-ordering test:** one file with both a durable method and a `[RestApi]` method → durable method gets the durable wrapper.
- E. `ExecutableAssembly.tt` regression → executable assembly snapshot unchanged in shape for durable return types.

**Diagnostics (Component E):** one test each for `DurableExecutionInvalidSignature` (ValueTask / wrong params), `InvalidDurableExecutionAttribute` (non-positive `RetentionPeriodInDays`/`ExecutionTimeout`), and `DurableExecutionExplicitRoleNeedsCheckpointPolicy` (explicit Role). Plus a test that `[DurableExecution]` + `[RestApi]` triggers the **existing** `MultipleEventsNotSupported` (AWSLambda0102). For the durable Errors (and AWSLambda0102), also assert no wrapper is generated (`IsValid=false`).

**CFN (Components F/G)** — `Libraries\test\Amazon.Lambda.Annotations.SourceGenerators.Tests\WriterTests\DurableExecutionTests.cs` (NEW):
- F1. `DurableConfig` with both props set (JSON + YAML); `Metadata.SyncedDurableConfig == true`.
- F2. Partial emit — only `RetentionPeriodInDays` set → `ExecutionTimeout` absent.
- F3. Orphan removal — attribute dropped → `DurableConfig` + marker removed.
- G. Managed-policy injection (JSON + YAML), asserting the `Policies` array is `["AWSLambdaBasicExecutionRole", "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicDurableExecutionRolePolicy"]` after write and re-parse.
- G2. Idempotency — regeneration does not duplicate the policy statement.
- G3. Role suppression — `Role` set → `Policies` untouched, Info diagnostic emitted.

Snapshot fixtures with the exact JSON/YAML shapes (Sections 5 and 6) must be authored as part of this work, not deferred.

---

## 9. Risks, Open Questions, and Must-Fix-First Items

### BLOCKING (resolve before implementation starts)
1. ~~**Runtime string is undefined infra.**~~ **DROPPED (2026-06-08): not an issue.** Durable functions run on either `dotnet8` or `dotnet10`, so the generator does **not** force a runtime — it lets the user's normal runtime selection flow through. No `DurableRuntime` constant, no override. (Component D no longer touches runtime at all.)
2. ~~**IAM emission shape — role shape still a DECISION.**~~ **RESOLVED (2026-06-08): Option 1 — per-function SAM `Policies`-array inline statement**, matching how the generator already emits `AWSLambdaBasicExecutionRole`. Action names verified against the reference snapshot (`lambda:CheckpointDurableExecution`, `lambda:GetDurableExecutionState`; lines 51-54). The remaining risk here is purely mechanical — the mixed string/object `Policies` array round-trip (see item 7), not a shape decision.
3. ~~**Diagnostic IDs.**~~ **RESOLVED (2026-06-08): `AWSLambda0142`–`AWSLambda0144`** (highest pre-existing is `AWSLambda0139`; `0126` is skipped in the file). `AWSLambda0140`/`AWSLambda0141` were never allocated (the executable-only and Zip-only gates were dropped). Three new descriptors — the exclusive-event case reuses the existing `AWSLambda0102`. See the Section 7 / Component E table.

### REQUIRED-BEFORE-CODING (artifacts that gate the rest)
4. Author `DurableExecutionInvoke.tt` first — snapshots cannot exist without it.
5. Create `DurableExecutionAttributeBuilder.cs` by copying the real `ScheduleEventAttributeBuilder.cs`, not from prose.
6. Author the exact JSON/YAML snapshot fixtures for `DurableConfig` and the all-strings `Policies` array (managed-policy ARN).

### Resolved (was a regression risk under the inline-statement design)
7. ~~**Mixed string/object `Policies` array** round-trip.~~ **RESOLVED by switching to the managed-policy ARN** (`AWSLambdaBasicDurableExecutionRolePolicy`): the `Policies` array stays all-strings, so there is no heterogeneous-type round-trip concern. See Section 6 / Component G.

### Correctness gates (enforced via `IsValid=false`, not severity alone)
8. **Validation gates must set `IsValid=false`** (diagnostic severity alone does not halt generation). Applies to `DurableExecutionInvalidSignature` and `InvalidDurableExecutionAttribute`; `ReportDiagnostics` returns `IsValid=false` whenever any Error diagnostic is present, so adding them to the list is sufficient. The exclusive-event case is already handled by the existing `MultipleEventsNotSupported` (AWSLambda0102), which returns early with `IsValid=false`. (There is no executable-only or Zip-only gate — both programming models are supported.)
9. **Branch ordering** is load-bearing in two files (`GeneratedMethodModelBuilder` and `LambdaFunctionTemplate.tt`) — durable must be checked before API/HttpApi/ALB. Covered by Test D.
10. **Signature constraint** — `ValueTask`/non-`(TInput, IDurableContext)` returns produce generated-code compile errors. `ValidateFunction` must reject them.
11. **Runtime serializer contract** — `WrapAsyncCore` reads the serializer off `__context__` (verified line 79); the generated wrapper assumes the bootstrap populated it. Not testable in snapshots; covered by Component A's round-trip unit test + a template comment.

### Accepted-for-preview (documented follow-ups, not promises)
12. ~~`Resource: "*"` on the inline checkpoint statement is broad.~~ **RESOLVED** by referencing the AWS-managed `AWSLambdaBasicDurableExecutionRolePolicy` instead of a hand-rolled inline statement (Section 6). Resource scoping is now AWS's responsibility — the per-execution durable ARN is allocated at runtime and is unknowable at synth time, so an inline statement could only ever use `"*"` anyway. If/when the service publishes resource-level conditions, the managed policy updates with no template change.
13. **TypeFullNames must exactly match** `Amazon.Lambda.Annotations.DurableExecutionAttribute` or the attribute is silently skipped → routed to `NoEventMethodBody`. Covered by the discovery test.

### Open questions deferred (non-blocking)
15. Upper bounds for `RetentionPeriodInDays`/`ExecutionTimeout` — `Validate()` only rejects `<= 0` now; tighten once service limits are published.
16. Whether, when a user adds an explicit `Role` to a function that previously had an auto-injected checkpoint policy, the old policy should be actively removed. The Role/Policies mutual-exclusivity (lines 155-166) clears `Policies` automatically in `ProcessLambdaFunctionProperties`, so the stale statement is removed as a side effect; verify this in the Role-suppression test and document it.
