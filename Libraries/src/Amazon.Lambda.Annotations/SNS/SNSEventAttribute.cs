using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Amazon.Lambda.Annotations.SNS
{
    /// <summary>
    /// This attribute defines the SNS event source configuration for a Lambda function.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class SNSEventAttribute : Attribute
    {
        private static readonly Regex _resourceNameRegex = new Regex("^[a-zA-Z0-9]+$");

        /// <summary>
        /// The SNS topic that will act as the event trigger for the Lambda function.
        /// This can either be the topic ARN or reference to the SNS topic resource that is already defined in the serverless template.
        /// To reference an SNS topic resource in the serverless template, prefix the resource name with "@" symbol.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// The CloudFormation resource name for the SNS event. By default this is set to the SNS topic name if the <see cref="Topic"/> is set to an SNS topic ARN.
        /// If <see cref="Topic"/> is set to an existing CloudFormation resource, than that is used as the default value without the "@" prefix.
        /// </summary>
        public string ResourceName
        {
            get
            {
                if (IsResourceNameSet)
                {
                    return resourceName;
                }
                if (string.IsNullOrEmpty(Topic))
                {
                    return string.Empty;
                }
                if (Topic.StartsWith("@"))
                {
                    return Topic.Substring(1);
                }

                var arnTokens = Topic.Split(new char[] { ':' }, 6);
                if (arnTokens.Length < 6)
                {
                    return Topic;
                }
                var topicName = arnTokens[5];
                var sanitizedTopicName = string.Join(string.Empty, topicName.Where(char.IsLetterOrDigit));
                return sanitizedTopicName;
            }
            set => resourceName = value;
        }

        private string resourceName { get; set; } = null;
        internal bool IsResourceNameSet => resourceName != null;

        /// <summary>
        /// A JSON filter policy that is applied to the SNS subscription.
        /// Only messages matching the filter policy will be delivered to the Lambda function.
        /// </summary>
        public string FilterPolicy { get; set; } = null;
        internal bool IsFilterPolicySet => FilterPolicy != null;

        /// <summary>
        /// If set to false, the event source will be disabled.
        /// Default value is true.
        /// </summary>
        public bool Enabled
        {
            get => enabled.GetValueOrDefault(true);
            set => enabled = value;
        }
        private bool? enabled { get; set; }
        internal bool IsEnabledSet => enabled.HasValue;

        /// <summary>
        /// Creates an instance of the <see cref="SNSEventAttribute"/> class.
        /// </summary>
        /// <param name="topic"><see cref="Topic"/> property</param>
        public SNSEventAttribute(string topic)
        {
            Topic = topic;
        }

        internal List<string> Validate()
        {
            var validationErrors = new List<string>();

            if (string.IsNullOrEmpty(Topic))
            {
                validationErrors.Add($"{nameof(SNSEventAttribute.Topic)} is required and must not be null or empty");
                return validationErrors;
            }

            if (!Topic.StartsWith("@"))
            {
                var arnTokens = Topic.Split(new char[] { ':' }, 6);
                if (arnTokens.Length != 6)
                {
                    validationErrors.Add($"{nameof(SNSEventAttribute.Topic)} = {Topic}. The SNS topic ARN is invalid. The ARN format is 'arn:<partition>:sns:<region>:<account-id>:<topic-name>'");
                }
            }
            if (IsResourceNameSet && !_resourceNameRegex.IsMatch(ResourceName))
            {
                validationErrors.Add($"{nameof(SNSEventAttribute.ResourceName)} = {ResourceName}. It must only contain alphanumeric characters and must not be an empty string");
            }

            return validationErrors;
        }
    }
}
