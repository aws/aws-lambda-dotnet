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

| Suite | Requirements | Handlers implemented |
|-------|-------------|----------------------|
| `step` | 1-1 .. 1-20 | ✅ all 20 |
| `wait`, `child`, `callback`, `invoke`, `parallel`, `map`, `wait_for_callback`, `wait_for_condition` | — | not yet scaffolded |

Retry requirements (`1-11`, `1-13`, `1-14`, `1-15`, `1-18`) count attempts
across separate invocations, which the replay model cannot hold in memory, so
those handlers use the `AttemptsTable` DynamoDB table declared in the template.

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

From this directory:

```bash
# 1. Publish the step handlers into publish/<Fn>/
./scripts/build_examples.sh step

# 2. Deploy + invoke + validate the step suite
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
