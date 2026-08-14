// 3-12: Child context interrupted and re-executed
// Uses DynamoDB to track attempts; first invocation is interrupted, second succeeds
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ChildInterrupted;

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

        var result = await context.RunInChildContextAsync(async (childContext, _ct) =>
        {
            var stepResult = await childContext.StepAsync(
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

                    if (attemptCount < 2)
                    {
                        await Task.Delay(1000);
                        Environment.Exit(1);
                    }

                    return input;
                });

            return stepResult;
        }, config: new ChildContextConfig { SubType = "RunInChildContext" });

        return result;
    }
}
