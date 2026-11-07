# AWS.ToolExtras.LambdaPSRuntime

Value-add PowerShell tooling for running PowerShell on AWS Lambda via a **customer-managed runtime**
or a **custom runtime**. It scaffolds, validates, builds, and packages runtime and function projects,
and offers one-command deployments for the common case.

AWS Lambda has no managed PowerShell runtime, so PowerShell brings its own: a native `pwsh`
interpreter plus a bootstrap implementing the Lambda Runtime API loop. That runtime ships as a
customer-managed runtime (a container image that functions bind to by alias), as a custom runtime on
`provided.al2023` (a layer, or bundled into the function's .zip), or inside a function's own container
image. This module automates building and deploying it. It lives deliberately outside the
`AWS.Tools.*` family so it is not tied to that family's unified-versioning lockstep.

## Requirements

- PowerShell 7.4+
- For the container-based approaches (CMR, container-per-function): **Docker with `buildx`** on
  `PATH`. Docker is the only container CLI this module supports. A customer-managed runtime needs a
  Docker v2 Schema 2 manifest, which the image build emits with buildx's `--provenance=false`,
  `--sbom=false` and `oci-mediatypes=false`. Other CLIs can produce one too (for example `podman push --format v2s2`),
  but the module builds and tests with Docker only.
  `Test-LambdaPSContainerRuntime` preflights both the CLI and the plugin.
- The container-free approaches (Layer, self-contained `.zip`) need no container runtime at all —
  that is the path for Docker-averse environments.
- AWS.Tools 5.0.1001 or later (`AWS.Tools.Lambda`, `AWS.Tools.LambdaCore`, `AWS.Tools.ECR`,
  `AWS.Tools.SecurityToken`), with an `AWS.Tools.LambdaCore` that exports the Lambda
  customer-managed runtime cmdlets (`New-LMCRuntime`, `New-LMCRuntimeVersion`,
  `New-LMCRuntimeAlias`, …) and an `AWS.Tools.Lambda` whose function cmdlets accept the runtime
  binding (`Publish-LMFunction -RuntimeConfig_RuntimeArn -RuntimeConfig_RuntimeAlias`).

## Concepts

The runtime can be packaged four ways; they differ only in packaging, each a scaffolding template
with its own lint rules:

| Approach | Scaffold with | `pwsh` | Container | One-command deploy |
| --- | --- | --- | --- | --- |
| **CMR** (default) | `New-LambdaPSRuntimeProject -Path ./rt -PowerShellVersion 7.6` (`-Target CMR` is the default) | installed in image | Yes | Yes |
| Layer | `New-LambdaPSRuntimeProject -Path ./rt -PowerShellVersion 7.6 -Target Layer` | bundled on `provided.al2023` | No | No |
| Self-contained `.zip` | `New-LambdaPSFunctionProject -Path ./fn -Handler handler -Target SelfContainedZip -PowerShellVersion 7.6` | bundled on `provided.al2023` | No | No |
| Container-per-function | `New-LambdaPSFunctionProject -Path ./fn -Handler handler -Target Container -PowerShellVersion 7.6` | installed in image | Yes | No |

A function bound to a customer-managed runtime needs only its handler code: `New-LambdaPSFunctionProject
-Path ./fn -Handler handler` scaffolds one (`-Target RuntimeBound`, the default).

CMR (customer-managed runtime) is the default: publish the runtime once as a container image,
reference it by alias, and roll the whole fleet forward by moving the alias — no per-function
redeploy. Each PowerShell line (7.4, 7.5, 7.6) installs a pinned pwsh release by default, unless
`New-LambdaPSRuntimeDeployment` finds a newer one it can verify (see
[Patching a runtime](#patching-a-runtime)); the
releases and their tarball SHA256s are listed in `Config/pwsh.psd1`, and `-PwshVersion` picks
another release listed there. A module release moves each line to its newest servicing release and
keeps the SHA256s of the earlier ones, so a project pinned to an earlier release still builds.
PowerShell 7.4 and 7.5 reach end of support on 10 November 2026
([lifecycle](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle)):
the first module release after that date stops installing them by default, so
`-PowerShellVersion 7.4` or `7.5` then also needs `-PwshVersion`. Use 7.6 for new projects. The
container-free targets run in invariant-globalization mode (`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`)
so no ICU libraries are needed.

The Layer and self-contained `.zip` packages bundle pwsh, so they are large: about 72 MB zipped and
174 MB unzipped for PowerShell 7.4 on x86_64, before any modules. That is over Lambda's direct-upload
limit, so upload the `.zip` to S3 and pass `-Code_S3Bucket`/`-Code_S3Key` (`-Content_S3Bucket`/
`-Content_S3Key` for `Publish-LMLayerVersion`). The 250 MB unzipped limit leaves roughly 75 MB for modules and code.

## Quick start (high-level)

Deploy the runtime once, then deploy functions bound to its alias.

```powershell
Import-Module ./src/AWS.ToolExtras.LambdaPSRuntime/AWS.ToolExtras.LambdaPSRuntime.psd1

# 1. Deploy the runtime (preflight container CLI -> scaffold -> build/push image ->
#    publish version -> point the 'default' alias at it).
New-LambdaPSRuntimeDeployment -RuntimeName my-powershell-runtime -PowerShellVersion 7.6 `
    -IncludeModule @(
        @{ ModuleName = 'AWS.Tools.S3';         RequiredVersion = '5.0.274' },
        @{ ModuleName = 'AWS.Tools.DynamoDBv2';  RequiredVersion = '5.0.274' }
    ) -Region us-east-1

# 2. Scaffold a function: ./hello/handler.ps1 is a starter handler to edit.
New-LambdaPSFunctionProject -Path ./hello -Handler handler

# 3. Deploy it bound to the runtime alias (resolves the runtime ARN for you). The role must exist
#    and trust lambda.amazonaws.com.
New-LambdaPSFunctionDeployment -FunctionName hello-powershell -ProjectPath ./hello `
    -RuntimeName my-powershell-runtime -RuntimeAlias default `
    -Role arn:aws:iam::111122223333:role/hello-powershell-role -Region us-east-1

# 4. Invoke it.
Invoke-LambdaPSFunction -FunctionName hello-powershell -Payload @{ name = 'world' } -Region us-east-1 `
    -IncludeLog -InformationAction Continue
```

The three cmdlets that call AWS take `-ProfileName`, `-ProfileLocation` and `-Credential`; a
relative `-ProfileLocation` is resolved against the current PowerShell location. Without them,
AWS.Tools uses its default credentials (`Set-AWSCredential`, environment variables, the default
profile, or an instance or container role). `-Region` is optional
the same way: without it, the region comes from `Set-DefaultAWSRegion`, the `-ProfileName` profile, the
default profile, `AWS_REGION`, the `AWS_PROFILE` profile, or the instance or container, in that order
(`AWS_DEFAULT_REGION` is not read), and a deploy that finds none fails with `RegionNotResolved`. For `New-LambdaPSFunctionDeployment` and
`Invoke-LambdaPSFunction`, a full function ARN in `-FunctionName` supplies the region when `-Region` is
omitted, and a `-Region` that differs from the ARN's fails with `FunctionRegionMismatch`.

A handler reads the parsed event as `$LambdaInput` and the runtime details as `$LambdaContext`. A script
handler has both in scope, with or without `param($LambdaInput, $LambdaContext)`; a function handler
receives them positionally, so it declares that param block.
Whatever the handler leaves on the pipeline is returned as the response. `-Handler` takes one of three
forms: `<script>.ps1` runs the whole file per invoke; `<script>.ps1::<function>` dot-sources the file
once and calls the function per invoke; `Module::<module>::<function>` calls a function of a module.

Omit `-Handler` and it is resolved from the source itself: the `ScriptFile` in a scaffolded project's
`manifest.json`, else the sole `*.ps1` at the root. An ambiguous folder is an error rather than a
guess, and a handler naming a script that is not in the package is rejected before anything is
deployed — Lambda accepts such a function, and its runtime then fails at start-up with "Handler
script not found".

**Architecture is checked, not assumed.** Lambda rejects a function whose CPU architecture its
customer-managed runtime does not support when the function is created; the deploy cmdlets check
first, so the error names the runtime alias. The service reports each runtime version's architecture from
the image manifest (not from anything the caller declares), and both deploy cmdlets check against it.
`New-LambdaPSRuntimeDeployment -Architecture` builds for that platform and stops before moving the
alias if the image turns out to be a different architecture — for example a buildx builder that cannot
emit `linux/arm64`. Moving an existing alias to a version of a different architecture is allowed but
warns: the functions bound to that alias fail to invoke until they are redeployed for the new
architecture. `New-LambdaPSFunctionDeployment` reads the architecture from the alias: omit
`-Architecture` and the function takes the runtime's; state one that differs and the deploy stops
before anything is created.

`New-LambdaPSFunctionDeployment` defaults `-Timeout` to 30 seconds and `-MemorySize` to 512 MB rather
than inheriting Lambda's 3 s / 128 MB, which a PowerShell function cannot reliably meet: a warm
invoke can take about 2 seconds at 128 MB, and pwsh uses about 115–130 MB of memory on startup. Memory
also scales CPU, so at 512 MB the same warm invoke takes about half a second.

### Patching a runtime

Run the same `New-LambdaPSRuntimeDeployment` command again to patch the runtime. Without
`-PwshVersion`, it installs the newest pwsh servicing release of `-PowerShellVersion` that the
image build can verify; without `-RuntimeVersion`, it publishes it as a new version named
`<pwsh release>-<n>` (`7.6.9-1`, then `7.6.9-2` for a rebuild that only changes modules or the base
image, then `7.6.10-1`) and moves the alias. Deleting the newest version frees its name for the next
deploy.

| Architecture | Installs | Release chosen |
| --- | --- | --- |
| x86_64 | Microsoft's GPG-signed RPM | the newest in packages.microsoft.com, read once in preflight so `-WhatIf` shows it, never older than the module's pin; if the repository cannot be read, the pinned release, with a warning |
| arm64 | the release tarball, checked against a SHA256 pinned in `Config/pwsh.psd1` | the newest release with a pinned hash, with a warning when a newer release exists |

The deploy prints the release it installs, and the version's release note is `pwsh <release>`. When
the alias already pointed at a version, the result's `PreviousRuntimeVersion` says which.
Naming a version lists the runtime's versions, so the deploy needs `lambda:ListRuntimeVersions`. Pass
`-PwshVersion` or `-RuntimeVersion` to pin either choice; for example, to put x86_64 and arm64 runtimes
on the same release, pass the arm64 one's `PwshVersion` to the x86_64 deploy.

Because an unchanged command picks up new pwsh releases, a pipeline that runs it on every commit also
patches every function bound to the alias. Deploy to a test alias first, and move production to the
version you tested:

```powershell
$test = New-LambdaPSRuntimeDeployment -RuntimeName my-powershell-runtime -PowerShellVersion 7.6 `
    -RuntimeAlias test -Region us-east-1
# ... run the functions bound to the 'test' alias ...
# New-LMCRuntimeAlias the first time, when 'default' does not exist yet.
Update-LMCRuntimeAlias -RuntimeName my-powershell-runtime -Name default `
    -RuntimeVersion $test.RuntimeVersion -Region us-east-1
```

## Low-level cmdlets

Reach for these to override any of the four decisions the one-command deploy makes for you (packaging
approach, stock image, one ECR repository per runtime, a new version per deploy behind one alias):

- Scaffold: `New-LambdaPSRuntimeProject`, `New-LambdaPSFunctionProject` (`-Target` selects the approach)
- Validate: `Test-LambdaPSRuntimeProject`, `Test-LambdaPSContainerRuntime`
- Build / package: `Build-LambdaPSRuntimeImage`, `Build-LambdaPSRuntimePackage`, `Compress-LambdaPSFunctionPackage`
- Modules: `Save-LambdaPSModule`
- Publish and alias, with AWS.Tools directly: `New-LMCRuntime`, `New-LMCRuntimeVersion`, and
  `New-LMCRuntimeAlias` or `Update-LMCRuntimeAlias`. Lambda pulls the image from ECR, so the
  repository policy must let `lambda.amazonaws.com` pull it, with the conditions `aws:SourceAccount`
  (your account) and `aws:SourceArn` (the runtime's ARN).

`Test-LambdaPSRuntimeProject` is a gate, not just a report: a failing rule goes to the error stream as
well as the pipeline, so `-ErrorAction Stop` halts a script, `$?` is `$false` afterwards, and `-Quiet`
returns a single boolean.
Pass `-ErrorAction SilentlyContinue` when you only want the findings.

`-Force` on either scaffolder **replaces** the target directory (its contents are deleted first)
rather than writing over the top. Merging would leave files the new target does not emit — a CMR
`Dockerfile` surviving into a re-scaffolded Layer project, for instance, which then builds into an
image whose bootstrap points at a path the image does not contain. `-WhatIf` shows what would go.
On Linux and macOS, an existing target directory owned by a user other than you, or by root and
writable by group or others, is refused with `TargetOwnedByAnotherUser`, even with `-Force` (no check
when running as root). `-Force` does not replace a non-empty target that is a link
(`TargetIsLink`), a drive root, or the current location or your home folder or a folder that contains
either (`TargetProtected`). `New-LambdaPSRuntimeDeployment` replaces
`-ProjectPath` the same way, but only when it holds a runtime-image project this module scaffolded
(a `lambdaps-runtime.json` marker with target CMR); any other non-empty directory is refused with
`TargetNotEmpty` before anything is created.

`-WhatIf` runs the same local checks as a real run — module pins and availability, the pwsh version,
the handler, the architecture — and the deploy cmdlets also run their read-only AWS checks, so a
preview that succeeds is one the real run would get past those checks too.

Every cmdlet has full comment-based help (`Get-Help <Name> -Full`).

The runtime reads two environment variables, set on the function: `POWERSHELL_RUNTIME_VERBOSE=TRUE`
logs its start-up and per-invocation steps, and `POWERSHELL_RUNTIME_JSON_DEPTH` (1-100, default 100)
is the depth to which a handler's output is serialized; an invalid value uses 100 and writes a warning to
the function log. Return plain data from handlers; a rich .NET
object (for example from `Get-Item`) serialized that deep can exhaust memory. Every scaffolded
project's README describes both. For a function bound to a customer-managed runtime,
`New-LambdaPSFunctionDeployment -EnvironmentVariable` sets them, with the function's other environment
variables.

## Modules

`-IncludeModule` (alias `-Module`) takes `[Microsoft.PowerShell.Commands.ModuleSpecification[]]`.
Each module **must pin an exact `RequiredVersion`** (a minimum or range is rejected); the module never
resolves "latest". Modules are copied into the project's `modules/` folder, following each manifest's
`RequiredModules` to copy transitive dependencies at the versions they declare. The function packager
and `Build-LambdaPSRuntimePackage` (for a SelfContainedZip) also harvest `#Requires -Modules` from the
handler under the same rules.

A module (or dependency) that is not installed locally is handled by `-MissingModuleAction`, on the
scaffolders, `Compress-LambdaPSFunctionPackage`, `Build-LambdaPSRuntimePackage` and both deploy
cmdlets:

| Value | Behavior |
|---|---|
| `Save` (default) | Download into a temporary folder, stage from there, delete the folder. The local PowerShell environment is not changed. |
| `Install` | Download the same way, and also install into the CurrentUser module folder (never over an existing version). |
| `Stop` | Fail with `ModuleNotFoundLocally`. |

Downloads use **AWS.Tools.Installer** 2.0.5 or a later 2.x for `AWS.Tools.*` modules (the AWS.Tools.zip from
CloudFront, one download per version; the Installer checks it against the SHA512 published beside it, which detects a
corrupted download, not a tampered one, and only on Windows also checks each module manifest's Authenticode signer)
and **PSResourceGet** (`Save-PSResource`) for everything
else, from your registered repositories in priority order, or from the ones you name with
`-ModuleRepository` (for example an internal mirror, so a same-named package in another feed cannot be
picked up). PowerShellGet is never called, but AWS.Tools.Installer requires it to be installed. If Installer 2.x
is not installed, the version this module pins is itself saved with PSResourceGet, from `-ModuleRepository` or
else PSGallery, and used from the temporary folder only when its files match the pinned SHA256 and, on Windows,
its Authenticode signature is valid. Repository
trust is not bypassed: an untrusted repository (PSGallery by default) prompts, and fails in a
non-interactive session — trust it with `Set-PSResourceRepository -Name PSGallery -Trusted`, or pre-stage modules and
use `Stop`.

Whenever something is downloaded, a warning names the modules, each with the repository it came from, and says
to test those exact versions before production use. It also names any module downloaded at the minimum a
`ModuleVersion` entry asks for, and, when `-ModuleRepository` is omitted and the modules came from more than one
repository, says to pass it. Modules that came from PSGallery get a second warning that PSGallery is not
recommended as a direct production dependency.

`Save-LambdaPSModule -Module <specs> -Path ./mods` runs the same download on its own, and also copies
any module in the set that is already installed, so `./mods` holds the complete set at the exact
versions a function will ship with. Test against it in a new PowerShell session, with `./mods` added
to `$env:PSModulePath` before anything is imported: a session that has already loaded AWS.Tools cannot
load another build of its assemblies.

## Migrating from AWSLambdaPSCore

Handlers written for AWSLambdaPSCore (`New-AWSPowerShellLambda`, `Publish-AWSPowerShellLambda`) run
on this runtime with these differences:

| AWSLambdaPSCore | This module |
| --- | --- |
| `New-AWSPowerShellLambda` | `New-LambdaPSFunctionProject` |
| `Publish-AWSPowerShellLambda` | `New-LambdaPSFunctionDeployment -ProjectPath` |
| `-PowerShellFunctionHandler <function>` | `-Handler <script>.ps1::<function>` |

- **A function handler declares its input.** A script handler reads `$LambdaInput` and
  `$LambdaContext` as before. A `<script>.ps1::<function>` or `Module::` handler receives them
  positionally, so the function needs `param($LambdaInput, $LambdaContext)`.
- **`#Requires -Modules` pins an exact version.** Change `ModuleVersion` to `RequiredVersion`, for
  example `#Requires -Modules @{ModuleName='AWS.Tools.S3';RequiredVersion='5.0.274'}`: packaging
  refuses a minimum version with `ModuleVersionRequired`.
- **The response is all of the handler's pipeline output, not just the last item.** One object is
  returned as that object; two or more are returned as a JSON array. Send everything else to `$null`
  (`$null = Invoke-Something`, or `| Out-Null`) so the handler outputs only its response, for example
  with `return $result`.
- **`$LambdaContext` has no `Logger`.** Log with `Write-Host`, which goes to the function's
  CloudWatch log: `$LambdaContext.Logger.LogLine($message)` becomes `Write-Host $message`.
- **`$LambdaContext.ClientContext` and `$LambdaContext.Identity` are JSON strings**, as Lambda sent
  them, and empty when the caller sent none. Parse them with `ConvertFrom-Json`, for example
  `$identity = $LambdaContext.Identity | ConvertFrom-Json`.

## Attribution

The module sets the SDK `ClientAppId` on the client config it passes to the cmdlets it wraps, so its
calls carry a stable `app/AWS.ToolExtras.LambdaPSRuntime#<version>` token for adoption metrics.

## Testing

Run the offline test suite (all AWS and container seams are mocked) with coverage. The suite also
checks every AWS cmdlet call against the real cmdlets, so it needs an `AWS.Tools.LambdaCore` that
exports the Lambda customer-managed runtime cmdlets; set `LAMBDAPS_AWSTOOLS_MODULE_PATH` to a
directory containing `AWS.Tools.*/<version>/` to use one that is not on `PSModulePath`. The suite
needs Pester 5.5.0 or later (`Install-PSResource Pester -Scope CurrentUser`), and `-Task Lint` needs
PSScriptAnalyzer 1.21.0 or later:

```powershell
pwsh ./build.ps1            # Pester suite + code coverage
pwsh ./build.ps1 -Task All  # PSScriptAnalyzer, then the suite
pwsh ./build.ps1 -Task Integration # the integration suite only, against AWS
```

The variable is read by `build.ps1` and `Contract.AwsCmdlet.Tests.ps1`. To use such a build interactively, put the same directory
at the front of `PSModulePath` before importing this module:

```powershell
$env:PSModulePath = '/path/to/aws-tools-modules' + [IO.Path]::PathSeparator + $env:PSModulePath
Import-Module ./src/AWS.ToolExtras.LambdaPSRuntime/AWS.ToolExtras.LambdaPSRuntime.psd1
```

Results are written to `testResults.xml` (NUnit) and `coverage.xml` (JaCoCo), both git-ignored.

Test files in `src/AWS.ToolExtras.LambdaPSRuntime/Tests/` are named by kind, and every `Describe` is
tagged with its file's kind:

| File | Tests |
|------|-------|
| `Unit.Public.<Command>.Tests.ps1`, `Unit.Private.<Function>.Tests.ps1` | one command, with the AWS and container calls it makes mocked |
| `Component.<Topic>.Tests.ps1` | several commands working together, with AWS mocked |
| `Contract.<Topic>.Tests.ps1` | the module as a whole: its exports and help, and its AWS cmdlet calls against the real cmdlets |
| `Integration.<Topic>.Tests.ps1` | end to end against AWS: each target deployed and invoked, then removed; run only by `-Task Integration`; every other task skips it (the file's header says what it creates and costs) |

Setup that several files share (importing the module, stand-in exception types, helper functions)
is in `Tests/Helpers/`, which each file dot-sources from its `BeforeAll`.

### Analyzer contract

`-Task Lint` runs PSScriptAnalyzer in two scopes, because the module source and its tests/templates
are held to different standards:

| Scope | Settings | Gate |
| --- | --- | --- |
| `Public/`, `Private/`, `Config/`, `*.psm1`, `*.psd1` | `PSScriptAnalyzerSettings.psd1` (default rules, no exclusions) | **any** finding fails |
| `Tests/`, `Templates/` | `PSScriptAnalyzerSettings.Relaxed.psd1` | only `Error` and `ParseError` (a file that does not parse) fail |

Where a rule is statically wrong about the source, it
is suppressed **at the site** with `[Diagnostics.CodeAnalysis.SuppressMessageAttribute]` and a
`Justification`, rather than excluded globally — so the reason sits next to the code and the gate is
not eroded for the rest of the module. The relaxed scope's exclusions (`PSAvoidUsingWriteHost` for
runtime logging in `Templates/`, `PSReviewUnusedParameter` for mockable stub signatures in `Tests/`,
`PSUseSingularNouns` for non-exported helper names in both) are each documented in that settings
file.

## Status

- **AWS.Tools dependency:** the AWS-calling paths use the Lambda customer-managed runtime cmdlets
  (`New-LMCRuntime`, `New-LMCRuntimeVersion`, `New-LMCRuntimeAlias`, `Update-LMCRuntimeAlias`,
  `Get-LMCRuntime`, …) and the function cmdlets' runtime binding (`-RuntimeConfig_RuntimeArn`,
  `-RuntimeConfig_RuntimeAlias`). The manifest declares
  `AWS.Tools.Lambda`/`.LambdaCore`/`.ECR`/`.SecurityToken` as `RequiredModules` with a **minimum**
  `ModuleVersion` of `5.0.1001`, the first build with those cmdlets and parameters, not an exact
  pin, so consumers can take newer AWS.Tools releases without this module re-pinning. All four share
  one `AWS.Tools.Common` build, so they have the same floor. Without them, or with an older build,
  the module does not import. A manifest cannot express "exports command X", so a later build whose
  modules lack those cmdlets or parameters imports fine and fails at the point of call.
- **Re-deploying an existing function updates its code and only the configuration you give.**
  `New-LambdaPSFunctionDeployment` against a function that already exists updates its code with
  `Update-LMFunctionCode`, then its configuration with `Update-LMFunctionConfiguration`: always its
  role, and `-Handler`, `-Timeout`, `-MemorySize`, `-RuntimeAlias` and `-EnvironmentVariable` only when
  you give them. Anything you don't give keeps the function's current value, so a redeploy doesn't
  reset a tuned timeout, memory size or alias to the defaults. The live handler is kept unless you give
  `-Handler`, its script is not in the package, or it is not a PowerShell handler, and the live alias is kept unless you give
  `-RuntimeAlias` or `-RuntimeName` names another runtime. The handler and alias a scaffolded project's
  `manifest.json` records apply to a new function (the alias also to one moving to another runtime);
  where the live ones are kept, a warning says when the manifest's differ. When the alias is kept, a
  stated `-Architecture` is checked against it: one it supports is applied, and one it doesn't is
  refused with `ArchitectureMismatch`. An existing function of the same name that is not
  bound to a customer-managed runtime is refused with `FunctionNotRuntimeBound` and left untouched. If
  the configuration update fails after the code update, `FunctionConfigurationNotApplied` says what the
  function now has; fix the cause the service reported, then run the deploy again.
- **A function deploy returns once the function can be invoked.** Lambda creates a function in state
  `Pending` and runs a code update as `InProgress`; `New-LambdaPSFunctionDeployment` waits for both to
  settle (up to 120 seconds each: before the code update, before the configuration update, and after
  it). A function that fails to become ready is `FunctionNotActive`, naming Lambda's reason; one still
  settling at the limit is `FunctionStateTimeout`, and it may still become ready, so check it with
  `Get-LMFunctionConfiguration`.
