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
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// Builds, deploys, and invokes a single durable Lambda function for an integration test.
/// Manages the full lifecycle: IAM role, zip package, managed-runtime Lambda function.
/// All resources are torn down on DisposeAsync.
/// </summary>
/// <remarks>
/// Durable functions deploy as a plain zip package on the managed <c>dotnet10</c> runtime
/// (executable model, <c>Handler=bootstrap</c>) — no container image or ECR repository
/// required. Each test function is published framework-dependent for linux-x64 and zipped.
/// </remarks>
internal sealed class DurableFunctionDeployment : IAsyncDisposable
{
    // The managed dotnet runtime that supports durable configuration. Executable model:
    // `dotnet publish` emits a native `bootstrap` shim and the runtime execs it.
    private const string ManagedRuntime = "dotnet10";
    private const string BootstrapHandler = "bootstrap";

    private static readonly RegionEndpoint DeploymentRegion = RegionEndpoint.USEast1;

    private readonly ITestOutputHelper _output;
    private readonly IAmazonLambda _lambdaClient;
    private readonly IAmazonIdentityManagementService _iamClient;

    private readonly string _functionName;
    private readonly string _roleName;
    private string? _roleArn;
    private string? _functionArn;
    private bool _functionCreated;
    private readonly List<string> _inlinePolicyNames = new();

    // Optional paired "external system" Lambda — a plain (non-durable) function
    // that the workflow's submitter invokes. Models a real-world callback flow
    // where an out-of-band service resolves the durable execution.
    private readonly string _externalFunctionName;
    private readonly string _externalRoleName;
    private string? _externalRoleArn;
    private bool _externalFunctionCreated;

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
        _lambdaClient = new AmazonLambdaClient(DeploymentRegion);
        _iamClient = new AmazonIdentityManagementServiceClient(DeploymentRegion);

        // Truncate the GUID (not the suffix) so CloudTrail entries stay readable.
        // Keep the GUID short enough that the total stays well under 40 chars even for long suffixes.
        static string ShortId() => Guid.NewGuid().ToString("N")[..Math.Min(8, 32)];
        _functionName = $"durable-integ-{suffix}-{ShortId()}";
        _roleName = $"durable-integ-{suffix}-{ShortId()}";
        _externalFunctionName = $"durable-integ-{suffix}-ext-{ShortId()}";
        _externalRoleName = $"durable-integ-{suffix}-ext-{ShortId()}";
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

    private async Task InitializeAsync(
        string testFunctionDir,
        string? externalFunctionDir,
        IDictionary<string, string>? environment,
        IReadOnlyList<string>? invokeAllowedFunctionArns,
        bool enableTenancy,
        string? handler,
        int executionTimeoutSeconds)
    {
        // 1. Create the workflow's IAM role.
        _output.WriteLine($"Creating IAM role: {_roleName}");
        var createRoleResponse = await _iamClient.CreateRoleAsync(new CreateRoleRequest
        {
            RoleName = _roleName,
            AssumeRolePolicyDocument = LambdaAssumeRolePolicy
        });
        _roleArn = createRoleResponse.Role.Arn;

        await _iamClient.AttachRolePolicyAsync(new AttachRolePolicyRequest
        {
            RoleName = _roleName,
            PolicyArn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
        });

        await _iamClient.AttachRolePolicyAsync(new AttachRolePolicyRequest
        {
            RoleName = _roleName,
            PolicyArn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicDurableExecutionRolePolicy"
        });

        // 2. (optional) Create the external function's IAM role up front so its
        //    sts:AssumeRole and lambda:SendDurableExecutionCallbackSuccess
        //    permissions propagate alongside the workflow role's permissions
        //    (single 10-second sleep covers both).
        if (externalFunctionDir != null)
        {
            _output.WriteLine($"Creating external IAM role: {_externalRoleName}");
            var extRoleResponse = await _iamClient.CreateRoleAsync(new CreateRoleRequest
            {
                RoleName = _externalRoleName,
                AssumeRolePolicyDocument = LambdaAssumeRolePolicy
            });
            _externalRoleArn = extRoleResponse.Role.Arn;

            await _iamClient.AttachRolePolicyAsync(new AttachRolePolicyRequest
            {
                RoleName = _externalRoleName,
                PolicyArn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
            });

            // Inline policy lets the external function call the durable callback API.
            // Resource "*" because we don't yet know the workflow's ARN at this point —
            // the external function only resolves callbacks belonging to executions the
            // workflow created, so the blast radius is bounded by the role's lifetime.
            await _iamClient.PutRolePolicyAsync(new PutRolePolicyRequest
            {
                RoleName = _externalRoleName,
                PolicyName = "SendDurableExecutionCallback",
                PolicyDocument = """
                {
                    "Version": "2012-10-17",
                    "Statement": [{
                        "Effect": "Allow",
                        "Action": [
                            "lambda:SendDurableExecutionCallbackSuccess",
                            "lambda:SendDurableExecutionCallbackFailure"
                        ],
                        "Resource": "*"
                    }]
                }
                """
            });

            // Workflow function will Invoke the external function — grant via inline policy.
            // Scoped to the external function name we just minted.
            await _iamClient.PutRolePolicyAsync(new PutRolePolicyRequest
            {
                RoleName = _roleName,
                PolicyName = "InvokeExternalFunction",
                PolicyDocument = $$"""
                {
                    "Version": "2012-10-17",
                    "Statement": [{
                        "Effect": "Allow",
                        "Action": "lambda:InvokeFunction",
                        "Resource": "arn:aws:lambda:*:*:function:{{_externalFunctionName}}"
                    }]
                }
                """
            });
            _inlinePolicyNames.Add("InvokeExternalFunction");
        }

        // Grant cross-Lambda invoke when the parent of a chained-invoke scenario
        // needs to call out to a downstream function. The durable execution service
        // is the one that actually drives the chained invocation in production —
        // attaching this directly to the parent's role keeps the parent role
        // capable of being used in non-durable contexts (e.g. for diagnostic
        // direct invokes from the test harness).
        if (invokeAllowedFunctionArns != null && invokeAllowedFunctionArns.Count > 0)
        {
            // Allow both the unqualified ARN and any qualifier (alias/version/$LATEST).
            var resources = new List<string>(invokeAllowedFunctionArns.Count * 2);
            foreach (var arn in invokeAllowedFunctionArns)
            {
                resources.Add(arn);
                resources.Add(arn + ":*");
            }
            var resourceJson = "[" + string.Join(",", resources.Select(r => $"\"{r}\"")) + "]";
            var policyDoc = $$"""
            {
                "Version": "2012-10-17",
                "Statement": [{
                    "Effect": "Allow",
                    "Action": ["lambda:InvokeFunction"],
                    "Resource": {{resourceJson}}
                }]
            }
            """;
            const string PolicyName = "AllowChainedInvoke";
            await _iamClient.PutRolePolicyAsync(new PutRolePolicyRequest
            {
                RoleName = _roleName,
                PolicyName = PolicyName,
                PolicyDocument = policyDoc
            });
            _inlinePolicyNames.Add(PolicyName);
        }

        // Wait for IAM propagation.
        await Task.Delay(TimeSpan.FromSeconds(10));

        // 3. Build + zip the workflow function package.
        _output.WriteLine($"Building and zipping function package from {testFunctionDir}...");
        var zipBytes = await BuildAndZipAsync(testFunctionDir);
        _output.WriteLine($"Package built: {zipBytes.Length} bytes");

        // 4. (optional) Build + deploy the external function. Done before the workflow
        //    Lambda so the workflow function's environment can reference the external
        //    function name (which is already known from the ctor).
        if (externalFunctionDir != null)
        {
            _output.WriteLine($"Building external function package from {externalFunctionDir}...");
            var extZipBytes = await BuildAndZipAsync(externalFunctionDir);

            _output.WriteLine($"Creating external Lambda function: {_externalFunctionName}");
            await _lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
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
            });
            _externalFunctionCreated = true;

            _output.WriteLine("Waiting for external function to become Active...");
            await WaitForFunctionActive(_externalFunctionName);
        }

        // 5. Create the workflow Lambda.
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

        var createFunctionResponse = await _lambdaClient.CreateFunctionAsync(createFunctionRequest);
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
        for (int i = 0; i < 60; i++)
        {
            try
            {
                var config = await _lambdaClient.GetFunctionConfigurationAsync(
                    new GetFunctionConfigurationRequest { FunctionName = functionName });
                if (config.State == State.Active) return;
                if (config.State == State.Failed)
                    throw new Exception($"Function '{functionName}' creation failed: {config.StateReasonCode} - {config.StateReason}");
            }
            catch (ResourceNotFoundException) { }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        throw new TimeoutException($"Function '{functionName}' did not become Active within 120 seconds");
    }

    /// <summary>
    /// Publishes a test function (framework-dependent, linux-x64) and zips the publish
    /// output for upload as a managed-runtime Lambda package. The zip contains the native
    /// <c>bootstrap</c> shim that the dotnet managed runtime execs (executable model).
    /// </summary>
    private async Task<byte[]> BuildAndZipAsync(string testFunctionDir)
    {
        // `dotnet test` spins up one testhost per TargetFramework (net8.0 + net10.0) and
        // runs them concurrently. Both testhosts invoke the same test classes, which means
        // two processes can race on the same TestFunctions/<X>/ source dir — wiping bin/
        // and obj/ under each other's feet. Symptom: MSB3030 "Could not copy bootstrap.dll"
        // because one process deleted obj/ while the other was mid-publish. Serialize the
        // per-source-dir build with a cross-process file lock so different test functions
        // can still build in parallel. (A Mutex would have thread-affinity issues across
        // awaits; an exclusive FileStream avoids that.) Lock file goes under temp — keeping
        // it out of the source tree avoids polluting git status across worktrees.
        var lockKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(testFunctionDir.ToLowerInvariant())))[..16];
        var lockPath = Path.Combine(Path.GetTempPath(), $"durable-integ-build-{lockKey}.lock");
        using var lockHandle = await AcquireExclusiveFileLockAsync(lockPath, TimeSpan.FromMinutes(10));

        var publishDir = Path.Combine(testFunctionDir, "bin", "publish");
        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, true);

        // MSBuild's up-to-date check leaves stale .Up2Date markers under obj/ that
        // make `dotnet publish` skip the copy-to-output step on a second run after
        // we've wiped bin/publish/. Result: empty publish dir → empty zip package.
        // Nuking obj/ guarantees a real publish each time the helper is invoked.
        // Cheap (each test function is small).
        var objDir = Path.Combine(testFunctionDir, "obj");
        if (Directory.Exists(objDir)) Directory.Delete(objDir, true);
        var binDir = Path.Combine(testFunctionDir, "bin");
        if (Directory.Exists(binDir)) Directory.Delete(binDir, true);

        await RunProcess("dotnet",
            $"publish -c Release -r linux-x64 --self-contained false -o \"{publishDir}\"",
            testFunctionDir);

        // Zip the publish output. On Linux (CI) ZipFile preserves the bootstrap exec bit;
        // on Windows the managed runtime tolerates the missing bit.
        var zipPath = Path.Combine(testFunctionDir, "bin", "function.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(publishDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

        return await File.ReadAllBytesAsync(zipPath);
    }

    private static async Task<FileStream> AcquireExclusiveFileLockAsync(string lockPath, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException($"Timed out waiting for build lock '{lockPath}' after {timeout.TotalSeconds:F0}s");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
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
                await _lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = _functionName });
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (function): {ex.Message}"); }
        }

        if (_externalFunctionCreated)
        {
            try
            {
                _output.WriteLine($"Deleting external function: {_externalFunctionName}");
                await _lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = _externalFunctionName });
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (external function): {ex.Message}"); }
        }

        if (_roleArn != null)
        {
            // Detach each policy independently — if one detach fails (e.g., the
            // policy was never attached because init bailed out early) we still
            // want to attempt the others and the final DeleteRole.
            await TryDetachManaged(_roleName, "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole");
            await TryDetachManaged(_roleName, "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicDurableExecutionRolePolicy");

            // Inline policies must be deleted (not detached) before DeleteRole succeeds.
            foreach (var inline in _inlinePolicyNames)
            {
                await TryDeleteInline(_roleName, inline);
            }

            try
            {
                await _iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = _roleName });
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (IAM DeleteRole): {ex.Message}"); }
        }

        if (_externalRoleArn != null)
        {
            await TryDetachManaged(_externalRoleName, "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole");
            await TryDeleteInline(_externalRoleName, "SendDurableExecutionCallback");
            try
            {
                await _iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = _externalRoleName });
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (IAM DeleteRole external): {ex.Message}"); }
        }

        async Task TryDetachManaged(string roleName, string policyArn)
        {
            try
            {
                await _iamClient.DetachRolePolicyAsync(new DetachRolePolicyRequest
                {
                    RoleName = roleName,
                    PolicyArn = policyArn
                });
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (IAM Detach {policyArn}): {ex.Message}"); }
        }

        async Task TryDeleteInline(string roleName, string policyName)
        {
            try
            {
                await _iamClient.DeleteRolePolicyAsync(new DeleteRolePolicyRequest
                {
                    RoleName = roleName,
                    PolicyName = policyName
                });
            }
            catch (NoSuchEntityException) { /* policy was never attached — fine */ }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (IAM DeleteInline {policyName}): {ex.Message}"); }
        }
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
