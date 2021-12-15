using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.LexEvents;

using Newtonsoft.Json;

using BlueprintBaseName._1;

namespace BlueprintBaseName._1.Tests
{
    public class FunctionTest
    {
        [Fact]
        public void StartBookACarEventTest()
        {
            var json = File.ReadAllText("start-book-a-car-event.json");

            var lexEvent = JsonConvert.DeserializeObject<LexEvent>(json);

            var function = new Function();
            var context = new TestLambdaContext();
            var response = function.FunctionHandler(lexEvent, context);

            Assert.Equal("Delegate", response.DialogAction.Type);
        }

        [Fact]
        public void DriverAgeTooYoungEventTest()
        {
            var json = File.ReadAllText("driver-age-too-young.json");

            var lexEvent = JsonConvert.DeserializeObject<LexEvent>(json);

            var function = new Function();
            var context = new TestLambdaContext();
            var response = function.FunctionHandler(lexEvent, context);

            Assert.Equal("ElicitSlot", response.DialogAction.Type);
            Assert.Equal("DriverAge", response.DialogAction.SlotToElicit);
            Assert.Equal("PlainText", response.DialogAction.Message.ContentType);
            Assert.Equal("Your driver must be at least eighteen to rent a car.  Can you provide the age of a different driver?", response.DialogAction.Message.Content);
        }

        [Fact]
        public void CommitBookACarEventTest()
        {
            var json = File.ReadAllText("commit-book-a-car.json");

            var lexEvent = JsonConvert.DeserializeObject<LexEvent>(json);

            var function = new Function();
            var context = new TestLambdaContext();
            var response = function.FunctionHandler(lexEvent, context);

            Assert.Equal("Close", response.DialogAction.Type);
            Assert.Equal("Fulfilled", response.DialogAction.FulfillmentState);
            Assert.Equal("PlainText", response.DialogAction.Message.ContentType);
            Assert.Equal("Thanks, I have placed your reservation.", response.DialogAction.Message.Content);
        }
    }
}
