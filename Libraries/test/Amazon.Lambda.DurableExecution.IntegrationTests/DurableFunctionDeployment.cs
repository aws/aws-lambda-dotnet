using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// Builds, deploys, and invokes a single durable Lambda function for an integration test.
/// Manages the full lifecycle: IAM role, ECR repo, Docker image, Lambda function.
/// All resources are torn down on DisposeAsync.
/// </summary>
internal sealed class DurableFunctionDeployment : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IAmazonLambda _lambdaClient;
    private readonly IAmazonECR _ecrClient;
    private readonly IAmazonIdentityManagementService _iamClient;

    private readonly string _functionName;
    private readonly string _repoName;
    private readonly string _roleName;
    private string? _roleArn;
    private string? _imageUri;
    private bool _functionCreated;
    private bool _ecrRepoCreated;

    public string FunctionName => _functionName;
    public IAmazonLambda LambdaClient => _lambdaClient;

    private DurableFunctionDeployment(ITestOutputHelper output, string suffix)
    {
        _output = output;
        _lambdaClient = new AmazonLambdaClient(RegionEndpoint.USEast1);
        _ecrClient = new AmazonECRClient(RegionEndpoint.USEast1);
        _iamClient = new AmazonIdentityManagementServiceClient(RegionEndpoint.USEast1);

        // Truncate the GUID (not the suffix) so CloudTrail entries stay readable.
        // Keep the GUID short enough that the total stays well under 40 chars even for long suffixes.
        static string ShortId() => Guid.NewGuid().ToString("N")[..Math.Min(8, 32)];
        _functionName = $"durable-integ-{suffix}-{ShortId()}";
        _repoName = $"durable-integ-{suffix}-{ShortId()}";
        _roleName = $"durable-integ-{suffix}-{ShortId()}";
    }

    public static async Task<DurableFunctionDeployment> CreateAsync(
        string testFunctionDir,
        string scenarioSuffix,
        ITestOutputHelper output,
        bool useDockerPublish = false)
    {
        var deployment = new DurableFunctionDeployment(output, scenarioSuffix);
        try
        {
            await deployment.InitializeAsync(testFunctionDir, useDockerPublish);
        }
        catch
        {
            // Tear down anything that did get created (IAM role, ECR repo) so we
            // don't leak resources when init fails part-way through.
            await deployment.DisposeAsync();
            throw;
        }
        return deployment;
    }

    private async Task InitializeAsync(string testFunctionDir, bool useDockerPublish)
    {
        // 1. Create IAM role
        _output.WriteLine($"Creating IAM role: {_roleName}");
        var assumeRolePolicy = """
        {
            "Version": "2012-10-17",
            "Statement": [{
                "Effect": "Allow",
                "Principal": {"Service": "lambda.amazonaws.com"},
                "Action": "sts:AssumeRole"
            }]
        }
        """;

        var createRoleResponse = await _iamClient.CreateRoleAsync(new CreateRoleRequest
        {
            RoleName = _roleName,
            AssumeRolePolicyDocument = assumeRolePolicy
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

        // Wait for IAM propagation
        await Task.Delay(TimeSpan.FromSeconds(10));

        // 2. Create ECR repository
        _output.WriteLine($"Creating ECR repository: {_repoName}");
        var createRepoResponse = await _ecrClient.CreateRepositoryAsync(new CreateRepositoryRequest
        {
            RepositoryName = _repoName
        });
        _ecrRepoCreated = true;
        var repositoryUri = createRepoResponse.Repository.RepositoryUri;

        // 3. Build and push Docker image
        _output.WriteLine($"Building and pushing Docker image from {testFunctionDir}...");
        _imageUri = await BuildAndPushImage(testFunctionDir, repositoryUri, useDockerPublish);
        _output.WriteLine($"Image pushed: {_imageUri}");

        // 4. Create Lambda function
        _output.WriteLine($"Creating Lambda function: {_functionName}");
        await _lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
        {
            FunctionName = _functionName,
            PackageType = PackageType.Image,
            Role = _roleArn,
            Code = new FunctionCode { ImageUri = _imageUri },
            Timeout = 30,
            MemorySize = 256,
            DurableConfig = new DurableConfig { ExecutionTimeout = 60 }
        });
        _functionCreated = true;

        _output.WriteLine("Waiting for function to become Active...");
        await WaitForFunctionActive();
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
                _output.WriteLine($"[WaitForHistory] attempt {attempt}: {eventCount} events");
                if (predicate(last)) return last;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[WaitForHistory] attempt {attempt} error (will retry): {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        _output.WriteLine($"[WaitForHistory] gave up after {attempt} attempts; returning last response with {last?.Events?.Count ?? 0} events");
        return last ?? throw new TimeoutException($"GetDurableExecutionHistory never succeeded within {timeout.TotalSeconds}s");
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

    private async Task WaitForFunctionActive()
    {
        for (int i = 0; i < 60; i++)
        {
            try
            {
                var config = await _lambdaClient.GetFunctionConfigurationAsync(
                    new GetFunctionConfigurationRequest { FunctionName = _functionName });
                if (config.State == State.Active) return;
                if (config.State == State.Failed)
                    throw new Exception($"Function creation failed: {config.StateReasonCode} - {config.StateReason}");
            }
            catch (ResourceNotFoundException) { }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        throw new TimeoutException("Function did not become Active within 120 seconds");
    }

    private async Task<string> BuildAndPushImage(string testFunctionDir, string repositoryUri, bool useDockerPublish)
    {
        var imageTag = $"{repositoryUri}:latest";

        // Two flavors of `docker build`:
        //   - Host-publish (default, JIT functions): `dotnet publish` runs on the host and
        //     writes to bin/publish/, which the Dockerfile COPYs in. Build context = function dir.
        //   - Docker-publish (NativeAOT): the host can't cross-compile AOT for linux-x64
        //     reliably (needs clang/zlib), so `dotnet publish` runs *inside* the image.
        //     The Dockerfile's project references reach back to Libraries\src\... and
        //     buildtools\common.props, so we stage those into a temp directory and use it
        //     as the build context.
        string buildContextDir;
        string dockerfilePath;
        string? stagingDir = null;

        if (useDockerPublish)
        {
            stagingDir = StageBuildContextForDockerPublish(testFunctionDir);
            buildContextDir = stagingDir;

            // The function's project lives at the same relative path inside the staging dir.
            var repoRoot = FindRepoRoot(testFunctionDir);
            var relFunctionDir = Path.GetRelativePath(repoRoot, testFunctionDir);
            dockerfilePath = Path.Combine(stagingDir, relFunctionDir, "Dockerfile");
        }
        else
        {
            var publishDir = Path.Combine(testFunctionDir, "bin", "publish");
            if (Directory.Exists(publishDir)) Directory.Delete(publishDir, true);

            await RunProcess("dotnet",
                $"publish -c Release -r linux-x64 --self-contained true -o \"{publishDir}\"",
                testFunctionDir);

            buildContextDir = testFunctionDir;
            dockerfilePath = Path.Combine(testFunctionDir, "Dockerfile");
        }

        try
        {
            await RunProcess("docker",
                $"build --platform linux/amd64 --provenance=false -f \"{dockerfilePath}\" -t {imageTag} \"{buildContextDir}\"",
                buildContextDir,
                timeout: useDockerPublish ? TimeSpan.FromMinutes(20) : TimeSpan.FromMinutes(5));

            var authResponse = await _ecrClient.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
            var authData = authResponse.AuthorizationData[0];
            var token = Encoding.UTF8.GetString(Convert.FromBase64String(authData.AuthorizationToken));
            var parts = token.Split(':');
            var registryUrl = authData.ProxyEndpoint;

            await RunProcess("docker",
                $"login --username {parts[0]} --password-stdin {registryUrl}",
                buildContextDir,
                stdin: parts[1]);

            await RunProcess("docker", $"push {imageTag}", buildContextDir);
        }
        finally
        {
            if (stagingDir != null && Directory.Exists(stagingDir))
            {
                try { Directory.Delete(stagingDir, true); }
                catch (Exception ex) { _output.WriteLine($"Cleanup error (staging dir): {ex.Message}"); }
            }
        }

        return imageTag;
    }

    /// <summary>
    /// Copies the minimum source tree needed to publish a NativeAOT durable function inside
    /// a Docker container: <c>buildtools/</c> (pulled in by Libraries\src\...\common.props),
    /// <c>Libraries/src/</c> (project references), and the function dir itself, all preserved
    /// at their original relative paths so ProjectReferences resolve.
    /// </summary>
    private static string StageBuildContextForDockerPublish(string testFunctionDir)
    {
        var repoRoot = FindRepoRoot(testFunctionDir);
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"durable-aot-stage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);

        CopyDirectoryFiltered(Path.Combine(repoRoot, "buildtools"), Path.Combine(stagingRoot, "buildtools"));
        CopyDirectoryFiltered(Path.Combine(repoRoot, "Libraries", "src"), Path.Combine(stagingRoot, "Libraries", "src"));

        var relFunctionDir = Path.GetRelativePath(repoRoot, testFunctionDir);
        CopyDirectoryFiltered(testFunctionDir, Path.Combine(stagingRoot, relFunctionDir));

        return stagingRoot;
    }

    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDir));
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "buildtools")) &&
                Directory.Exists(Path.Combine(dir.FullName, "Libraries")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate repo root (with buildtools/ and Libraries/) starting from {startDir}");
    }

    private static void CopyDirectoryFiltered(string source, string destination)
    {
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"Source directory not found: {source}");

        Directory.CreateDirectory(destination);
        foreach (var dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            // Skip build artifacts — they bloat the docker context and AOT publish needs a clean obj/.
            var rel = Path.GetRelativePath(source, dirPath);
            if (rel.Contains("bin", StringComparison.OrdinalIgnoreCase) ||
                rel.Contains("obj", StringComparison.OrdinalIgnoreCase) ||
                rel.Contains(".vs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            Directory.CreateDirectory(Path.Combine(destination, rel));
        }
        foreach (var filePath in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, filePath);
            if (rel.Contains("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                rel.Contains("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                rel.Contains(".vs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            File.Copy(filePath, Path.Combine(destination, rel), overwrite: true);
        }
    }

    private async Task RunProcess(string fileName, string arguments, string workingDir, string? stdin = null, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(5);
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
            Task.Delay(effectiveTimeout));

        if (!process.HasExited)
        {
            process.Kill();
            throw new TimeoutException($"{fileName} timed out after {effectiveTimeout.TotalMinutes} minutes");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stdout))
            _output.WriteLine($"stdout: {stdout[..Math.Min(stdout.Length, 1000)]}");

        if (process.ExitCode != 0)
        {
            _output.WriteLine($"stderr: {stderr}");
            throw new Exception($"{fileName} failed (exit {process.ExitCode}): {stderr}");
        }
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

        if (_ecrRepoCreated)
        {
            try
            {
                _output.WriteLine($"Deleting ECR repository: {_repoName}");
                await _ecrClient.DeleteRepositoryAsync(new DeleteRepositoryRequest
                {
                    RepositoryName = _repoName,
                    Force = true
                });
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (ECR): {ex.Message}"); }
        }

        if (_roleArn != null)
        {
            // Detach each policy independently — if one detach fails (e.g., the
            // policy was never attached because init bailed out early) we still
            // want to attempt the others and the final DeleteRole.
            await TryDetachPolicy("arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole");
            await TryDetachPolicy("arn:aws:iam::aws:policy/service-role/AWSLambdaBasicDurableExecutionRolePolicy");
            try
            {
                await _iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = _roleName });
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (IAM DeleteRole): {ex.Message}"); }
        }

        async Task TryDetachPolicy(string policyArn)
        {
            try
            {
                await _iamClient.DetachRolePolicyAsync(new DetachRolePolicyRequest
                {
                    RoleName = _roleName,
                    PolicyArn = policyArn
                });
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup error (IAM Detach {policyArn}): {ex.Message}"); }
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
