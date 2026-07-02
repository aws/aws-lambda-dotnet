# Durable Lambda Function

This project contains a Lambda **durable execution** workflow built with the
[Lambda Annotations](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.Annotations)
programming model.

Durable execution lets you write a multi-step workflow as a single straight-line method. The
runtime checkpoints every operation, so the function can be **suspended** during waits and
**resumed after a crash** without re-running completed work.

## How it works

`Function.ProcessOrder` is the workflow entry point. It is marked with two attributes:

* `[LambdaFunction]` — registers the method with the Annotations source generator.
* `[DurableExecution(executionTimeout: 86400)]` — tells the generator to wrap the method with the
  durable runtime and to add the durable configuration and IAM policy to `serverless.template`.
  `executionTimeout` (in seconds) is required.

The workflow uses the core durable primitives on `IDurableContext`:

| Primitive | Used for |
|-----------|----------|
| `StepAsync` | A checkpointed unit of work. On replay the cached result is returned instead of re-running the body. |
| `StepAsync` + `StepConfig.RetryStrategy` | Retry a flaky step with exponential backoff; only the successful attempt is checkpointed. |
| `StepSemantics.AtMostOncePerRetry` | Avoid re-running a side-effecting step (e.g. charging a card) if Lambda is re-invoked mid-attempt. |
| `WaitAsync` | Suspend the workflow for a delay. There is no compute charge while suspended. |
| `RunInChildContextAsync` | Group related steps into a single logical operation. |

The class-library model is used (no `Main`): the managed `dotnet10` runtime hosts the bootstrap and
invokes the generated handler wrapper directly.

> **Note:** Durable execution requires the managed **`dotnet10`** runtime.

## Requirements

* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* [Amazon.Lambda.Tools](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools)

  ```bash
  dotnet tool install -g Amazon.Lambda.Tools
  ```

## Deploy

The `[DurableExecution]` attribute drives the source generator, which keeps the function resource in
`serverless.template` in sync (runtime, handler, `DurableConfig`, and the
`AWSLambdaBasicDurableExecutionRolePolicy` managed policy). Deploy the serverless application with:

```bash
dotnet lambda deploy-serverless
```

## Invoke

CloudFormation generates the deployed function name, so it won't be `BlueprintBaseName.1`. Find it by
looking at the deployed CloudFormation stack — either in the AWS Console (the stack's **Resources**
tab) or with the CLI:

```bash
aws cloudformation describe-stack-resources --stack-name <stack-name> \
    --query "StackResources[?ResourceType=='AWS::Serverless::Function' || ResourceType=='AWS::Lambda::Function'].PhysicalResourceId"
```

Durable functions are invoked asynchronously and then monitored until the execution completes. Pass
`--invoke-mode DurableExecution` so the tool starts the execution and polls it to completion:

```bash
dotnet lambda invoke-function <generated-function-name> --invoke-mode DurableExecution --payload '{"OrderId":"order-123","Items":["sku-1","sku-2"]}'
```

The workflow validates the order, charges payment, waits out a short settlement period, ships the
order in a child context, and returns the result.

## Test

You can drive the workflow locally with the `Amazon.Lambda.DurableExecution.Testing` package — no AWS
resources required. Add a test project that references your function project and
`Amazon.Lambda.DurableExecution.Testing`, then use its in-memory durable execution runner to invoke
`ProcessOrder` and assert on the result. If you created this project from the Visual Studio template
that includes a test project, run it with:

```bash
dotnet test
```
