using Xunit;

using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.KinesisFirehoseEvents;
using Amazon.Lambda.TestUtilities;

using Newtonsoft.Json;



namespace BlueprintBaseName._1.Tests;

public class FunctionTest
{

    [Fact]
    public void TestKinesisFirehoseEvent()
    {
        var json = File.ReadAllText("sample-event.json");

        var kinesisEvent = JsonConvert.DeserializeObject<KinesisFirehoseEvent>(json);

        var function = new Function();
        var context = new TestLambdaContext();
        var kinesisResponse = function.FunctionHandler(kinesisEvent, context);

        Assert.Equal(1, kinesisResponse.Records.Count);
        Assert.Equal("49572672223665514422805246926656954630972486059535892482", kinesisResponse.Records[0].RecordId);
        Assert.Equal(KinesisFirehoseResponse.TRANSFORMED_STATE_OK, kinesisResponse.Records[0].Result);
        Assert.Equal("SEVMTE8gV09STEQ=", kinesisResponse.Records[0].Base64EncodedData);
    }
}