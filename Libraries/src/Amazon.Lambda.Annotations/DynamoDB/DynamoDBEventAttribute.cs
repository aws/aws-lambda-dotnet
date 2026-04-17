// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Amazon.Lambda.Annotations.DynamoDB
{
    /// <summary>
    /// This attribute defines the DynamoDB event source configuration for a Lambda function.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DynamoDBEventAttribute : Attribute
    {
        private static readonly Regex _resourceNameRegex = new Regex("^[a-zA-Z0-9]+$");

        /// <summary>
        /// The DynamoDB stream that will act as the event trigger for the Lambda function.
        /// This can either be the stream ARN or reference to the DynamoDB table resource that is already defined in the serverless template.
        /// To reference a DynamoDB table resource in the serverless template, prefix the resource name with "@" symbol.
        /// </summary>
        public string Stream { get; set; }

        /// <summary>
        /// The CloudFormation resource name for the DynamoDB event source mapping.
        /// </summary>
        public string ResourceName
        {
            get
            {
                if (IsResourceNameSet)
                {
                    return resourceName;
                }

                if (string.IsNullOrWhiteSpace(Stream))
                {
                    return string.Empty;
                }

                if (Stream.StartsWith("@"))
                {
                    return Stream.Length > 1 ? Stream.Substring(1) : string.Empty;
                }

                // DynamoDB stream ARN format: arn:aws:dynamodb:region:account:table/TableName/stream/timestamp
                var arnParts = Stream.Split('/');
                if (arnParts.Length >= 2)
                {
                    var tableName = arnParts[1];
                    return string.Join(string.Empty, tableName.Where(char.IsLetterOrDigit));
                }
                return string.Join(string.Empty, Stream.Where(char.IsLetterOrDigit));
            }
            set => resourceName = value;
        }

        private string resourceName { get; set; } = null;
        internal bool IsResourceNameSet => resourceName != null;

        /// <summary>
        /// The maximum number of records in each batch that Lambda pulls from the stream.
        /// Default value is 100.
        /// </summary>
        public uint BatchSize
        {
            get => batchSize.GetValueOrDefault(100);
            set => batchSize = value;
        }
        private uint? batchSize { get; set; }
        internal bool IsBatchSizeSet => batchSize.HasValue;

        /// <summary>
        /// The position in the stream where Lambda starts reading. Valid values are TRIM_HORIZON and LATEST.
        /// Default value is LATEST.
        /// </summary>
        public string StartingPosition { get; set; } = "LATEST";
        internal bool IsStartingPositionSet => true;

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
        /// The maximum amount of time, in seconds, to gather records before invoking the function.
        /// </summary>
        public uint MaximumBatchingWindowInSeconds
        {
            get => maximumBatchingWindowInSeconds.GetValueOrDefault();
            set => maximumBatchingWindowInSeconds = value;
        }
        private uint? maximumBatchingWindowInSeconds { get; set; }
        internal bool IsMaximumBatchingWindowInSecondsSet => maximumBatchingWindowInSeconds.HasValue;

        /// <summary>
        /// A collection of semicolon (;) separated strings where each string denotes a filter pattern.
        /// </summary>
        public string Filters { get; set; } = null;
        internal bool IsFiltersSet => Filters != null;

        /// <summary>
        /// Creates an instance of the <see cref="DynamoDBEventAttribute"/> class.
        /// </summary>
        /// <param name="stream"><see cref="Stream"/> property</param>
        public DynamoDBEventAttribute(string stream)
        {
            Stream = stream;
        }

        internal List<string> Validate()
        {
            var validationErrors = new List<string>();

            if (IsBatchSizeSet && (BatchSize < 1 || BatchSize > 10000))
            {
                validationErrors.Add($"{nameof(DynamoDBEventAttribute.BatchSize)} = {BatchSize}. It must be between 1 and 10000");
            }
            if (IsMaximumBatchingWindowInSecondsSet && MaximumBatchingWindowInSeconds > 300)
            {
                validationErrors.Add($"{nameof(DynamoDBEventAttribute.MaximumBatchingWindowInSeconds)} = {MaximumBatchingWindowInSeconds}. It must be between 0 and 300");
            }
            if (string.IsNullOrEmpty(StartingPosition))
            {
                validationErrors.Add($"{nameof(DynamoDBEventAttribute.StartingPosition)} must not be null or empty. It must be either TRIM_HORIZON or LATEST");
            }
            else if (StartingPosition != "TRIM_HORIZON" && StartingPosition != "LATEST")
            {
                validationErrors.Add($"{nameof(DynamoDBEventAttribute.StartingPosition)} = {StartingPosition}. It must be either TRIM_HORIZON or LATEST");
            }
            if (string.IsNullOrWhiteSpace(Stream))
            {
                validationErrors.Add($"{nameof(DynamoDBEventAttribute.Stream)} must not be null or empty");
            }
            else if (Stream.StartsWith("@") && string.IsNullOrWhiteSpace(Stream.Substring(1)))
            {
                validationErrors.Add($"{nameof(DynamoDBEventAttribute.Stream)} = {Stream}. The '@' prefix must be followed by a non-empty resource or parameter name");
            }
            else if (!Stream.StartsWith("@"))
            {
                if (!Stream.Contains(":dynamodb:") || !Stream.Contains("/stream/"))
                {
                    validationErrors.Add($"{nameof(DynamoDBEventAttribute.Stream)} = {Stream}. The DynamoDB stream ARN is invalid");
                }
            }
            if (IsResourceNameSet && !_resourceNameRegex.IsMatch(ResourceName))
            {
                validationErrors.Add($"{nameof(DynamoDBEventAttribute.ResourceName)} = {ResourceName}. It must only contain alphanumeric characters and must not be an empty string");
            }

            return validationErrors;
        }
    }
}
