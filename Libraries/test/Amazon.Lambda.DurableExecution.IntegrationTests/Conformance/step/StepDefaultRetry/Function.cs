// 1-13: Default retry strategy (uses DynamoDB to track attempts)
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace StepDefaultRetry;

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
        => DurableFunction.WrapAsync<object?, string>(Workflow, input, context);

    private async Task<string> Workflow(object? input, IDurableContext context)
    {
        var executionId = context.ExecutionContext.DurableExecutionArn;

        // Step with no explicit retry config — uses SDK default
        var result = await context.StepAsync(
            async (_, _ct) =>
            {
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

                if (attemptCount < 3)
                {
                    throw new InvalidOperationException($"Attempt {attemptCount} failed");
                }
                return "recovered";
            },
            config: new StepConfig
            {
                RetryStrategy = RetryStrategy.Default
            });

        return result;
    }
}
