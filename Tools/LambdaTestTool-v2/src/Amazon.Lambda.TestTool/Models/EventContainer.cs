using Amazon.Lambda.TestTool.Services;

namespace Amazon.Lambda.TestTool.Models;

public class EventContainer
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
    
    public EventContainer(RuntimeApiDataStore dataStore, int eventCount, string eventJson)
    {
        LastUpdated = DateTime.Now;
        _dataStore = dataStore;
        AwsRequestId = eventCount.ToString("D12");
        EventJson = eventJson;
    }

    public string FunctionArn
    {
        get => DefaultFunctionArn;
    }

    public void ReportSuccessResponse(string response)
    {
        LastUpdated = DateTime.Now;
        this.Response = response;
        this.EventStatus = Status.Success;
        _dataStore.RaiseStateChanged();
    }

    public void ReportErrorResponse(string errorType, string errorBody)
    {
        LastUpdated = DateTime.Now;
        this.ErrorType = errorType;
        this.ErrorResponse = errorBody;
        this.EventStatus = Status.Failure;
        _dataStore.RaiseStateChanged();
    }
}