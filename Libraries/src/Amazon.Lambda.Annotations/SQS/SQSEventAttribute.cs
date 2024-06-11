using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Amazon.Lambda.Annotations.SQS
{
    /// <summary>
    /// This attribute defines the SQS event source configuration for a Lambda function.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class SQSEventAttribute : Attribute
    {
        // Except for Queue all other properties are optional.
        // .NET attributes cannot be nullable. To work around this, we have added nullable backing fields to all optional properties and added an internal Is<PropertyName>Set method to identify which properties were explicitly set the customer.
        // These internal methods are used by the CloudFormationWriter while deciding which properties to write in the CF template.

        // Only allow alphanumeric characters
        private static readonly Regex _resourceNameRegex = new Regex("^[a-zA-Z0-9]+$");

        /// <summary>
        /// The SQS queue that will act as the event trigger for the Lambda function.
        /// This can either be the queue ARN or reference to the SQS queue resource that is already defined in the serverless template.
        /// To reference a SQS queue resource in the serverless template, prefix the resource name with "@" symbol.
        /// </summary>
        public string Queue { get; set; }

        /// <summary>
        /// The CloudFormation resource name for the SQS event source mapping. By default this is set to the SQS queue name if the <see cref="Queue"/> is set to an SQS queue ARN.
        /// If <see cref="Queue"/> is set to an existing CloudFormation resource, than that is used as the default value without the "@" prefix.
        /// </summary>
        public string ResourceName
        {
            get 
            { 
                if (IsResourceNameSet)
                {
                    return resourceName;
                }
                if (Queue.StartsWith("@"))
                {
                    return Queue.Substring(1);
                }

                var arnTokens = Queue.Split(new char[] { ':' }, 6);
                var queueName = arnTokens[5];
                var sanitizedQueueName = string.Join(string.Empty, queueName.Where(char.IsLetterOrDigit));
                return sanitizedQueueName;
            }
            set => resourceName = value;
        }

        private string resourceName { get; set; } = null;
        internal bool IsResourceNameSet => resourceName != null;


        /// <summary>
        /// If set to false, the event source mapping will be disabled and message polling will be paused.
        /// Default value is true.
        /// </summary>
        public bool Enabled
        {
            get => enabled.GetValueOrDefault();
            set => enabled = value;
        }
        private bool? enabled { get; set; }
        internal bool IsEnabledSet => enabled.HasValue;

        /// <summary>
        /// The maximum number of messages that will be sent for processing in a single batch. 
        /// This value must be between 1 to 10000. Default value is 10.
        /// </summary>
        public uint BatchSize
        {
            get => batchSize.GetValueOrDefault();
            set => batchSize = value;
        }
        private uint? batchSize { get; set; }
        internal bool IsBatchSizeSet => batchSize.HasValue;

        /// <summary>
        /// The maximum amount of time, in seconds, to gather records before invoking the function. 
        /// This value must be between 0 to 300. Default value is 0.
        /// </summary>
        public uint MaximumBatchingWindowInSeconds 
        {
            get => maximumBatchingWindowInSeconds.GetValueOrDefault();
            set => maximumBatchingWindowInSeconds = value;
        }
        private uint? maximumBatchingWindowInSeconds { get; set; }
        internal bool IsMaximumBatchingWindowInSecondsSet => maximumBatchingWindowInSeconds.HasValue;

        /// <summary>
        /// A collection of semicolon (;) separated strings where each string denotes a pattern. 
        /// Only those SQS messages that conform to at least 1 pattern will be forwarded to the Lambda function for processing. 
        /// </summary>
        public string Filters { get; set; } = null;
        internal bool IsFiltersSet  => Filters != null;

        /// <summary>
        /// The maximum number of concurrent Lambda invocations that the SQS queue can trigger.
        /// This value must be between 2 to 1000. The default value is 1000.
        /// </summary>
        public uint MaximumConcurrency
        { 
            get => maximumConcurrency.GetValueOrDefault();
            set => maximumConcurrency = value;
        }
        private uint? maximumConcurrency { get; set; }
        internal bool IsMaximumConcurrencySet => maximumConcurrency.HasValue;

        /// <summary>
        /// Creates an instance of the <see cref="SQSEventAttribute"/> class.
        /// </summary>
        /// <param name="queue"><see cref="Queue"/> property"/></param>
        public SQSEventAttribute(string queue)
        {
            Queue = queue;
        }

        internal List<string> Validate()
        {
            var validationErrors = new List<string>();

            if (IsBatchSizeSet && (BatchSize < 1 || BatchSize > 10000))
            {
                validationErrors.Add($"{nameof(SQSEventAttribute.BatchSize)} = {BatchSize}. It must be between 1 and 10000");
            }
            if (IsMaximumConcurrencySet && (MaximumConcurrency < 2 || MaximumConcurrency > 1000))
            {
                validationErrors.Add($"{nameof(SQSEventAttribute.MaximumConcurrency)} = {MaximumConcurrency}. It must be between 2 and 1000");
            }
            if (IsMaximumBatchingWindowInSecondsSet && (MaximumBatchingWindowInSeconds < 0 || MaximumBatchingWindowInSeconds > 300))
            {
                validationErrors.Add($"{nameof(SQSEventAttribute.MaximumBatchingWindowInSeconds)} = {MaximumBatchingWindowInSeconds}. It must be between 0 and 300");
            }
            if (!Queue.StartsWith("@"))
            {
                var arnTokens = Queue.Split(new char[] { ':' }, 6);
                if (arnTokens.Length != 6) 
                {
                    validationErrors.Add($"{nameof(SQSEventAttribute.Queue)} = {Queue}. The SQS queue ARN is invalid. The ARN format is 'arn:<partition>:sqs:<region>:<account-id>:<queue-name>'");
                }
            }
            if (IsResourceNameSet && !_resourceNameRegex.IsMatch(ResourceName))
            {
                validationErrors.Add($"{nameof(SQSEventAttribute.ResourceName)} = {ResourceName}. It must only contain alphanumeric characters and must not be an empty string");
            }

            return validationErrors;
        }
    }
}
