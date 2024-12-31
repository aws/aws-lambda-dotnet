using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.TestTool.BlazorTester.Services
{
    public interface IRuntimeApiDataStore
    {
        EventContainer QueueEvent(string eventBody);
        
        IReadOnlyList<IEventContainer> QueuedEvents { get; }
        
        IReadOnlyList<IEventContainer> ExecutedEvents { get; }

        void ClearQueued();

        void ClearExecuted();

        void DeleteEvent(string awsRequestId);


        IEventContainer ActiveEvent { get; }

        event EventHandler StateChange;

        bool TryActivateEvent(out IEventContainer activeEvent);

        void ReportSuccess(string awsRequestId, string response);
        void ReportError(string awsRequestId, string errorType, string errorBody);
    }

    public class RuntimeApiDataStore : IRuntimeApiDataStore
    {
        private IList<EventContainer> _queuedEvents = new List<EventContainer>();
        private IList<EventContainer> _executedEvents = new List<EventContainer>();
        private int _eventCounter = 1;
        private object _lock = new object();
        
        public event EventHandler StateChange;
        
        public EventContainer QueueEvent(string eventBody)
        {
            var evnt = new EventContainer(this, _eventCounter++, eventBody);
            lock (_lock)
            {
                _queuedEvents.Add(evnt);
            }

            // Start a task that will cancel the event after a timeout or dispatch
            Task.Run(async () => {
                // Wait for the event to be dispatched or timeout
                try {
                    await evnt.DispatchedTCS.Task.WaitAsync(evnt.TimedOutCTS.Token);
                } catch (TaskCanceledException) {
                    // If the event was cancelled then it timed out
                    lock(_lock) {
                        _queuedEvents.Remove(evnt);
                    }
                    evnt.Cancel("Lambda throttled response error");
                    return;
                }

                // If the event was not cancelled then it was dispatched, and we're good
            });
            
            RaiseStateChanged();
            return evnt;
        }

        public bool TryActivateEvent(out IEventContainer activeEvent)
        {
            activeEvent = null;
            lock(_lock)
            {
                while (true) {
                    if (!_queuedEvents.Any())
                    {
                        return false;
                    }

                    var evnt = _queuedEvents[0];
                    _queuedEvents.RemoveAt(0);
                    if (evnt.MarkExecuting()) {
                        if (ActiveEvent != null)
                        {
                            _executedEvents.Add(ActiveEvent as EventContainer);
                        }
                        ActiveEvent = evnt;
                        activeEvent = ActiveEvent;
                        RaiseStateChanged();
                        return true;
                    }

                    // If we get here there was an event but it was already failed (timed out)
                    // Loop around and check if there are more events
                }
            }
        }
        
        public IEventContainer ActiveEvent { get; private set; }

        public IReadOnlyList<IEventContainer> QueuedEvents
        {
            get
            {
                lock(_lock)
                {
                    return new ReadOnlyCollection<EventContainer>(_queuedEvents.ToArray()); 
                }
            }
        }

        public IReadOnlyList<IEventContainer> ExecutedEvents
        {
            get
            {
                lock(_lock)
                {
                    return new ReadOnlyCollection<EventContainer>(_executedEvents.ToArray());
                }
            }
        }

        public void ReportSuccess(string awsRequestId, string response)
        {
            lock(_lock)
            {
                var evnt = FindEventContainer(awsRequestId);
                if (evnt == null)
                {
                    return;
                }
                
                evnt.ReportSuccessResponse(response);
            }
            RaiseStateChanged();
        }

        public void ReportError(string awsRequestId, string errorType, string errorBody)
        {
            lock(_lock)
            {
                var evnt = FindEventContainer(awsRequestId);
                if (evnt == null)
                {
                    return;
                }
                
                evnt.ReportErrorResponse(errorType, errorBody);
            }
            RaiseStateChanged();
        }

        public void ClearQueued()
        {
            lock(_lock)
            {
                this._queuedEvents.Clear();
            }
            RaiseStateChanged();
        }

        public void ClearExecuted()
        {
            lock(_lock)
            {
                this._executedEvents.Clear();
            }
            RaiseStateChanged();
        }

        public void DeleteEvent(string awsRequestId)
        {
            lock(_lock)
            {
                var executedEvent = this._executedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, awsRequestId));
                if (executedEvent != null)
                {
                    this._executedEvents.Remove(executedEvent);
                }
                else
                {
                    executedEvent = this._queuedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, awsRequestId));
                    if (executedEvent != null)
                    {
                        this._queuedEvents.Remove(executedEvent);
                    }
                }
            }
            RaiseStateChanged();
        }

        private EventContainer FindEventContainer(string awsRequestId)
        {
            if (string.Equals(this.ActiveEvent?.AwsRequestId, awsRequestId))
            {
                return this.ActiveEvent as EventContainer;
            }

            var evnt = _executedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, awsRequestId)) as EventContainer;
            if (evnt != null)
            {
                return evnt;
            }

            evnt = _queuedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, awsRequestId)) as EventContainer;

            return evnt;
        }

        internal void RaiseStateChanged()
        {
            var handler = StateChange;
            handler?.Invoke(this, EventArgs.Empty);
        }
    }

    public interface IEventContainer
    {
        public enum Status {Queued, Executing, Success, Failure}
        
        string AwsRequestId { get; }
        string EventJson { get; }
        string ErrorResponse { get; }
        string ErrorType { get; }
        
        string Response { get; }
        Status EventStatus { get; }
        
        string FunctionArn { get; }
        
        DateTime LastUpdated { get; }
    }

    public class EventContainer : IEventContainer
    {
        public Action OnSuccess { get; set; }

        public Action OnError { get; set; }

        private const string defaultFunctionArn = "arn:aws:lambda:us-west-2:123412341234:function:Function";
        public string AwsRequestId { get; }
        public string EventJson { get; }
        public string ErrorResponse { get; private set; }
        
        public string ErrorType { get; private set; }
        
        public string Response { get; private set; }
        
        public bool MarkExecuting()
        {
            lock(_statusLock){
                if (EventStatus == IEventContainer.Status.Queued)
                {
                    EventStatus = IEventContainer.Status.Executing;

                    // Mark that the event has been dispatched
                    DispatchedTCS.TrySetResult();
                    return true;
                }
            }
            return false;
        }

        public DateTime LastUpdated { get; private set; }

        public TaskCompletionSource DispatchedTCS { get; private set; } = new ();
        public CancellationTokenSource TimedOutCTS { get; private set; }
        private readonly object _statusLock = new();
        private IEventContainer.Status _status = IEventContainer.Status.Queued;
        public IEventContainer.Status EventStatus
        {
            get => _status;
            private set
            {
                _status = value;
                LastUpdated = DateTime.Now;
            }
        }

        private readonly RuntimeApiDataStore _dataStore;
        public EventContainer(RuntimeApiDataStore dataStore, int eventCount, string eventJson)
        {
            LastUpdated = DateTime.Now;
            this._dataStore = dataStore;
            this.AwsRequestId = eventCount.ToString("D12");
            this.EventJson = eventJson;
            // TODO: Parse the JSON so we can get the timeout value
            this.TimedOutCTS = new (1000);
        }

        public string FunctionArn
        {
            get => defaultFunctionArn;
        }

        public void ReportSuccessResponse(string response)
        {
            LastUpdated = DateTime.Now;
            lock (_statusLock) {
                if (this.EventStatus == IEventContainer.Status.Executing) {
                    this.EventStatus = IEventContainer.Status.Success;
                    this.Response = response;
                    OnSuccess?.Invoke();
                    _dataStore.RaiseStateChanged();
                }
            }
        }
        
        public void ReportErrorResponse(string errorType, string errorBody)
        {
            LastUpdated = DateTime.Now;
            lock(_statusLock) {
                if (EventStatus == IEventContainer.Status.Queued || this.EventStatus == IEventContainer.Status.Executing) {
                    this.ErrorType = errorType;
                    this.ErrorResponse = errorBody;
                    this.EventStatus = IEventContainer.Status.Failure;
                    OnError?.Invoke();
                    _dataStore.RaiseStateChanged();
                }
            }
        }

        public void Cancel(string errorBody)
        {
            lock(_statusLock) {
                if (EventStatus == IEventContainer.Status.Queued)
                {
                    ReportErrorResponse("Throttled", errorBody);
                }
                else
                {
                    ReportErrorResponse("Failed", errorBody);
                }
            }
        }
    }
}