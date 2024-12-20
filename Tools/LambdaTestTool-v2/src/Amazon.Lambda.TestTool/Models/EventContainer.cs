// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Services;

namespace Amazon.Lambda.TestTool.Models;

public class EventContainer : IDisposable
{
    public enum Status { Queued, Executing, Success, Failure }

    private const string DefaultFunctionArn = "arn:aws:lambda:us-west-2:123412341234:function:Function";
    public string AwsRequestId { get; }
    public string EventJson { get; }
    public string? ErrorResponse { get; private set; }

    public string? ErrorType { get; private set; }

    public string? Response { get; private set; }

    public DateTime LastUpdated { get; private set; }

    private Status _status = Status.Queued;

    private ManualResetEventSlim? _resetEvent;

    public Status EventStatus
    {
        get => _status;
        set
        {
            _status = value;
            LastUpdated = DateTime.Now;
        }
    }

    private readonly RuntimeApiDataStore _dataStore;

    public EventContainer(RuntimeApiDataStore dataStore, int eventCount, string eventJson, bool isRequestResponseMode)
    {
        LastUpdated = DateTime.Now;
        _dataStore = dataStore;
        AwsRequestId = eventCount.ToString("D12");
        EventJson = eventJson;

        if (isRequestResponseMode)
        {
            _resetEvent = new ManualResetEventSlim(false);
        }
    }

    public string FunctionArn
    {
        get => DefaultFunctionArn;
    }

    public void ReportSuccessResponse(string response)
    {
        LastUpdated = DateTime.Now;
        Response = response;
        EventStatus = Status.Success;

        if (_resetEvent != null)
        {
            _resetEvent.Set();
        }

        _dataStore.RaiseStateChanged();
    }

    public void ReportErrorResponse(string errorType, string errorBody)
    {
        LastUpdated = DateTime.Now;
        ErrorType = errorType;
        ErrorResponse = errorBody;
        EventStatus = Status.Failure;

        if (_resetEvent != null)
        {
            _resetEvent.Set();
        }

        _dataStore.RaiseStateChanged();
    }

    public bool WaitForCompletion()
    {
        if (_resetEvent == null)
        {
            return false;
        }

        // The 15 minutes is a fail safe so we at some point we unblock the thread. It is intentionally
        // long to give the user time to debug the Lambda function.
        return _resetEvent.Wait(TimeSpan.FromMinutes(15));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private bool _disposed = false;
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            if (_resetEvent != null)
            {
                _resetEvent.Dispose();
                _resetEvent = null;
            }
        }

        _disposed = true;
    }
}
