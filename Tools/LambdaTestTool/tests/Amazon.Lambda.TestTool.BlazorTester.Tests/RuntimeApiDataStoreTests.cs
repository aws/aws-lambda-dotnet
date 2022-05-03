using Amazon.Lambda.TestTool.BlazorTester.Services;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Amazon.Lambda.TestTool.BlazorTester.Tests
{
    public class RuntimeApiDataStoreTests
    {
        [Fact]
        public void ActivateEvents()
        {
            var dataStore = new RuntimeApiDataStore();

            IEventContainer evnt;
            Assert.False(dataStore.TryActivateEvent(out evnt));
            Assert.Empty(dataStore.QueuedEvents);

            dataStore.QueueEvent("{}");
            Assert.Single(dataStore.QueuedEvents);
            Assert.Equal(IEventContainer.Status.Queued, dataStore.QueuedEvents[0].EventStatus);

            Assert.True(dataStore.TryActivateEvent(out evnt));
            Assert.Equal("{}", evnt.EventJson);
            Assert.Equal(evnt, dataStore.ActiveEvent);
            Assert.Equal(IEventContainer.Status.Executing, dataStore.ActiveEvent.EventStatus);
            Assert.Equal("{}", dataStore.ActiveEvent.EventJson);
        }

        [Fact]
        public void EnsureRequestIdsAreDifferent()
        {
            var dataStore = new RuntimeApiDataStore();
            for(int i = 0; i< 10; i++)
            {
                dataStore.QueueEvent("{}");
            }
            Assert.Equal(10, dataStore.QueuedEvents.Count);

            var requestIds = new HashSet<string>();
            for(int i = 0;i < 10;i++)
            {
                Assert.True(dataStore.TryActivateEvent(out var evnt));
                Assert.DoesNotContain(evnt.AwsRequestId, requestIds);
                requestIds.Add(evnt.AwsRequestId);
            }

            // This is 9 because the 10th event is the active event.
            Assert.Equal(9, dataStore.ExecutedEvents.Count);
        }

        [Fact]
        public void ReportSuccess()
        {
            var dataStore = new RuntimeApiDataStore();
            dataStore.QueueEvent("{}");
            Assert.Equal(IEventContainer.Status.Queued, dataStore.QueuedEvents[0].EventStatus);

            Assert.True(dataStore.TryActivateEvent(out var evnt));

            dataStore.ReportSuccess(evnt.AwsRequestId, "\"Success\"");

            Assert.Equal(IEventContainer.Status.Success, dataStore.ActiveEvent.EventStatus);
            Assert.Equal("\"Success\"", dataStore.ActiveEvent.Response);
        }

        [Fact]
        public void ReportError()
        {
            var dataStore = new RuntimeApiDataStore();
            dataStore.QueueEvent("{}");
            Assert.Equal(IEventContainer.Status.Queued, dataStore.QueuedEvents[0].EventStatus);

            Assert.True(dataStore.TryActivateEvent(out var evnt));

            dataStore.ReportError(evnt.AwsRequestId, "BadError", "\"YouFail\"");

            Assert.Equal(IEventContainer.Status.Failure, dataStore.ActiveEvent.EventStatus);
            Assert.Equal("BadError", dataStore.ActiveEvent.ErrorType);
            Assert.Equal("\"YouFail\"", dataStore.ActiveEvent.ErrorResponse);
        }

        [Fact]
        public void ClearQueue()
        {
            var dataStore = new RuntimeApiDataStore();
            for (int i = 0; i < 10; i++)
            {
                dataStore.QueueEvent("{}");
            }
            Assert.Equal(10, dataStore.QueuedEvents.Count);

            dataStore.ClearQueued();
            Assert.Empty(dataStore.QueuedEvents);
        }

        [Fact]
        public void ClearExecuted()
        {
            var dataStore = new RuntimeApiDataStore();
            for (int i = 0; i < 10; i++)
            {
                dataStore.QueueEvent("{}");
            }
            Assert.Equal(10, dataStore.QueuedEvents.Count);

            for (int i = 0; i < 10; i++)
            {
                Assert.True(dataStore.TryActivateEvent(out _));
            }

            // This is 9 because the 10th event is the active event.
            Assert.Equal(9, dataStore.ExecutedEvents.Count);
            dataStore.ClearExecuted();
            Assert.Empty(dataStore.ExecutedEvents);
        }

        [Fact]
        public void DeleteEvent()
        {
            var dataStore = new RuntimeApiDataStore();
            for (int i = 0; i < 10; i++)
            {
                dataStore.QueueEvent("{}");
            }

            for (int i = 0; i < 5; i++)
            {
                Assert.True(dataStore.TryActivateEvent(out _));
            }

            Assert.Equal(5, dataStore.QueuedEvents.Count);
            Assert.Equal(4, dataStore.ExecutedEvents.Count);

            var deletedQueueEvent = dataStore.QueuedEvents[1];
            dataStore.DeleteEvent(deletedQueueEvent.AwsRequestId);

            Assert.Equal(4, dataStore.QueuedEvents.Count);
            Assert.Equal(4, dataStore.ExecutedEvents.Count);
            Assert.Null(dataStore.QueuedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, deletedQueueEvent.AwsRequestId)));

            var deletedExecutedEvent = dataStore.ExecutedEvents[1];
            dataStore.DeleteEvent(deletedExecutedEvent.AwsRequestId);
            Assert.Equal(4, dataStore.QueuedEvents.Count);
            Assert.Equal(3, dataStore.ExecutedEvents.Count);
            Assert.Null(dataStore.ExecutedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, deletedExecutedEvent.AwsRequestId)));

            dataStore.DeleteEvent("does-not-exist");
            Assert.Equal(4, dataStore.QueuedEvents.Count);
            Assert.Equal(3, dataStore.ExecutedEvents.Count);
        }
    }
}