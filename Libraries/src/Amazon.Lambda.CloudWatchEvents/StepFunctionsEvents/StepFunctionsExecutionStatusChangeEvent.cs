namespace Amazon.Lambda.CloudWatchEvents.StepFunctionsEvents
{
    /// <summary>
    /// This class represents a Step Functions Execution Status Change sent via EventBridge.
    /// For more see - https://docs.aws.amazon.com/step-functions/latest/dg/eventbridge-integration.html
    /// </summary>
    public class StepFunctionsExecutionStatusChangeEvent : CloudWatchEvent<StepFunctionsExecutionStatusChange> { }
}
