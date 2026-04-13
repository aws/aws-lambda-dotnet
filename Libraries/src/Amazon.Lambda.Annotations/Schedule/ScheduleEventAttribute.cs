// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Amazon.Lambda.Annotations.Schedule
{
    /// <summary>
    /// This attribute defines the Schedule event source configuration for a Lambda function.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ScheduleEventAttribute : Attribute
    {
        private static readonly Regex _resourceNameRegex = new Regex("^[a-zA-Z0-9]+$");

        /// <summary>
        /// The schedule expression. Supports rate and cron expressions.
        /// Examples: "rate(5 minutes)", "cron(0 12 * * ? *)"
        /// </summary>
        public string Schedule { get; set; }

        /// <summary>
        /// The CloudFormation resource name for the schedule event.
        /// </summary>
        public string ResourceName
        {
            get
            {
                if (IsResourceNameSet)
                {
                    return resourceName;
                }
                // Generate a default resource name from the schedule expression
                var sanitized = string.Join(string.Empty, (Schedule ?? string.Empty).Where(char.IsLetterOrDigit));
                return sanitized.Length > 0 ? sanitized : "ScheduleEvent";
            }
            set => resourceName = value;
        }

        private string resourceName { get; set; } = null;
        internal bool IsResourceNameSet => resourceName != null;

        /// <summary>
        /// A description for the schedule rule.
        /// </summary>
        public string Description { get; set; } = null;
        internal bool IsDescriptionSet => Description != null;

        /// <summary>
        /// A JSON string to pass as input to the Lambda function.
        /// </summary>
        public string Input { get; set; } = null;
        internal bool IsInputSet => Input != null;

        /// <summary>
        /// If set to false, the event source mapping will be disabled. Default value is true.
        /// </summary>
        public bool Enabled
        {
            get => enabled.GetValueOrDefault(true);
            set => enabled = value;
        }
        private bool? enabled { get; set; }
        internal bool IsEnabledSet => enabled.HasValue;

        /// <summary>
        /// Creates an instance of the <see cref="ScheduleEventAttribute"/> class.
        /// </summary>
        /// <param name="schedule"><see cref="Schedule"/> property</param>
        public ScheduleEventAttribute(string schedule)
        {
            Schedule = schedule;
        }

        internal List<string> Validate()
        {
            var validationErrors = new List<string>();

            if (string.IsNullOrEmpty(Schedule))
            {
                validationErrors.Add($"{nameof(ScheduleEventAttribute.Schedule)} must not be null or empty");
            }
            else if (!Schedule.StartsWith("rate(") && !Schedule.StartsWith("cron("))
            {
                validationErrors.Add($"{nameof(ScheduleEventAttribute.Schedule)} = {Schedule}. It must start with 'rate(' or 'cron('");
            }

            if (IsResourceNameSet && !_resourceNameRegex.IsMatch(ResourceName))
            {
                validationErrors.Add($"{nameof(ScheduleEventAttribute.ResourceName)} = {ResourceName}. It must only contain alphanumeric characters and must not be an empty string");
            }

            return validationErrors;
        }
    }
}
