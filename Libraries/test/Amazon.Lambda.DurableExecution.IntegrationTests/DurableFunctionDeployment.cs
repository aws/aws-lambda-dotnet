// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// Builds, deploys, and invokes a single durable Lambda function for an integration test.
/// Manages the full lifecycle: IAM role, zip package, managed-runtime Lambda function.
/// All resources are torn down on DisposeAsync.
/// </summary>
/// <remarks>
/// Durable functions deploy as a plain zip package on a managed dotnet runtime
/// (executable model, <c>Handler=bootstrap</c>) — no container image or ECR repository
/// required. Each test function is published framework-dependent for linux-x64 and zipped.
/// This harness pins a single managed runtime (see <see cref="ManagedRuntime"/>) for CI;
/// durable execution itself is not tied to that specific runtime version.
/// </remarks>
internal sealed class DurableFunctionDeployment : IAsyncDisposable
{
    // The managed dotnet runtime this harness deploys against. Executable model:
    // `dotnet publish` emits a native `bootstrap` shim and the runtime execs it.
    // This is just the runtime CI exercises — durable execution is not tied to it.
    private const string ManagedRuntime = "dotnet10";
    private const string BootstrapHandler = "bootstrap";

    private static readonly RegionEndpoint DeploymentRegion = RegionEndpoint.USEast1;

    private readonly ITestOutputHelper _output;

    // Clients are shared (static) across all deployments. Each deployment used to construct its
    // own clients, which defeated adaptive retry: its congestion controller / rate limiter is
    // per-client, so N independent clients each believed they had capacity, all fired at once, and
    // collectively blew Lambda's account-wide control-plane limits ("capacity could not be obtained
    // ... insufficient capacity"). A single shared client per service lets adaptive retry actually
    // coordinate backoff across the parallel deployments.
    private static readonly IAmazonLambda _lambdaClient = new AmazonLambdaClient(BuildClientConfig<AmazonLambdaConfig>());
    private static readonly IAmazonIdentityManagementService _iamClient =
        new AmazonIdentityManagementServiceClient(BuildClientConfig<AmazonIdentityManagementServiceConfig>());

    // Lambda control-plane calls (CreateFunction/DeleteFunction/GetFunctionConfiguration) are
    // account-rate-limited and are the next bottleneck once IAM is no longer per-test. Cap how many
    // run concurrently across the whole suite so the parallel deployments don't collectively exceed
    // Lambda's limits; data-plane calls (Invoke, durable-execution reads) are not gated.
    private static readonly SemaphoreSlim LambdaControlPlaneGate = new(2, 2);

    private readonly string _functionName;
    private string? _roleArn;
    private string? _functionArn;
    private bool _functionCreated;

    // Optional paired "external system" Lambda — a plain (non-durable) function
    // that the workflow's submitter invokes. Models a real-world callback flow
    // where an out-of-band service resolves the durable execution.
    private readonly string _externalFunctionName;
    private string? _externalRoleArn;
    private bool _externalFunctionCreated;

    // A single IAM role shared by every test function in the suite. Creating and deleting a role
    // per deployment burst-throttled IAM ("Rate exceeded") once the suite started running in
    // parallel — IAM is global, single-bucketed, and throttles mutating calls aggressively. The
    // shared role is created at most once per account (reused across runs) and gated so concurrent
    // deployments don't race to create it. No test depends on a role *lacking* a permission, so a
    // single union-of-permissions role is safe; it is scoped to invoking durable-integ-* functions.
    private const string SharedRoleName = "durable-integ-shared-execution-role";
    private static readonly SemaphoreSlim SharedRoleGate = new(1, 1);
    private static string? _sharedRoleArn;

    // Publishing is done ONCE for all test functions, up front, instead of per-test. The test
    // functions all reference the same source projects (Amazon.Lambda.DurableExecution etc.);
    // publishing each function separately (and the old code wiped obj/bin first, forcing a cold
    // build every time) rebuilt those shared projects dozens of times, and doing it concurrently
    // thrashed MSBuild. A single up-front pass builds the shared projects once and the publishes
    // run incrementally; each test then just zips its already-published output.
    private static readonly SemaphoreSlim PrePublishGate = new(1, 1);
    private static bool _prePublished;

    public string FunctionName => _functionName;
    public string? ExternalFunctionName => _externalFunctionCreated ? _externalFunctionName : null;

    /// <summary>
    /// The fully-qualified function ARN (unqualified). Available after <see cref="CreateAsync"/>
    /// or <see cref="CreateWithDownstreamAsync"/> completes. Use <c>$"{FunctionArn}:$LATEST"</c>
    /// when constructing a qualified identifier for chained invocation.
    /// </summary>
    public string FunctionArn => _functionArn
        ?? throw new InvalidOperationException("Function ARN is not available until the function has been created.");

    public IAmazonLambda LambdaClient => _lambdaClient;

    /// <summary>
    /// The region all resources for this deployment are created in. Tests that build
    /// their own AWS clients (e.g. CloudWatch Logs) must use this so they target the
    /// same region the functions actually deploy to.
    /// </summary>
    public RegionEndpoint Region => DeploymentRegion;

    private DurableFunctionDeployment(ITestOutputHelper output, string suffix)
    {
        // xUnit buffers ITestOutputHelper and only flushes it when the test
        // completes, so a long deploy/poll shows no live progress. When
        // DURABLE_INTEG_TRACE is set, tee every line to an autoflushed file you
        // can `tail -f` (DURABLE_INTEG_TRACE=<path>, or "1"/"true" for a default
        // path under the temp dir). Off by default — no behavior change.
        _output = FileTracingTestOutputHelper.MaybeWrap(output);

        // Truncate the GUID (not the suffix) so CloudTrail entries stay readable.
        // Keep the GUID short enough that the total stays well under 40 chars even for long suffixes.
        static string ShortId() => Guid.NewGuid().ToString("N")[..Math.Min(8, 32)];
        _functionName = $"durable-integ-{suffix}-{ShortId()}";
        _externalFunctionName = $"durable-integ-{suffix}-ext-{ShortId()}";
    }

    /// <summary>
    /// Builds a client config tuned to survive throttling when the suite runs in parallel:
    /// adaptive retry (client-side rate limiting + backoff on throttle) and a generous retry count.
    /// </summary>
    private static TConfig BuildClientConfig<TConfig>() where TConfig : ClientConfig, new()
    {
        var config = new TConfig
        {
            RegionEndpoint = DeploymentRegion,
            RetryMode = RequestRetryMode.Adaptive,
            MaxErrorRetry = 10
        };
        return config;
    }

    // The optional `handler` defaults to `bootstrap` (executable model). Pass an
    // `Assembly::Type::Method` string to deploy the class-library model instead.
    public static async Task<DurableFunctionDeployment> CreateAsync(
        string testFunctionDir,
        string scenarioSuffix,
        ITestOutputHelper output,
        string? externalFunctionDir = null,
        IDictionary<string, string>? environment = null,
        IReadOnlyList<string>? invokeAllowedFunctionArns = null,
        bool enableTenancy = false,
        string? handler = null,
        int executionTimeoutSeconds = 60)
    {
        var deployment = new DurableFunctionDeployment(output, scenarioSuffix);
        try
        {
            await deployment.InitializeAsync(testFunctionDir, externalFunctionDir, environment, invokeAllowedFunctionArns, enableTenancy, handler, executionTimeoutSeconds);
        }
        catch
        {
            // Tear down anything that did get created (IAM role) so we
            // don't leak resources when init fails part-way through.
            await deployment.DisposeAsync();
            throw;
        }
        return deployment;
    }

    /// <summary>
    /// Two-step deployment for chained-invoke scenarios: deploys the downstream (callee)
    /// function first, captures its ARN, then deploys the parent (caller) with
    /// <c>DOWNSTREAM_FUNCTION_ARN</c> set in the parent's environment and the parent's
    /// role granted <c>lambda:InvokeFunction</c> on the downstream's ARN.
    /// </summary>
    /// <remarks>
    /// The parent and downstream are independent <see cref="DurableFunctionDeployment"/>
    /// instances; both are returned so the caller can dispose them in the right order
    /// (parent first, then downstream — the caller is the one in flight when the test ends).
    /// The <c>DOWNSTREAM_FUNCTION_ARN</c> env var carries a qualified identifier
    /// (<c>arn:...:function:name:$LATEST</c>) so the parent can pass it directly to
    /// <c>ctx.InvokeAsync(...)</c> without further manipulation.
    /// </remarks>
    public static async Task<(DurableFunctionDeployment Parent, DurableFunctionDeployment Downstream)>
        CreateWithDownstreamAsync(
            string parentTestFunctionDir,
            string downstreamTestFunctionDir,
            string scenarioSuffix,
            ITestOutputHelper output,
            IDictionary<string, string>? extraParentEnvironment = null,
            bool enableDownstreamTenancy = false)
    {
        // Deploy downstream first so we can pass its ARN to the parent's environment.
        var downstream = await CreateAsync(
            downstreamTestFunctionDir,
            scenarioSuffix + "-d",
            output,
            enableTenancy: enableDownstreamTenancy);

        DurableFunctionDeployment? parent = null;
        try
        {
            // Use a qualified identifier — the durable execution service rejects
            // unqualified ARNs. $LATEST is fine for integration tests; production
            // should use a version or alias.
            var qualifiedDownstreamArn = downstream.FunctionArn + ":$LATEST";
            var parentEnv = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOWNSTREAM_FUNCTION_ARN"] = qualifiedDownstreamArn,
            };
            if (extraParentEnvironment != null)
            {
                foreach (var kv in extraParentEnvironment)
                    parentEnv[kv.Key] = kv.Value;
            }

            parent = await CreateAsync(
                parentTestFunctionDir,
                scenarioSuffix + "-p",
                output,
                environment: parentEnv,
                invokeAllowedFunctionArns: new[] { downstream.FunctionArn });
        }
        catch
        {
            // Parent failed to deploy — tear down the downstream we already created
            // so we don't leak resources.
            await downstream.DisposeAsync();
            throw;
        }

        return (parent!, downstream);
    }

    private const string LambdaAssumeRolePolicy = """
    {
        "Version": "2012-10-17",
        "Statement": [{
            "Effect": "Allow",
            "Principal": {"Service": "lambda.amazonaws.com"},
            "Action": "sts:AssumeRole"
        }]
    }
    """;

    // Inline policy granting the permissions every durable-integ scenario needs: invoking any
    // durable-integ-* function (covers chained invoke and external-function invoke) and sending
    // durable-execution callbacks. Resource is scoped to the suite's function name prefix.
    private const string SharedInlinePolicyName = "DurableIntegSharedPermissions";
    private const string SharedInlinePolicyDocument = """
    {
        "Version": "2012-10-17",
        "Statement": [
            {
                "Effect": "Allow",
                "Action": "lambda:InvokeFunction",
                "Resource": [
                    "arn:aws:lambda:*:*:function:durable-integ-*",
                    "arn:aws:lambda:*:*:function:durable-integ-*:*"
                ]
            },
            {
                "Effect": "Allow",
                "Action": [
                    "lambda:SendDurableExecutionCallbackSuccess",
                    "lambda:SendDurableExecutionCallbackFailure"
                ],
                "Resource": "*"
            }
        ]
    }
    """;

    /// <summary>
    /// Returns the ARN of the shared execution role, creating it once per account if absent.
    /// Gated by a semaphore + memoized ARN so concurrent deployments don't race or re-create it.
    /// In steady state (role already exists from a prior run) this is a single GetRole call for the
    /// entire suite, which is what keeps the parallel run under IAM's mutating-call rate limits.
    /// </summary>
    private async Task<string> GetOrCreateSharedRoleAsync()
    {
        if (_sharedRoleArn != null)
            return _sharedRoleArn;

        await SharedRoleGate.WaitAsync();
        try
        {
            if (_sharedRoleArn != null)
                return _sharedRoleArn;

            try
            {
                var existing = await _iamClient.GetRoleAsync(new GetRoleRequest { RoleName = SharedRoleName });
                _output.WriteLine($"Reusing shared IAM role: {SharedRoleName}");
                _sharedRoleArn = existing.Role.Arn;
                return _sharedRoleArn;
            }
            catch (NoSuchEntityException)
            {
                // Falls through to create it.
            }

            _output.WriteLine($"Creating shared IAM role: {SharedRoleName}");
            var created = await _iamClient.CreateRoleAsync(new CreateRoleRequest
            {
                RoleName = SharedRoleName,
                AssumeRolePolicyDocument = LambdaAssumeRolePolicy
            });

            await _iamClient.AttachRolePolicyAsync(new AttachRolePolicyRequest
            {
                RoleName = SharedRoleName,
                PolicyArn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
            });
            await _iamClient.AttachRolePolicyAsync(new AttachRolePolicyRequest
            {
                RoleName = SharedRoleName,
                PolicyArn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicDurableExecutionRolePolicy"
            });
            await _iamClient.PutRolePolicyAsync(new PutRolePolicyRequest
            {
                RoleName = SharedRoleName,
                PolicyName = SharedInlinePolicyName,
                PolicyDocument = SharedInlinePolicyDocument
            });

            // Wait for IAM propagation so the first function create doesn't hit
            // "The role defined for the function cannot be assumed by Lambda".
            await Task.Delay(TimeSpan.FromSeconds(10));

            _sharedRoleArn = created.Role.Arn;
            return _sharedRoleArn;
        }
        finally
        {
            SharedRoleGate.Release();
        }
    }

    private async Task InitializeAsync(
        string testFunctionDir,
        string? externalFunctionDir,
        IDictionary<string, string>? environment,
        IReadOnlyList<string>? invokeAllowedFunctionArns,
        bool enableTenancy,
        string? handler,
        int executionTimeoutSeconds)
    {
        // 1. Acquire the shared IAM role (created once per account, reused across tests and runs).
        //    Both the workflow function and any paired external function run under this single role,
        //    which carries the union of permissions every scenario needs. The external function's
        //    callback-send permission and the workflow's invoke permission are all baked into the
        //    shared role, so per-test PutRolePolicy calls are no longer needed.
        _roleArn = await GetOrCreateSharedRoleAsync();
        if (externalFunctionDir != null)
        {
            _externalRoleArn = _roleArn;
        }

        // 2. Build + zip the workflow function package.
        _output.WriteLine($"Building and zipping function package from {testFunctionDir}...");
        var zipBytes = await BuildAndZipAsync(testFunctionDir);
        _output.WriteLine($"Package built: {zipBytes.Length} bytes");

        // 3. (optional) Build + deploy the external function. Done before the workflow
        //    Lambda so the workflow function's environment can reference the external
        //    function name (which is already known from the ctor).
        if (externalFunctionDir != null)
        {
            _output.WriteLine($"Building external function package from {externalFunctionDir}...");
            var extZipBytes = await BuildAndZipAsync(externalFunctionDir);

            _output.WriteLine($"Creating external Lambda function: {_externalFunctionName}");
            await RunControlPlaneAsync(() => _lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
            {
                FunctionName = _externalFunctionName,
                Runtime = ManagedRuntime,
                Handler = BootstrapHandler,
                Role = _externalRoleArn,
                Code = new FunctionCode { ZipFile = new MemoryStream(extZipBytes) },
                Timeout = 30,
                MemorySize = 256,
                LoggingConfig = new LoggingConfig { LogFormat = LogFormat.JSON }
                // No DurableConfig — this is a plain function.
            }));
            _externalFunctionCreated = true;

            _output.WriteLine("Waiting for external function to become Active...");
            await WaitForFunctionActive(_externalFunctionName);
        }

        // 4. Create the workflow Lambda.
        _output.WriteLine($"Creating Lambda function: {_functionName}");
        var createFunctionRequest = new CreateFunctionRequest
        {
            FunctionName = _functionName,
            Runtime = ManagedRuntime,
            // Defaults to the executable model (bootstrap); a non-null handler deploys
            // the class-library model via an Assembly::Type::Method string.
            Handler = handler ?? BootstrapHandler,
            Role = _roleArn,
            Code = new FunctionCode { ZipFile = new MemoryStream(zipBytes) },
            Timeout = 30,
            MemorySize = 256,
            DurableConfig = new DurableConfig { ExecutionTimeout = executionTimeoutSeconds },
            // Emit structured JSON logs so tests that parse log records (e.g.
            // ReplayAwareLoggerTest) can assert on durable-execution scope keys.
            LoggingConfig = new LoggingConfig { LogFormat = LogFormat.JSON }
        };

        // Tenant isolation must be set at function-creation time (Lambda rejects
        // post-create modification). Without it, the durable execution service
        // refuses chained invokes that carry a TenantId — so the tenant-routing
        // integration test needs the *callee* deployed with PER_TENANT.
        if (enableTenancy)
        {
            createFunctionRequest.TenancyConfig = new TenancyConfig
            {
                TenantIsolationMode = TenantIsolationMode.PER_TENANT
            };
        }

        // Build the function's environment: start with the caller-supplied vars, then
        // tack on EXTERNAL_FUNCTION_NAME if a paired external function exists.
        var envVars = new Dictionary<string, string>(StringComparer.Ordinal);
        if (environment != null)
        {
            foreach (var kv in environment)
                envVars[kv.Key] = kv.Value;
        }
        if (externalFunctionDir != null)
        {
            envVars["EXTERNAL_FUNCTION_NAME"] = _externalFunctionName;
        }
        if (envVars.Count > 0)
        {
            createFunctionRequest.Environment = new Amazon.Lambda.Model.Environment
            {
                Variables = envVars
            };
        }

        var createFunctionResponse = await RunControlPlaneAsync(() => _lambdaClient.CreateFunctionAsync(createFunctionRequest));
        _functionCreated = true;
        _functionArn = createFunctionResponse.FunctionArn;

        _output.WriteLine($"Waiting for function to become Active... (ARN: {_functionArn})");
        await WaitForFunctionActive(_functionName);
    }

    public async Task<(InvokeResponse Response, string ExecutionName)> InvokeAsync(string payload, string? executionName = null)
    {
        var name = executionName ?? $"integ-test-{Guid.NewGuid():N}";
        var response = await _lambdaClient.InvokeAsync(new InvokeRequest
        {
            FunctionName = _functionName,
            Qualifier = "$LATEST",
            Payload = payload,
            DurableExecutionName = name
        });
        return (response, name);
    }

    /// <summary>
    /// Polls ListDurableExecutionsByFunction until an execution with the given name appears.
    /// Useful when the synchronous Invoke response gives no ARN (e.g., failed workflows return null).
    /// </summary>
    public async Task<string?> FindDurableExecutionArnByNameAsync(string executionName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var attempt = 0;
        _output.WriteLine($"[FindArn] Starting search for execution name '{executionName}' on function '{_functionName}' (timeout: {timeout.TotalSeconds}s)");

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            try
            {
                var resp = await _lambdaClient.ListDurableExecutionsByFunctionAsync(
                    new ListDurableExecutionsByFunctionRequest
                    {
                        FunctionName = _functionName,
                        DurableExecutionName = executionName  // server-side exact match
                    });

                var count = resp.DurableExecutions?.Count ?? 0;
                _output.WriteLine($"[FindArn] attempt {attempt}: List returned {count} executions");

                if (count > 0)
                {
                    foreach (var e in resp.DurableExecutions!)
                    {
                        _output.WriteLine($"[FindArn]   - name='{e.DurableExecutionName}' status={e.Status} arn={e.DurableExecutionArn}");
                    }
                    var match = resp.DurableExecutions.FirstOrDefault(e => e.DurableExecutionName == executionName);
                    if (match != null)
                    {
                        _output.WriteLine($"[FindArn] matched on attempt {attempt}");
                        return match.DurableExecutionArn;
                    }
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[FindArn] attempt {attempt} error (will retry): {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        _output.WriteLine($"[FindArn] gave up after {attempt} attempts ({timeout.TotalSeconds}s)");
        return null;
    }

    public async Task<string> PollForCompletionAsync(string durableExecutionArn, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = await _lambdaClient.GetDurableExecutionAsync(
                    new GetDurableExecutionRequest { DurableExecutionArn = durableExecutionArn });

                var status = resp.Status?.ToString();
                if (status == "SUCCEEDED" || status == "FAILED" ||
                    status == "TIMED_OUT" || status == "STOPPED")
                {
                    return status;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Poll error (will retry): {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return "TIMEOUT";
    }

    public async Task<GetDurableExecutionResponse> GetExecutionAsync(string durableExecutionArn)
        => await _lambdaClient.GetDurableExecutionAsync(
            new GetDurableExecutionRequest { DurableExecutionArn = durableExecutionArn });

    public async Task<GetDurableExecutionHistoryResponse> GetHistoryAsync(string durableExecutionArn, bool includeExecutionData = true)
        => await _lambdaClient.GetDurableExecutionHistoryAsync(
            new GetDurableExecutionHistoryRequest
            {
                DurableExecutionArn = durableExecutionArn,
                IncludeExecutionData = includeExecutionData
            });

    /// <summary>
    /// Repeatedly fetches history until <paramref name="predicate"/> is satisfied or the
    /// timeout elapses. Needed because the history endpoint is eventually consistent —
    /// the execution status can flip to SUCCEEDED before all events are indexed.
    /// </summary>
    public async Task<GetDurableExecutionHistoryResponse> WaitForHistoryAsync(
        string durableExecutionArn,
        Func<GetDurableExecutionHistoryResponse, bool> predicate,
        TimeSpan timeout,
        bool includeExecutionData = true)
    {
        var deadline = DateTime.UtcNow + timeout;
        GetDurableExecutionHistoryResponse? last = null;
        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            try
            {
                last = await GetHistoryAsync(durableExecutionArn, includeExecutionData);
                var eventCount = last.Events?.Count ?? 0;
                var typeCounts = last.Events?
                    .GroupBy(e => e.EventType?.Value ?? "<null>")
                    .Select(g => $"{g.Key}:{g.Count()}")
                    .OrderBy(s => s);
                _output.WriteLine($"[WaitForHistory] attempt {attempt}: {eventCount} events [{string.Join(",", typeCounts ?? Enumerable.Empty<string>())}]");
                if (predicate(last))
                {
                    DumpEvents(last);
                    return last;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[WaitForHistory] attempt {attempt} error (will retry): {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        _output.WriteLine($"[WaitForHistory] gave up after {attempt} attempts; returning last response with {last?.Events?.Count ?? 0} events");
        if (last != null) DumpEvents(last);
        return last ?? throw new TimeoutException($"GetDurableExecutionHistory never succeeded within {timeout.TotalSeconds}s");
    }

    private void DumpEvents(GetDurableExecutionHistoryResponse history)
    {
        var events = history.Events ?? new List<Event>();
        _output.WriteLine($"[WaitForHistory] event dump ({events.Count} total):");
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            _output.WriteLine($"  [{i}] type={e.EventType?.Value ?? "<null>"} name={e.Name ?? "<null>"} ts={e.EventTimestamp:O}");
        }
    }

    public string? ExtractDurableExecutionArn(string responsePayload)
    {
        try
        {
            var doc = JsonDocument.Parse(responsePayload);
            if (doc.RootElement.TryGetProperty("durableExecutionArn", out var arnProp))
                return arnProp.GetString();
        }
        catch { }
        return null;
    }

    private async Task WaitForFunctionActive(string functionName)
    {
        for (int i = 0; i < 40; i++)
        {
            try
            {
                // Gate each poll call: GetFunctionConfiguration is control-plane and rate-limited,
                // and all parallel deployments poll at once.
                var config = await RunControlPlaneAsync(() => _lambdaClient.GetFunctionConfigurationAsync(
                    new GetFunctionConfigurationRequest { FunctionName = functionName }));
                if (config.State == State.Active) return;
                if (config.State == State.Failed)
                    throw new Exception($"Function '{functionName}' creation failed: {config.StateReasonCode} - {config.StateReason}");
            }
            catch (ResourceNotFoundException) { }
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        throw new TimeoutException($"Function '{functionName}' did not become Active within 120 seconds");
    }

    /// <summary>
    /// Runs a Lambda control-plane operation under <see cref="LambdaControlPlaneGate"/> so the
    /// suite's parallel deployments don't collectively exceed Lambda's account-wide
    /// control-plane request rate. Adaptive retry on the shared client handles brief throttles;
    /// this gate keeps the offered load low enough that retry doesn't exhaust its capacity.
    /// </summary>
    private static async Task<T> RunControlPlaneAsync<T>(Func<Task<T>> operation)
    {
        await LambdaControlPlaneGate.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            LambdaControlPlaneGate.Release();
        }
    }

    /// <summary>
    /// Returns the zipped, published package for a test function. The actual publishing happens
    /// once for all functions (see <see cref="EnsureAllFunctionsPublishedAsync"/>); this just zips
    /// the already-published output. The zip contains the native <c>bootstrap</c> shim that the
    /// dotnet managed runtime execs (executable model).
    /// </summary>
    private async Task<byte[]> BuildAndZipAsync(string testFunctionDir)
    {
        await EnsureAllFunctionsPublishedAsync();

        var publishDir = Path.Combine(testFunctionDir, "bin", "publish");
        if (!Directory.Exists(publishDir))
            throw new DirectoryNotFoundException($"Expected published output at '{publishDir}' but it does not exist.");

        // Zip the publish output to a UNIQUE temp path. A given function (e.g. ApproverFunction) is
        // the external function for more than one test, so multiple parallel tests zip the same
        // published output at once — writing to a shared bin/function.zip raced ("file is being used
        // by another process"). The publish output itself is read-only and shared safely; only the
        // zip destination needs to be per-call. On Linux (CI) ZipFile preserves the bootstrap exec
        // bit; on Windows the managed runtime tolerates the missing bit.
        var zipPath = Path.Combine(Path.GetTempPath(), $"durable-integ-fn-{Guid.NewGuid():N}.zip");
        try
        {
            ZipFile.CreateFromDirectory(publishDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return await File.ReadAllBytesAsync(zipPath);
        }
        finally
        {
            try { File.Delete(zipPath); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Publishes every test function once, up front, in a SINGLE MSBuild invocation. Runs at most
    /// once per test run (gated + memoized). A generated traversal project references all function
    /// projects and publishes them with one <c>dotnet build</c>, so MSBuild builds the shared
    /// dependency projects once and publishes the functions in parallel within that one process —
    /// avoiding both the per-project CLI/MSBuild startup cost of N separate <c>dotnet publish</c>
    /// calls and the cross-process thrash that those caused when the suite ran in parallel. Each
    /// function still lands in its own <c>bin/publish</c>; tests then only zip that output.
    /// </summary>
    private async Task EnsureAllFunctionsPublishedAsync()
    {
        if (_prePublished)
            return;

        await PrePublishGate.WaitAsync();
        try
        {
            if (_prePublished)
                return;

            var testFunctionsRoot = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestFunctions"));
            var projects = Directory.GetFiles(testFunctionsRoot, "*.csproj", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            _output.WriteLine($"Pre-publishing {projects.Count} test function(s) in a single MSBuild pass...");

            // Generate a traversal project that publishes every function project to its own
            // bin/publish (PublishDir relative to each project). BuildInParallel lets MSBuild fan
            // the publishes out across nodes once the shared dependency projects are built.
            var itemsXml = string.Concat(projects.Select(p =>
                $"    <FunctionProject Include=\"{System.Security.SecurityElement.Escape(p)}\" />\n"));
            var traversalProject = $"""
                <Project>
                  <ItemGroup>
                {itemsXml}  </ItemGroup>
                  <Target Name="PublishAll">
                    <!-- Restore in a SEPARATE, NON-parallel pass first. Restore is not
                         parallel-safe: the function projects share src ProjectReferences
                         (e.g. Amazon.Lambda.Serialization.SystemTextJson), and restoring them
                         concurrently races on the shared obj/project.assets.json ("file already
                         exists"). One sequential restore pass writes each shared project's assets
                         once. (An outer -restore would only restore this traversal project, not the
                         referenced functions, so it must be done here.) -->
                    <MSBuild
                      Projects="@(FunctionProject)"
                      Targets="Restore"
                      BuildInParallel="false"
                      Properties="Configuration=Release;RuntimeIdentifier=linux-x64;SelfContained=false" />
                    <!-- Then publish in parallel; restore is already done so no shared-output race. -->
                    <MSBuild
                      Projects="@(FunctionProject)"
                      Targets="Publish"
                      BuildInParallel="true"
                      Properties="Configuration=Release;RuntimeIdentifier=linux-x64;SelfContained=false;PublishDir=bin\publish\" />
                  </Target>
                </Project>
                """;

            var traversalPath = Path.Combine(Path.GetTempPath(), $"durable-integ-publish-all-{Guid.NewGuid():N}.proj");
            await File.WriteAllTextAsync(traversalPath, traversalProject);
            try
            {
                // -maxcpucount lets MSBuild use multiple nodes for the parallel publishes.
                await RunProcess("dotnet",
                    $"build \"{traversalPath}\" -t:PublishAll -maxcpucount",
                    testFunctionsRoot);
            }
            finally
            {
                try { File.Delete(traversalPath); } catch { /* best effort */ }
            }

            _prePublished = true;
        }
        finally
        {
            PrePublishGate.Release();
        }
    }

    private async Task RunProcess(string fileName, string arguments, string workingDir, string? stdin = null)
    {
        _output.WriteLine($"Running: {fileName} {arguments}");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin != null,
            UseShellExecute = false
        };

        var process = System.Diagnostics.Process.Start(psi)!;

        if (stdin != null)
        {
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAny(
            process.WaitForExitAsync(),
            Task.Delay(TimeSpan.FromMinutes(5)));

        if (!process.HasExited)
        {
            process.Kill();
            throw new TimeoutException($"{fileName} timed out after 5 minutes");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            // Dump the FULL streams on failure — diagnosing build errors with
            // truncated output is painful, and these only fire on test failure.
            _output.WriteLine($"stdout: {stdout}");
            _output.WriteLine($"stderr: {stderr}");
            var detail = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
            throw new Exception($"{fileName} failed (exit {process.ExitCode}): {detail}");
        }

        if (!string.IsNullOrWhiteSpace(stdout))
            _output.WriteLine($"stdout: {stdout[..Math.Min(stdout.Length, 1000)]}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_functionCreated)
        {
            try
            {
                _output.WriteLine($"Deleting function: {_functionName}");
                await RunControlPlaneAsync(() => _lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = _functionName }));
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (function): {ex.Message}"); }
        }

        if (_externalFunctionCreated)
        {
            try
            {
                _output.WriteLine($"Deleting external function: {_externalFunctionName}");
                await RunControlPlaneAsync(() => _lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = _externalFunctionName }));
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (external function): {ex.Message}"); }
        }

        // The shared IAM role is intentionally NOT deleted here — it is reused by every test and
        // across runs. Deleting/recreating it per test is exactly what burst-throttled IAM. It is a
        // single stable role (durable-integ-shared-execution-role) that the test account retains.
    }

    public static string FindTestFunctionDir(string functionDirName)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "TestFunctions", functionDirName);
            if (Directory.Exists(candidate))
                return candidate;

            // Also check legacy "TestFunction" location for backwards compat
            var legacy = Path.Combine(dir, functionDirName);
            if (Directory.Exists(legacy) && File.Exists(Path.Combine(legacy, $"{functionDirName}.csproj")))
                return legacy;

            dir = Path.GetDirectoryName(dir);
        }

        // Fallback: relative from test source directory
        var fallback = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestFunctions", functionDirName));
        if (Directory.Exists(fallback))
            return fallback;

        throw new DirectoryNotFoundException(
            $"Could not find TestFunctions/{functionDirName}/ directory. Looked up from: {AppContext.BaseDirectory}");
    }
}
