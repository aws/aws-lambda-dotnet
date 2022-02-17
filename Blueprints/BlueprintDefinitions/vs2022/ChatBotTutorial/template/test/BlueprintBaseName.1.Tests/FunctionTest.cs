using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.LexEvents;

using Newtonsoft.Json;

namespace BlueprintBaseName._1.Tests;

public class FunctionTest
{
    [Fact]
    public void StartOrderingFlowersEventTest()
    {
        var json = File.ReadAllText("start-order-flowers-event.json");

        var lexEvent = JsonConvert.DeserializeObject<LexEvent>(json);

        var function = new Function();
        var context = new TestLambdaContext();
        var response = function.FunctionHandler(lexEvent, context);

        Assert.Equal("Delegate", response.DialogAction.Type);
    }

    [Fact]
    public void CommitOrderingFlowersEventTest()
    {
        var json = File.ReadAllText("commit-order-flowers-event.json");

        var lexEvent = JsonConvert.DeserializeObject<LexEvent>(json);

        var function = new Function();
        var context = new TestLambdaContext();
        var response = function.FunctionHandler(lexEvent, context);

        Assert.Equal("Close", response.DialogAction.Type);
        Assert.Equal("Fulfilled", response.DialogAction.FulfillmentState);
        Assert.Equal("PlainText", response.DialogAction.Message.ContentType);
        Assert.Contains("Thanks, your order for Roses has been placed", response.DialogAction.Message.Content);
    }
}