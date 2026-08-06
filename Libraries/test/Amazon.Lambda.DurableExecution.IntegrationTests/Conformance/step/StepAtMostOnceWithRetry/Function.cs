// 1-18: Step with AtMostOncePerRetry semantics (with retry, succeeds on second attempt)
// Uses DynamoDB to track attempts across invocations
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Logging;

namespace StepAtMostOnceWithRetry;

public class Function
{
    private static readonly AmazonDynamoDBClient DdbClient = new();
    private static readonly string TableName = Environment.GetEnvironmentVariable("ATTEMPTS_TABLE_NAME") ?? "Attempts";

    public static async Task Main(string[] args)
    {
        var handler = new Function();
        var serializer = new DefaultLambdaJsonSerializer();
        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>(handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(handlerWrapper);
        await bootstrap.RunAsync();
    }

    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<string, string>(Workflow, input, context);

    private async Task<string> Workflow(string input, IDurableContext context)
    {
        var executionId = context.ExecutionContext.DurableExecutionArn;

        var result = await context.StepAsync(
            async (stepContext, _ct) =>
            {
                // Atomically increment attempt counter in DynamoDB
                var response = await DdbClient.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = TableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["executionId"] = new AttributeValue { S = executionId }
                    },
                    UpdateExpression = "SET attemptCount = if_not_exists(attemptCount, :zero) + :inc",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":zero"] = new AttributeValue { N = "0" },
                        [":inc"] = new AttributeValue { N = "1" }
                    },
                    ReturnValues = ReturnValue.UPDATED_NEW
                });

                var attemptCount = int.Parse(response.Attributes["attemptCount"].N);

                if (attemptCount < 2)
                {
                    // Log input via durable step logger (structured record with
                    // durableExecutionArn — matched by the conformance runner).
                    stepContext.Logger.LogInformation("{Input}", input);
                    // First attempt: simulate Lambda crash
                    Environment.Exit(1);
                }
                // Second attempt (retry): log and succeed
                stepContext.Logger.LogInformation("{Input}", input);
                return "succeeded on second attempt";
            },
            config: new StepConfig
            {
                Semantics = StepSemantics.AtMostOncePerRetry,
                RetryStrategy = RetryStrategy.FromDelegate((error, attempts) =>
                {
                    if (attempts >= 3)
                        return RetryDecision.DoNotRetry();
                    return RetryDecision.RetryAfter(TimeSpan.FromSeconds(1));
                })
            });

        return result;
    }
}
