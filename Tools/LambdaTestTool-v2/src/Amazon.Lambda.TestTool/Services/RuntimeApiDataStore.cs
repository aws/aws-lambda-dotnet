// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;
using System.Collections.ObjectModel;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// The runtime API data store is used to hold the queued, executed and active Lambda events for Lambda function.
/// </summary>
public interface IRuntimeApiDataStore
{
    /// <summary>
    /// Queue the event for a Lambda function to process.
    /// </summary>
    /// <param name="eventBody">The Lambda event body.</param>
    /// <param name="isRequestResponseMode">If the event is for a request response mode thread syncronization code will be activated to all the response to be return for the request.</param>
    /// <returns></returns>
    EventContainer QueueEvent(string eventBody, bool isRequestResponseMode);

    /// <summary>
    /// The list of queued events.
    /// </summary>
    IReadOnlyList<EventContainer> QueuedEvents { get; }

    /// <summary>
    /// The list of executed events.
    /// </summary>
    IReadOnlyList<EventContainer> ExecutedEvents { get; }

    /// <summary>
    /// Clear the list of queued events.
    /// </summary>
    void ClearQueued();

    /// <summary>
    /// Clear the list of executed events.
    /// </summary>
    void ClearExecuted();

    void DeleteEvent(string awsRequestId);

    /// <summary>
    /// The active event a Lambda function has pulled from the queue and is currently processing.
    /// </summary>
    EventContainer? ActiveEvent { get; }

    /// <summary>
    /// An event that some event or event collection has changed. This is used by the UI to
    /// know when it should refresh.
    /// </summary>
    event EventHandler? StateChange;

    /// <summary>
    /// Try to activate an event by grabbing the latest event from the queue.
    /// </summary>
    /// <param name="activeEvent"></param>
    /// <returns></returns>
    bool TryActivateEvent(out EventContainer? activeEvent);

    /// <summary>
    /// Report the event was successfully processed. Used by the Lambda Runtime API when it gets
    /// notification from the Lambda function.
    /// </summary>
    /// <param name="awsRequestId"></param>
    /// <param name="response"></param>
    void ReportSuccess(string awsRequestId, string response);

    /// <summary>
    /// Report the processing the event failed. Used by the Lambda Runtime API when it gets
    /// notification from the Lambda function.
    /// </summary>
    /// <param name="awsRequestId"></param>
    /// <param name="response"></param>
    void ReportError(string awsRequestId, string errorType, string errorBody);
}

/// <inheritdoc/>
public class RuntimeApiDataStore : IRuntimeApiDataStore
{
    private readonly IList<EventContainer> _queuedEvents = new List<EventContainer>();
    private readonly IList<EventContainer> _executedEvents = new List<EventContainer>();
    private int _eventCounter = 1;
    private readonly object _lock = new object();

    /// <inheritdoc/>
    public event EventHandler? StateChange;

    /// <inheritdoc/>
    public EventContainer QueueEvent(string eventBody, bool isRequestResponseMode)
    {
        var evnt = new EventContainer(this, _eventCounter++, eventBody, isRequestResponseMode);
        lock (_lock)
        {
            _queuedEvents.Add(evnt);
        }

        RaiseStateChanged();
        return evnt;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public EventContainer? ActiveEvent { get; private set; }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void ClearQueued()
    {
        lock(_lock)
        {
            _queuedEvents.Clear();
        }
        RaiseStateChanged();
    }

    /// <inheritdoc/>
    public void ClearExecuted()
    {
        lock(_lock)
        {
            _executedEvents.Clear();
        }
        RaiseStateChanged();
    }

    /// <inheritdoc/>
    public void DeleteEvent(string awsRequestId)
    {
        lock(_lock)
        {
            var executedEvent = _executedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, awsRequestId));
            if (executedEvent != null)
            {
                _executedEvents.Remove(executedEvent);
            }
            else
            {
                executedEvent = _queuedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, awsRequestId));
                if (executedEvent != null)
                {
                    _queuedEvents.Remove(executedEvent);
                }
            }
        }
        RaiseStateChanged();
    }

    private EventContainer? FindEventContainer(string awsRequestId)
    {
        if (string.Equals(ActiveEvent?.AwsRequestId, awsRequestId))
        {
            return ActiveEvent;
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
