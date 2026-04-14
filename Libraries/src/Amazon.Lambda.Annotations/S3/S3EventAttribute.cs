// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Amazon.Lambda.Annotations.S3
{
    /// <summary>
    /// This attribute defines the S3 event source configuration for a Lambda function.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class S3EventAttribute : Attribute
    {
        private static readonly Regex _resourceNameRegex = new Regex("^[a-zA-Z0-9]+$");

        /// <summary>
        /// The S3 bucket that will act as the event trigger for the Lambda function.
        /// This must be a reference to an S3 bucket resource defined in the serverless template, prefixed with "@".
        /// </summary>
        public string Bucket { get; set; }

        /// <summary>
        /// The CloudFormation resource name for the S3 event. By default this is derived from the Bucket reference without the "@" prefix.
        /// </summary>
        public string ResourceName
        {
            get
            {
                if (IsResourceNameSet)
                    return resourceName;
                if (!string.IsNullOrEmpty(Bucket) && Bucket.StartsWith("@"))
                    return Bucket.Substring(1);
                return Bucket;
            }
            set => resourceName = value;
        }
        private string resourceName = null;
        internal bool IsResourceNameSet => resourceName != null;

        /// <summary>
        /// Semicolon-separated list of S3 event types. Default is 's3:ObjectCreated:*'.
        /// </summary>
        public string Events
        {
            get => events ?? "s3:ObjectCreated:*";
            set => events = value;
        }
        private string events = null;
        internal bool IsEventsSet => events != null;

        /// <summary>
        /// S3 key prefix filter for the event notification.
        /// </summary>
        public string FilterPrefix
        {
            get => filterPrefix;
            set => filterPrefix = value;
        }
        private string filterPrefix = null;
        internal bool IsFilterPrefixSet => filterPrefix != null;

        /// <summary>
        /// S3 key suffix filter for the event notification.
        /// </summary>
        public string FilterSuffix
        {
            get => filterSuffix;
            set => filterSuffix = value;
        }
        private string filterSuffix = null;
        internal bool IsFilterSuffixSet => filterSuffix != null;

        /// <summary>
        /// If set to false, the event source will be disabled. Default value is true.
        /// </summary>
        public bool Enabled
        {
            get => enabled.GetValueOrDefault(true);
            set => enabled = value;
        }
        private bool? enabled;
        internal bool IsEnabledSet => enabled.HasValue;

        /// <summary>
        /// Creates an instance of the <see cref="S3EventAttribute"/> class.
        /// </summary>
        /// <param name="bucket"><see cref="Bucket"/> property</param>
        public S3EventAttribute(string bucket)
        {
            Bucket = bucket;
        }

        internal List<string> Validate()
        {
            var validationErrors = new List<string>();

            if (string.IsNullOrEmpty(Bucket))
            {
                validationErrors.Add($"{nameof(S3EventAttribute.Bucket)} is required and must not be empty");
            }
            else if (!Bucket.StartsWith("@"))
            {
                validationErrors.Add($"{nameof(S3EventAttribute.Bucket)} = {Bucket}. S3 event sources require a reference to an S3 bucket resource in the serverless template. Prefix the resource name with '@'");
            }
            else
            {
                var bucketResourceName = Bucket.Substring(1);
                if (!_resourceNameRegex.IsMatch(bucketResourceName))
                {
                    validationErrors.Add($"{nameof(S3EventAttribute.Bucket)} = {Bucket}. The referenced S3 bucket resource name must not be empty and must only contain alphanumeric characters after the '@' prefix");
                }
            }

            if (IsResourceNameSet && !_resourceNameRegex.IsMatch(ResourceName))
            {
                validationErrors.Add($"{nameof(S3EventAttribute.ResourceName)} = {ResourceName}. It must only contain alphanumeric characters and must not be an empty string");
            }

            if (string.IsNullOrEmpty(Events))
            {
                validationErrors.Add($"{nameof(S3EventAttribute.Events)} must not be empty");
            }

            return validationErrors;
        }
    }
}
