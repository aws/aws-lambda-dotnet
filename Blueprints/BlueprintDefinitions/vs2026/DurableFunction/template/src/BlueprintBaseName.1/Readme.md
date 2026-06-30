# Durable Lambda Function

This project contains a Lambda **durable execution** workflow built with the
[Amazon.Lambda.DurableExecution](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.DurableExecution)
**static wrapper** programming model, deployed straight to Lambda with `dotnet lambda deploy-function`.

Durable execution lets you write a multi-step workflow as a single straight-line method. The
runtime checkpoints every operation, so the function can be **suspended** during waits and
**resumed after a crash** without re-running completed work.

> Looking for the CloudFormation/Annotations variant? Use the **`serverless.DurableFunction`**
> template, which uses `[DurableExecution]` + a `serverless.template` and deploys with
> `dotnet lambda deploy-serverless`.

## How it works

`Function.Handler` is the Lambda entry point. It delegates to `DurableFunction.WrapAsync`, which
bridges the durable invocation envelope to the strongly-typed `ProcessOrder` workflow:

```csharp
public Task<DurableExecutionInvocationOutput> Handler(
    DurableExecutionInvocationInput input, ILambdaContext context)
    => DurableFunction.WrapAsync<OrderRequest, OrderResult>(ProcessOrder, input, context);
```

This is the **class-library** hosting model on the managed `dotnet10` runtime: there is no
`Main`/`LambdaBootstrap` loop and no `[DurableExecution]` annotation. The runtime hosts the
bootstrap and invokes `Handler` directly via the `Assembly::Type::Method` handler string in
`aws-lambda-tools-defaults.json`. The serializer is declared with an assembly attribute:

```csharp
[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
```

The workflow uses the core durable primitives on `IDurableContext`:

| Primitive | Used for |
|-----------|----------|
| `StepAsync` | A checkpointed unit of work. On replay the cached result is returned instead of re-running the body. |
| `StepAsync` + `StepConfig.RetryStrategy` | Retry a flaky step with exponential backoff; only the successful attempt is checkpointed. |
| `StepSemantics.AtMostOncePerRetry` | Avoid re-running a side-effecting step (e.g. charging a card) if Lambda is re-invoked mid-attempt. |
| `WaitAsync` | Suspend the workflow for a delay. There is no compute charge while suspended. |
| `RunInChildContextAsync` | Group related operations into a single logical operation. |

> **Note:** Durable execution requires the managed **`dotnet10`** runtime.

## Requirements

* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* [Amazon.Lambda.Tools](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools)

  ```bash
  dotnet tool install -g Amazon.Lambda.Tools
  ```

## Deploy

`aws-lambda-tools-defaults.json` sets the runtime (`dotnet10`), handler, and the durable execution
timeout (`durable-execution-timeout`). Deploy the function directly to Lambda with:

```bash
dotnet lambda deploy-function
```

When the tool creates the function's execution role for you, it automatically attaches the
`AWSLambdaBasicDurableExecutionRolePolicy` managed policy, which grants the durable-execution
checkpoint permissions the function needs at runtime. If you supply your own role
(`--function-role`), make sure that policy is attached to it.

## Invoke

Durable functions are invoked with a qualified function reference and a durable execution name:

```bash
dotnet lambda invoke-function BlueprintBaseName.1 --payload '{"OrderId":"order-123","Items":["sku-1","sku-2"]}'
```

The workflow validates the order, charges payment, waits out a short settlement period, ships the
order in a child context, and returns the result.

## Test

The included test project drives the workflow locally with the
`Amazon.Lambda.DurableExecution.Testing` runner — no AWS resources required:

```bash
dotnet test
```
