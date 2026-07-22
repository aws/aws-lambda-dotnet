# Durable Execution support in Lambda Test Tool v2 — Design

**Status:** Proposed · **Phase 0 spike:** ✅ complete (endpoint redirection verified)
**Audience:** contributors to `Tools/LambdaTestTool-v2` and `Amazon.Lambda.DurableExecution*`

## 1. Goal

Let a developer run a **real** durable Lambda function locally (`dotnet run`, debugger attached) and
exercise a full multi-invocation durable workflow — steps, waits, retries, callbacks, child contexts,
parallel/map — without deploying to AWS and without a real durable-execution service. The Test Tool
already emulates the Lambda Runtime API for ordinary functions; this adds the **durable-execution
service data plane** as a sibling emulator on the same host.

Two experiences result:
- **Headless** — invoke a durable workflow and watch it drive to completion (time-skipped by default).
- **Interactive** — step through the replay cycle in a debugger; inspect the operation timeline and fire
  callbacks from the Blazor UI.

The in-process `DurableTestRunner` (in `Amazon.Lambda.DurableExecution.Testing`) already covers automated
unit/integration testing. It is **not** a substitute for this: it drives an in-process C# delegate, so it
cannot run the developer's real, separately-launched executable under a debugger — which is the entire
point of the Test Tool. See §3 for why the orchestrator cannot simply be reused.

## 2. How the two systems work today (verified)

### 2.1 Test Tool v2 (`Tools/LambdaTestTool-v2/src/Amazon.Lambda.TestTool`)

- Accepts SDK invokes at `POST /2015-03-31/functions/{functionName}/invocations` and a fixed
  `/functions/function/invocations` default variant (`LambdaRuntimeAPI.cs:25-26`).
- Queues events into a per-function `RuntimeApiDataStore`, partitioned **by URL path** through
  `RuntimeApiDataStoreManager` (a `ConcurrentDictionary` keyed by function name, `:40-52`). The partition
  is selected by the path, **not** by `AWS_LAMBDA_FUNCTION_NAME`.
- Exposes the Runtime API a local bootstrap polls: `GET /{fn}/2018-06-01/runtime/invocation/next`
  (`GetNextInvocation`, polls `TryActivateEvent` every 100 ms, `:120`) and
  `POST .../invocation/{awsRequestId}/response` (`PostInvocationResponse` → `ReportSuccess`, `:189`).
- `EventContainer` tracks a request/response pair and blocks the caller in RequestResponse mode via
  `WaitForCompletion` — a **fail-safe 15-minute bounded wait**, not indefinite (`EventContainer.cs:86-96`).
  RequestResponse is the default unless `X-Amz-Invocation-Type` says otherwise (`LambdaRuntimeAPI.cs:56-60`).
- **Hosting model:** `TestToolProcess` is a plain class with a static `Startup(...)` factory (not a
  `BackgroundService`, not a base class); endpoints register from `TestToolProcess.cs:108`. Only the SQS /
  DynamoDB event sources use real `BackgroundService`. The API Gateway emulator uses a
  `app.Map("/{**catchAll}", ...)` catch-all (`ApiGatewayEmulatorProcess.cs:86`). Subsystems are wired into
  the run from `RunCommand.cs:49-97` — which starts **only the tool's own web app and a browser**; it does
  **not** launch the user's function.

### 2.2 Durable Execution SDK (`Libraries/src/Amazon.Lambda.DurableExecution`)

- The function's handler delegates to `DurableFunction.WrapAsync`, which hydrates `ExecutionState` from the
  invocation envelope's `InitialExecutionState.Operations` (+ `NextMarker` paging), runs the workflow, and
  returns a `DurableExecutionInvocationOutput` with status **Succeeded / Failed / Pending**.
- During a run it calls exactly **two** data-plane RPCs through `IDurableServiceClient`
  (`Services/IDurableServiceClient.cs`): `CheckpointAsync` (flush `OperationUpdate`s) and
  `GetExecutionStateAsync` (page state). The production adapter `LambdaDurableServiceClient` calls
  `IAmazonLambda.CheckpointDurableExecutionAsync` / `GetDurableExecutionStateAsync`.
- **The SDK owns no timers.** Suspension = returning a never-completing Task and emitting `Pending`
  (`TerminationManager`). Timers, retry backoff, and re-invocation are the **service's** responsibility.
- By default `DurableFunction.cs:30-31` constructs `new AmazonLambdaClient()` with **no config**, cached in
  a `Lazy`. There is also a `WrapAsync(..., IAmazonLambda lambdaClient)` overload (`DurableFunction.cs:46-52`)
  for supplying a custom client.

### 2.3 Testing package (`Amazon.Lambda.DurableExecution.Testing`)

- `ExecutionOrchestrator` plays the service role **in-process**: it repeatedly calls
  `DurableFunction.WrapAsync` with an in-memory `IDurableServiceClient`, interprets `Pending`, and drives
  re-invocation until terminal (`ExecutionOrchestrator.cs:83-136`).
- Time-skipping lives in `CheckpointProcessor.ApplyTimeSkipping` (`CheckpointProcessor.cs:273-298`), which
  mutates stored ops (`WAIT → Succeeded`, retry `STEP → Ready`) **at checkpoint-application time**.
- The reusable backend trio — `InMemoryOperationStore`, `CheckpointProcessor`, `InMemoryDurableServiceClient`
  — are all `internal sealed`, wired at `DurableTestRunner.cs:60-62`. They already traffic in
  `Amazon.Lambda.Model.OperationUpdate` / `Operation`.

### 2.4 Wire format (`aws-sdk-net` `generator/ServiceModels/lambda/lambda-2015-03-31.normal.json`)

- REST-JSON with path templates. `CheckpointDurableExecution` →
  `POST /2025-12-01/durable-executions/{DurableExecutionArn}/checkpoint`; `GetDurableExecutionState` →
  `GET /2025-12-01/durable-executions/{DurableExecutionArn}/state`; callbacks →
  `POST /2025-12-01/durable-execution-callbacks/{CallbackId}/{succeed|fail|heartbeat}`.
- **There is no `StartDurableExecution` operation** and **no named `DurableExecutionInvocationInput/Output`
  wire shape.** An execution starts as an ordinary `Invoke` carrying the `X-Amz-Durable-Execution-Name`
  header; the response returns `X-Amz-Durable-Execution-Arn`. The `InvocationInput/Output` envelope is an
  **SDK-side POCO** that rides the opaque Runtime-API event/response.
- `DurableExecutionArn` **contains slashes** (`arn:...:function:NAME:QUALIFIER/durable-execution/GROUP/NAME`),
  so an emulator router must use a catch-all and handle URL-encoded slashes.
- Payload caps: 6 MB max `OperationPayload`/`InputPayload` string length; effective caps are 256 KB
  (CONTEXT/STEP/WAIT/CALLBACK) and 1 MB (async/chained). `ReplayChildren` exists on both checkpoint updates
  (`ContextOptions.ReplayChildren`) and state responses (`ContextDetails.ReplayChildren`).
- **ID allocation is split:** the **server (emulator)** allocates the `DurableExecutionArn` and the rotating
  `CheckpointToken`; the **SDK (client)** allocates operation IDs and callback IDs. The emulator only mints
  ARNs and tokens.

## 3. Corrections to the original investigation

| Claim | Verdict | Correction |
|---|---|---|
| Reuse `ExecutionOrchestrator` in the Test Tool | **Rejected** | It holds a generic in-process `Func<TInput,IDurableContext,Task<TOutput>>` (`ExecutionOrchestrator.cs:16`, invoked `:95`) with no serialization boundary. It cannot drive a separate process. Reuse the **backend trio**; **re-write** the ~50-line drive loop over the wire. |
| Test Tool injects env vars into the function process | **False** | The Test Tool never launches the function (`RunCommand.cs:49-97`). The **user** sets the env vars on their own `dotnet run`, matching today's `AWS_LAMBDA_RUNTIME_API` UX. |
| Redirect the durable client via a ServiceURL hook in the SDK | **Resolved (see §4)** | No bespoke SDK hook; relies on stock AWSSDK `AWS_ENDPOINT_URL_LAMBDA` resolution. **Verified working** by the Phase-0 spike. |
| Feed `CheckpointDurableExecutionRequest` straight into `CheckpointProcessor.Process` | **False as written** | `Process` takes `Amazon.Lambda.Model.OperationUpdate`, whose `Action`/`Type` are AWSSDK `ConstantClass` types with **no System.Text.Json contract**. The emulator must define its own rest-json DTOs and **hand-map** them into the model types. This mapper is the bulk of the data-plane work. |
| `TestToolProcess`-style background services | **Partly** | `TestToolProcess` is not a `BackgroundService` and not a base class. The durable driver should follow the Runtime-API model — a per-execution `Task` — not `AddHostedService`. |
| Time-skipping incompatible with a wall-clock re-invoke loop | **Resolved** | True only against the **real** service. Here the Test Tool **owns** the store the function reads back on replay, so `ApplyTimeSkipping` is authoritative and is the correct fast default. |
| IVT already lets the Test Tool reuse the backend trio | **False** | The trio is `internal sealed`; `.Testing` grants IVT only to `.Testing.Tests`. A **new IVT entry (or public facade)** plus matching strong-name is required. Prefer a small public facade to avoid coupling to 0.x internals. |

## 4. Phase 0 spike — endpoint redirection (COMPLETE ✅)

**Question:** does the bundled `AWSSDK.Lambda 4.0.13.1` route `CheckpointDurableExecutionAsync` to
`AWS_ENDPOINT_URL_LAMBDA` when the client is `new AmazonLambdaClient()` with no config — i.e. exactly the
`DurableFunction` default path?

**Method** (`C:\tmp\durable-endpoint-spike`): a local `HttpListener` stub; set only
`AWS_ENDPOINT_URL_LAMBDA`, `AWS_REGION`, and dummy creds; construct `new AmazonLambdaClient()`; call
`CheckpointDurableExecutionAsync` and observe where the request lands.

**Result: SUCCESS.** The SDK resolved `client.Config.ServiceURL` to the stub and sent:
```
POST /2025-12-01/durable-executions/arn%3Aaws%3Alambda%3A...%2Fdurable-execution%2Fg%2Fe/checkpoint
User-Agent: aws-sdk-dotnet-coreclr/4.0.13.1 ... api/Lambda#4.0.13.1
```
Confirmed in AWSSDK.Core: `ClientConfig.ServiceURL` reads the service-specific
`AWS_ENDPOINT_URL_LAMBDA` (then global `AWS_ENDPOINT_URL`) whenever `IgnoreConfiguredEndpointUrls == false`
(default) and `ServiceId != null` (`ClientConfig.cs:340-395`).

**Implications:**
- The **zero-user-code-change** story holds: the developer sets env vars, no SDK modification needed.
- Note the ARN's slashes arrive **URL-encoded** (`%2F`) — the emulator router must decode them.
- The user must also set `AWS_REGION` + dummy `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`, or SigV4 signing
  throws before the request leaves the process; the emulator ignores the signature.
- **Fallback (not needed):** if a future SDK build regressed this, the `WrapAsync(IAmazonLambda)` overload
  (`DurableFunction.cs:46-52`) with a caller-built `AmazonLambdaConfig { ServiceURL }` would work but require
  a user code change.

## 5. Recommended architecture

Single Kestrel `WebApplication` (the existing `TestToolProcess`), one port, three route groups, one
out-of-process function.

**Env vars the user sets on their `dotnet run`:**
- `AWS_LAMBDA_RUNTIME_API=127.0.0.1:{port}/{functionName}` — the bootstrap poll target. **Must include the
  function-name path prefix** so the bootstrap's `BaseUrl` hits the named partition (`RuntimeApiDataStoreManager`).
- `AWS_ENDPOINT_URL_LAMBDA=http://127.0.0.1:{port}` — redirects checkpoint/state/callback traffic (verified §4).
- `AWS_REGION` + dummy `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` — required for signing; ignored by the emulator.

**Route group A — Runtime API (existing, unchanged).** Transports the durable envelope in/out via the
event queue + `EventContainer`.

**Route group B — Durable data plane (NEW).** Registered via `SetupDurableServiceEndpoints(WebApplication)`
called next to the Runtime-API setup at `TestToolProcess.cs:108`:
- `POST /2025-12-01/durable-executions/{**path}` (suffix `/checkpoint`, `/stop`)
- `GET  /2025-12-01/durable-executions/{**path}` (bare, `/state`, `/history`)
- `POST /2025-12-01/durable-execution-callbacks/{callbackId}/{succeed|fail|heartbeat}`

Uses a catch-all `{**path}` + suffix parse (the `ApiGatewayEmulatorProcess.cs:86` pattern) because the ARN
contains encoded slashes. Each handler deserializes into **emulator-owned STJ DTOs**, hand-maps to
`Amazon.Lambda.Model.OperationUpdate`, calls `CheckpointProcessor.Process(arn, token, updates)` over a
shared `InMemoryOperationStore`, and returns `NewExecutionState { Operations, NextMarker }` with a
**freshly minted CheckpointToken**.

**Route group C — Durable start (NEW).** Extend `PostEvent` (`LambdaRuntimeAPI.cs:51`) to branch on the
`X-Amz-Durable-Execution-Name` header. It does **not** take the blocking `EventContainer` path; it allocates
the ARN + initial token, seeds an `EXECUTION`-type op with the input payload, launches the drive loop as a
detached `Task`, and returns **202 immediately** with `X-Amz-Durable-Execution-Arn`. Idempotency: same
name + same payload → existing ARN; differing payload → `DurableExecutionAlreadyStartedException`.

**Drive loop (`DurableExecutionDriver`, re-expressing `ExecutionOrchestrator.cs:83-136` over the wire):**
1. Read the **current** CheckpointToken from the store (rotates each checkpoint — never reuse the prior).
2. Build the invocation envelope with a **bounded first page** of `InitialExecutionState.Operations` +
   `NextMarker` (honoring `ReplayChildren`). **Do not inline full history** — it breaches the 6 MB
   queued-event cap (`LambdaRuntimeAPI.cs:14,65`); page the rest via `GET .../state`.
3. `GetLambdaRuntimeDataStore(fn).QueueEvent(inputJson, isRequestResponseMode:true)`
   (`RuntimeApiDataStore.cs:92`), then block on `EventContainer.WaitForCompletion` — the **driver** blocks,
   not the end caller.
4. While the function runs, its redirected client hits route group B; with time-skip on,
   `ApplyTimeSkipping` flips `WAIT → Succeeded` / retry `STEP → Ready` in the store **at checkpoint time**,
   before the function posts its Pending response — so on `WaitForCompletion` return the store shows no
   pending timer.
5. Parse `DurableExecutionInvocationOutput.Status`: `Succeeded`/`Failed` → terminal, store result/error,
   stop. `Pending` → re-drive from step 1. **Serialize passes per ARN** so the abandoned-event resend
   (`LambdaRuntimeAPI.cs:120-134`) can't double-deliver.

**Timers.** Default: time-skip. Optional wall-clock mode: `Task.Delay` to the min pending
`WaitDetails.ScheduledEndTimestamp` (mirrors `ExecutionOrchestrator.cs:123-135`) to watch real backoff.

**Callbacks.** Inbound endpoints are keyed by `{CallbackId}`, **not** ARN. Maintain a `CallbackId → ARN`
reverse map to wake the correct parked driver. The `onNewOperations` cb-id feedback survives the HTTP hop as
long as the checkpoint response's `NewExecutionState.Operations` is populated — `LambdaDurableServiceClient`
already forwards minted cb-ids back into `ExecutionState`.

## 6. Reused vs new

**Reused verbatim:**
- Runtime-API transport: `LambdaRuntimeAPI.cs:26-38,120,189`, `RuntimeApiDataStore.QueueEvent` (`:92`),
  `EventContainer.WaitForCompletion` (`:86-96`), `RuntimeApiDataStoreManager` (`:40-52`).
- Durable state machine: `CheckpointProcessor` (incl. `ApplyTimeSkipping` `:273-298`, cb minting `:167-170`),
  `InMemoryOperationStore`, `InMemoryDurableServiceClient`.
- Public STJ envelope POCOs: `DurableExecutionInvocationInput/Output`, `InitialExecutionState`, `Operation`.
- Hosting/DI seams: `TestToolProcess.cs:43-84,108`; catch-all routing `ApiGatewayEmulatorProcess.cs:86`;
  task-list wiring `RunCommand.cs:49-97`.
- Stock AWSSDK.Lambda `AWS_ENDPOINT_URL_LAMBDA` resolution (verified §4).

**New:**
- `Services/DurableExecution/DurableServiceApi.cs` — `SetupDurableServiceEndpoints`, route group B.
- `Services/DurableExecution/DurableRestJsonDtos.cs` + mapper — emulator-owned STJ DTOs ⇄
  `Amazon.Lambda.Model.OperationUpdate/Operation` (**the largest new component**; must track
  `Action{START/SUCCEED/FAIL/RETRY/CANCEL}`, `Type`, `Status` against `lambda-2015-03-31.normal.json`).
- `Services/DurableExecution/DurableExecutionDriver` (+ `IDurableExecutionDriver`) — start + per-ARN drive
  loop, `ConcurrentDictionary<arn, ExecutionRecord>`, callback reverse-map, `Stop`.
- `DurableExecutionStore` singleton — owns `CheckpointProcessor` + `InMemoryOperationStore` keyed by ARN;
  mints ARNs + rotates tokens.
- Extension of `PostEvent` (`LambdaRuntimeAPI.cs:51`) for the `X-Amz-Durable-Execution-Name` branch.
- `RunCommandSettings` flags: `--durable-execution`, `--durable-time-skip` (default true).
- New **IVT entry / public facade** on the `.Testing` assembly for the Test Tool (+ strong-name match).
- (Phase 3) `Components/Pages/DurableExecutionPanel.razor` — timeline + callback buttons + time control,
  using the existing `StateChange → StateHasChanged` pattern (`Home.razor.cs:114,218`).

## 7. Risks & open questions

1. ~~**Endpoint redirection**~~ — **resolved by the Phase-0 spike (§4).**
2. **rest-json ⇄ SDK-model mapping fidelity.** `ConstantClass` blocks STJ; the hand-map across every nested
   option (Step/Wait/Callback/ChainedInvoke/Context, Error, `ReplayChildren`) is the real cost and must stay
   in sync with the service model. **The most under-estimated piece of work.**
3. **Chained durable-to-durable invokes are not v1.** Out-of-process, `ctx.Invoke` would loop forever on
   `InvokePending`. **Detect and fail fast** with a clear "chained invoke not yet supported" error until Phase 4.
4. **IVT + strong-naming coupling.** Locks the Test Tool version to the 0.x `.Testing` package. Prefer a
   small public facade over raw IVT.
5. **Checkpoint-before-suspend ordering.** The driver time-skips on store state, so `WrapAsyncCore` must
   flush the WAIT/retry op over HTTP **before** posting Pending, not on async dispose after. Verify.
6. **15-min `WaitForCompletion` cap** at a breakpoint — dev-acceptable, but the driver needs a clean
   abort/re-attach path.
7. **Payload caps / `ReplayChildren`** tiered enforcement (6 MB / 1 MB / 256 KB) — fidelity, deferrable past
   first ship but required before "high-fidelity" is claimed.

## 8. Phased plan

- **Phase 0 — Spike: endpoint redirection (S).** ✅ **Done.** Verified §4.
- **Phase 1 — Data plane + backend reuse (M).** ✅ **Done.** Chose to **copy** `CheckpointProcessor` +
  `InMemoryOperationStore` into the Test Tool (retyped to plain-STJ DTOs) rather than IVT/facade, keeping
  it decoupled from the 0.x preview internals. `DurableServiceApi` catch-all endpoints; `DurableExecutionStore`
  over the copied backend. Delivered under `Services/DurableExecution/`, gated by `--durable-execution`.
  Tests (`DurableServiceApiTests`) exercise checkpoint/state round-trips through a real `AmazonLambdaClient`.
  **Bug found & fixed:** the checkpoint/state *response* is read by the AWSSDK rest-json unmarshaller (not
  System.Text.Json), whose `timestamp` members are unix **seconds** while the `Operation` POCO emits epoch
  **millis** — the direct-POCO serialization overflowed `DateTime`. Fixed with dedicated `WireOperation`
  response DTOs that convert millis→seconds (validates §7 risk #2).
- **Phase 2 — Start hook + drive loop (M).** ✅ **Done.** `X-Amz-Durable-Execution-Name` branch in
  `PostEvent`; `DurableExecutionDriver` (QueueEvent + WaitForCompletion, per-ARN idempotency by name +
  payload hash, paged envelope, time-skip default, fail-fast on chained invokes, park on pending callback).
  Registered when `--durable-execution` is set. `DurableExecutionDriverTests` drives a wait-then-step
  workflow end to end (start → Pending → time-skip → Succeeded) through the real Runtime API + SDK wire.
  **Bug found & fixed:** the ARN's internal slashes arrive percent-encoded (`%2F`) and ASP.NET's catch-all
  preserves them un-decoded, so the checkpoint handler keyed the store under the encoded ARN while the driver
  reads by the raw ARN — the two never met. Fixed by `Uri.UnescapeDataString` on the captured ARN in
  `DurableServiceApi.TrySplit`. (Phase 1 tests didn't catch it because checkpoint+getstate were both
  SDK-encoded and thus internally consistent.) **First genuinely useful ship** — single-function workflows
  headless, debugger-attached.
- **Phase 3 — Callbacks + Blazor UI (M).** Callback endpoints + `CallbackId → ARN` map + driver wake;
  `DurableExecutionPanel.razor` timeline, Send-Callback buttons, time control.
- **Phase 4 — Chained invokes + fidelity polish (L).** Out-of-process nested drive for `CHAINED_INVOKE`
  across registered sibling functions; tiered payload caps + `ReplayChildren`; idempotency; `StopDurableExecution`.

## Appendix — Phase-0 spike source

Kept at `C:\tmp\durable-endpoint-spike\` (throwaway; not checked in). `Program.cs` stands up an
`HttpListener`, sets only `AWS_ENDPOINT_URL_LAMBDA` + region + dummy creds, constructs
`new AmazonLambdaClient()`, and calls `CheckpointDurableExecutionAsync`. Reproduce with `dotnet run -c Release`.
