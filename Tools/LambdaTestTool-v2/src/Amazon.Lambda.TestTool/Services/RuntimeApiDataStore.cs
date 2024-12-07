using Amazon.Lambda.TestTool.Models;
using System.Collections.ObjectModel;

namespace Amazon.Lambda.TestTool.Services;

public interface IRuntimeApiDataStore
{
    EventContainer QueueEvent(string eventBody);
    
    IReadOnlyList<EventContainer> QueuedEvents { get; }
    
    IReadOnlyList<EventContainer> ExecutedEvents { get; }

    void ClearQueued();

    void ClearExecuted();

    void DeleteEvent(string awsRequestId);


    EventContainer? ActiveEvent { get; }

    event EventHandler? StateChange;

    bool TryActivateEvent(out EventContainer? activeEvent);

    void ReportSuccess(string awsRequestId, string response);
    void ReportError(string awsRequestId, string errorType, string errorBody);
}

public class RuntimeApiDataStore : IRuntimeApiDataStore
{
    private IList<EventContainer> _queuedEvents = new List<EventContainer>();
    private IList<EventContainer> _executedEvents = new List<EventContainer>();
    private int _eventCounter = 1;
    private object _lock = new object();
    
    public event EventHandler? StateChange;
    
    public EventContainer QueueEvent(string eventBody)
    {
        var evnt = new EventContainer(this, _eventCounter++, eventBody);
        lock (_lock)
        {
            _queuedEvents.Add(evnt);
        }
        
        RaiseStateChanged();
        return evnt;
    }

    public bool TryActivateEvent(out EventContainer? activeEvent)
    {
        activeEvent = null;
        lock(_lock)
        {
            if (!_queuedEvents.Any())
            {
                return false;
            }

            // Grab the next event from the queue
            var evnt = _queuedEvents[0];
            _queuedEvents.RemoveAt(0);
            evnt.EventStatus = EventContainer.Status.Executing;

            // Move current active event to the executed list.
            if (ActiveEvent != null)
            {
                _executedEvents.Add(ActiveEvent);
            }

            // Make the event pull from the queue active
            ActiveEvent = evnt;
            activeEvent = ActiveEvent;
            RaiseStateChanged();
            return true;
        }
    }
    
    public EventContainer? ActiveEvent { get; private set; }

    public IReadOnlyList<EventContainer> QueuedEvents
    {
        get
        {
            lock(_lock)
            {
                return new ReadOnlyCollection<EventContainer>(_queuedEvents.ToArray()); 
            }
        }
    }

    public IReadOnlyList<EventContainer> ExecutedEvents
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

    private EventContainer? FindEventContainer(string awsRequestId)
    {
        if (string.Equals(this.ActiveEvent?.AwsRequestId, awsRequestId))
        {
            return this.ActiveEvent;
        }

        var evnt = _executedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, awsRequestId));
        if (evnt != null)
        {
            return evnt;
        }

        evnt = _queuedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, awsRequestId));

        return evnt;
    }

    internal void RaiseStateChanged()
    {
        var handler = StateChange;
        handler?.Invoke(this, EventArgs.Empty);
    }
}