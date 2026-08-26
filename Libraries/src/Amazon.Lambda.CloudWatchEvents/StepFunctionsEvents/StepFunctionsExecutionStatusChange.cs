namespace Amazon.Lambda.CloudWatchEvents.StepFunctionsEvents
{
    /// <summary>
    /// This class represents the details of a Step Functions Execution Status Change sent via EventBridge.
    /// For more see - https://docs.aws.amazon.com/step-functions/latest/dg/eventbridge-integration.html#event-detail-execution-status-change
    /// </summary>
    public class StepFunctionsExecutionStatusChange
    {
        /// <summary>
        /// The Amazon Resource Name (ARN) that identifies the execution.
        /// </summary>
        public string ExecutionArn { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) that identifies the state machine.
        /// </summary>
        public string StateMachineArn { get; set; }

        /// <summary>
        /// The name of the execution.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The current status of the execution.
        /// Possible values are RUNNING, SUCCEEDED, FAILED, TIMED_OUT, ABORTED and PENDING_REDRIVE.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// The date the execution started, in Unix epoch milliseconds.
        /// </summary>
        public long StartDate { get; set; }

        /// <summary>
        /// The date the execution stopped, in Unix epoch milliseconds.
        /// This is null while the execution is still running.
        /// </summary>
        public long? StopDate { get; set; }

        /// <summary>
        /// The string that contains the JSON input data of the execution.
        /// This can be excluded when the combined escaped input and output exceeds the EventBridge payload quota.
        /// </summary>
        public string Input { get; set; }

        /// <summary>
        /// Provides details about the input, such as whether it was included in the event.
        /// </summary>
        public StepFunctionsExecutionDataDetails InputDetails { get; set; }

        /// <summary>
        /// The JSON output data of the execution.
        /// This is null while the execution is still running and can be excluded when it exceeds the EventBridge payload quota.
        /// </summary>
        public string Output { get; set; }

        /// <summary>
        /// Provides details about the output, such as whether it was included in the event.
        /// </summary>
        public StepFunctionsExecutionDataDetails OutputDetails { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) that identifies the state machine version associated with the execution.
        /// This is null if the execution was not associated with a version.
        /// </summary>
        public string StateMachineVersionArn { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) that identifies the state machine alias associated with the execution.
        /// This is null if the execution was not associated with an alias.
        /// </summary>
        public string StateMachineAliasArn { get; set; }

        /// <summary>
        /// The number of times the execution has been redriven.
        /// </summary>
        public int RedriveCount { get; set; }

        /// <summary>
        /// The date the execution was last redriven.
        /// This is null if the execution has not been redriven.
        /// </summary>
        public string RedriveDate { get; set; }

        /// <summary>
        /// Indicates whether the execution can be redriven.
        /// Possible values are NOT_REDRIVABLE, REDRIVABLE and REDRIVE_IN_PROGRESS.
        /// </summary>
        public string RedriveStatus { get; set; }

        /// <summary>
        /// Provides a reason for the value of the <see cref="RedriveStatus"/> field.
        /// </summary>
        public string RedriveStatusReason { get; set; }

        /// <summary>
        /// The error code of the failure, present when the status is FAILED or TIMED_OUT.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// A more detailed explanation of the cause of the failure, present when the status is FAILED or TIMED_OUT.
        /// </summary>
        public string Cause { get; set; }
    }

    /// <summary>
    /// Provides details about execution input or output, mirroring the Step Functions
    /// CloudWatchEventsExecutionDataDetails data type.
    /// For more see - https://docs.aws.amazon.com/step-functions/latest/apireference/API_CloudWatchEventsExecutionDataDetails.html
    /// </summary>
    public class StepFunctionsExecutionDataDetails
    {
        /// <summary>
        /// Indicates whether the input or output was included in the response.
        /// This is false when the data was truncated because it exceeded the EventBridge payload quota.
        /// </summary>
        public bool Included { get; set; }
    }
}
