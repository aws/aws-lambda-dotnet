# Durable Execution Conformance Tests (.NET)

This directory wires the .NET Durable Execution SDK into the language-neutral
[`aws-durable-execution-conformance-tests`](https://github.com/aws/aws-durable-execution-conformance-tests)
runner. The runner is a Python tool that deploys a SAM template, invokes each
mapped Lambda, and validates the durable execution **result** and **event
history** against language-agnostic requirement specs.

## How it works

- Each requirement (e.g. `1-1`) has a YAML spec in the runner's
  `test-requirements/<suite>/` directory describing the expected result and
  execution history.
- For every requirement we implement, there is a small handler project under
  `<suite>/<HandlerName>/` (executable model: `Main` + `LambdaBootstrap`,
  `AssemblyName=bootstrap`). Each project references the in-repo SDK directly.
- `template_<suite>.yaml` maps each function to its requirement id(s) via
  `TestingMetadata.TestDescription: ["1-1"]` and deploys it on the `dotnet8`
  managed runtime.
- The runner reads `TestingMetadata`, deploys the template, invokes each
  function (sync or async depending on the requirement), then asserts.

Handlers are published ahead of time into `publish/<HandlerName>/`; the SAM
template's `BuildMethod: makefile` copies the pre-built `bootstrap` into the
deploy artifact.

## Layout

```
Conformance/
├── README.md
├── template_step.yaml          # one template per suite; functions -> requirement ids
├── scripts/
│   ├── build_examples.sh       # dotnet publish each handler -> publish/<Fn>/
│   ├── discover_suites.py      # emits the CI matrix (suites with template + handlers)
│   └── inject_execution_role.py# CI: point functions at a pre-existing role
└── step/                       # one dir per suite; one subdir per handler
    ├── StepBasic/              # 1-1
    ├── StepWithName/           # 1-2
    └── ...                     # 1-3 .. 1-20
```

## Coverage

All nine suites are implemented (one handler project per requirement id):

| Suite | Ids | Handlers |
|-------|-----|----------|
| `step` | 1-1 .. 1-20 | 20 |
| `wait` | 2-1 .. 2-5 | 5 |
| `child` | 3-1 .. 3-13, 3-15 .. 3-18 | 17 |
| `callback` | 4-1 .. 4-19 | 19 |
| `invoke` | 5-1 .. 5-15 | 15 (+2 target functions, +1 tenancy alias) |
| `wait_for_condition` | 6-1 .. 6-13 | 13 |
| `wait_for_callback` | 7-1 .. 7-15 | 15 |
| `parallel` | 8-1 .. 8-22 (8-15 n/a) | 21 |
| `map` | 9-1 .. 9-18 (9-14 n/a) | 17 |

A few requirement ids have no .NET handler because the SDK intentionally lacks
the feature they exercise (e.g. per-item / whole-result serdes slots in `map`);
those are documented in the relevant `template_<suite>.yaml` and reported as
`NOT_IMPLEMENTED` (non-blocking) rather than silently omitted.

### Handlers that need extra resources

- **Retry-across-invocation tests** (`step` 1-11/1-13/1-14/1-15/1-18, `child`
  3-7/3-12) count attempts across separate invocations, which the replay model
  cannot hold in memory, so they use the `AttemptsTable` DynamoDB table declared
  in the template (`AWSSDK.DynamoDBv2`).
- **`invoke` targets** — the suite deploys two callee functions
  (`InvokeEchoTarget`, `InvokeFailTarget`) that the workflow handlers invoke via
  `AWSSDK.Lambda`; ARNs are wired through env vars with `Fn::GetAtt`. The
  tenancy test (5-8) reuses the echo target's binary under a second logical id
  (`InvokeEchoTargetTenant`, `PER_TENANT` isolation) — `build_examples.sh`
  produces that publish dir by aliasing (there is no separate source project).

## Prerequisites

- .NET 8 SDK
- Python 3.14+ and the conformance runner:
  ```bash
  pip install "git+https://github.com/aws/aws-durable-execution-conformance-tests.git@main#subdirectory=packages/aws-durable-execution-conformance-tests"
  ```
- [SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html)
- AWS credentials for an account allowed to deploy + invoke (CloudFormation, IAM,
  Lambda, DynamoDB). Prefix commands with `unset AWS_PROFILE` to use `[default]`.

## Running locally

From this directory (swap `step` for any suite name):

```bash
# 1. Publish the suite's handlers into publish/<Fn>/
#    (omit the arg to publish every suite)
./scripts/build_examples.sh step

# 2. Deploy + invoke + validate the suite
unset AWS_PROFILE
python -m aws_durable_execution_conformance_tests.app \
  --template template_step.yaml \
  --language dotnet \
  --suite step \
  --name conformance-dotnet-step \
  --region us-east-1 \
  --history-dir history-step \
  --report console
```

> On Windows, set `PYTHONUTF8=1` — the runner prints `✅`/`❌`, which crashes the
> summary printer under the default cp1252 console encoding.

The checked-in template is self-contained (it creates its own
`DurableFunctionRole`). CI instead injects a pre-existing execution role with
`scripts/inject_execution_role.py`.

## CI

`.github/workflows/conformance-tests.yml` runs one matrix job per discovered
suite: publish handlers → install the runner → assume the deploy role via OIDC →
inject the execution role → run the suite → upload the JUnit report. It requires
the repository secrets `TEST_ROLE_ARN` (SAM-capable deploy role) and
`TEST_LAMBDA_EXECUTION_ROLE_ARN`, plus the `AWS_REGION` variable.

## Adding a suite

1. Add `<suite>/<HandlerName>/` handler projects (one per requirement).
2. Add `template_<suite>.yaml` mapping each function to its requirement id(s).
3. Declare any intentional gaps under a function's
   `TestingMetadata.NotImplemented` (reported `NOT_IMPLEMENTED`, non-blocking).

`discover_suites.py` picks it up automatically, so it becomes a new CI matrix job.
