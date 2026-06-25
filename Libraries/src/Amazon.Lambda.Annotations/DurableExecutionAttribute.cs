using System;
using System.Collections.Generic;

namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// Marks a Lambda function method (also annotated with <see cref="LambdaFunctionAttribute"/>) as a
    /// durable execution workflow. The Amazon.Lambda.Annotations source generator recognizes this
    /// attribute and generates a handler wrapper that delegates to
    /// <c>Amazon.Lambda.DurableExecution.DurableFunction.WrapAsync</c>, along with the corresponding
    /// <c>DurableConfig</c> and checkpoint-API IAM permissions in the generated CloudFormation/SAM template.
    /// </summary>
    /// <remarks>
    /// The annotated method must have the signature <c>(TInput, IDurableContext) -&gt; Task</c> or
    /// <c>(TInput, IDurableContext) -&gt; Task&lt;TOutput&gt;</c>. Durable functions are supported in both the
    /// executable and class-library programming models on the managed runtime.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DurableExecutionAttribute : Attribute
    {
        // Service-enforced bounds for DurableConfig, mirrored here so a misconfiguration is caught at build
        // time instead of being rejected at deploy time. Kept in sync with the AWSSDK.Lambda DurableConfig
        // model ([AWSProperty(Min/Max)] on RetentionPeriodInDays / ExecutionTimeout).
        private const int MaxRetentionPeriodInDays = 90;
        private const int MaxExecutionTimeoutInSeconds = 31622400;

        private int _retentionPeriodInDays;

        /// <summary>
        /// The number of days the durable execution's history is retained after completion.
        /// Maps to <c>DurableConfig.RetentionPeriodInDays</c> on the generated function resource.
        /// When unset, the property is omitted from the template and the service default applies.
        /// </summary>
        public int RetentionPeriodInDays
        {
            get => _retentionPeriodInDays;
            set
            {
                _retentionPeriodInDays = value;
                IsRetentionPeriodInDaysSet = true;
            }
        }

        /// <summary>
        /// Indicates whether <see cref="RetentionPeriodInDays"/> was explicitly set.
        /// </summary>
        internal bool IsRetentionPeriodInDaysSet { get; private set; }

        private int _executionTimeout;

        /// <summary>
        /// The maximum duration, in seconds, that a single durable execution may run.
        /// Maps to <c>DurableConfig.ExecutionTimeout</c> on the generated function resource.
        /// When unset, the property is omitted from the template and the service default applies.
        /// </summary>
        public int ExecutionTimeout
        {
            get => _executionTimeout;
            set
            {
                _executionTimeout = value;
                IsExecutionTimeoutSet = true;
            }
        }

        /// <summary>
        /// Indicates whether <see cref="ExecutionTimeout"/> was explicitly set.
        /// </summary>
        internal bool IsExecutionTimeoutSet { get; private set; }

        /// <summary>
        /// Validates the attribute's property values.
        /// </summary>
        /// <returns>A list of validation error messages; empty when the attribute is valid.</returns>
        internal List<string> Validate()
        {
            var validationErrors = new List<string>();

            if (IsRetentionPeriodInDaysSet && (RetentionPeriodInDays < 1 || RetentionPeriodInDays > MaxRetentionPeriodInDays))
            {
                validationErrors.Add($"{nameof(RetentionPeriodInDays)} = {RetentionPeriodInDays}. It must be between 1 and {MaxRetentionPeriodInDays}.");
            }

            if (IsExecutionTimeoutSet && (ExecutionTimeout < 1 || ExecutionTimeout > MaxExecutionTimeoutInSeconds))
            {
                validationErrors.Add($"{nameof(ExecutionTimeout)} = {ExecutionTimeout}. It must be between 1 and {MaxExecutionTimeoutInSeconds}.");
            }

            return validationErrors;
        }
    }
}
