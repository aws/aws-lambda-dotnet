namespace Amazon.Lambda.ConfigEvents
{
    using System;

    /// <summary>
    /// AWS Config event
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules.html
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules_example-events.html
    /// </summary>
    public class ConfigEvent
    {
        /// <summary>
        /// The ID of the AWS account that owns the rule.
        /// </summary>
        public string AccountId { get; set; }

        /// <summary>
        /// The ARN that AWS Config assigned to the rule.
        /// </summary>
        public string ConfigRuleArn { get; set; }

        /// <summary>
        /// The ID that AWS Config assigned to the rule.
        /// </summary>
        public string ConfigRuleId { get; set; }

        /// <summary>
        /// The name that you assigned to the rule that caused AWS Config
        /// to publish the event and invoke the function.
        /// </summary>
        public string ConfigRuleName { get; set; }

        /// <summary>
        /// A Boolean value that indicates whether the AWS resource to be
        /// evaluated has been removed from the rule's scope.
        /// </summary>
        public bool EventLeftScope { get; set; }

        /// <summary>
        /// The ARN of the IAM role that is assigned to AWS Config.
        /// </summary>
        public string ExecutionRoleArn { get; set; }

        /// <summary>
        /// If the event is published in response to a resource configuration
        /// change, the value for this attribute is a string that contains
        /// a JSON configuration item.
        /// </summary>
        public string InvokingEvent { get; set; }

        /// <summary>
        /// A token that the function must pass to AWS Config with the
        /// PutEvaluations call.
        /// </summary>
        public string ResultToken { get; set; }

        /// <summary>
        /// Key/value pairs that the function processes as part of its
        /// evaluation logic.
        /// </summary>
        public string RuleParameters { get; set; }

        /// <summary>
        /// A version number assigned by AWS.
        /// The version will increment if AWS adds attributes to AWS Config
        /// events.
        /// If a function requires an attribute that is only in events that
        /// match or exceed a specific version, then that function can check
        /// the value of this attribute.
        /// </summary>
        public string Version { get; set; }

    }
}
