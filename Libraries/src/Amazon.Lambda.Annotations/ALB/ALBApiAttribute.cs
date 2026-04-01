using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Amazon.Lambda.Annotations.ALB
{
    /// <summary>
    /// Configures the Lambda function to be called from an Application Load Balancer.
    /// The source generator will create the necessary CloudFormation resources
    /// (TargetGroup, ListenerRule, Lambda Permission) to wire the Lambda function
    /// as a target behind the specified ALB listener.
    /// </summary>
    /// <remarks>
    /// The listener ARN (or template reference), path pattern, and priority are required.
    /// See <a href="https://docs.aws.amazon.com/elasticloadbalancing/latest/application/lambda-functions.html">ALB Lambda documentation</a>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class ALBApiAttribute : Attribute
    {
        // Only allow alphanumeric characters for resource names
        private static readonly Regex _resourceNameRegex = new Regex("^[a-zA-Z0-9]+$");

        /// <summary>
        /// The ARN of the existing ALB listener, or a "@ResourceName" reference to a
        /// listener resource or parameter defined in the CloudFormation template.
        /// To reference a resource in the serverless template, prefix the resource name with "@" symbol.
        /// </summary>
        public string ListenerArn { get; set; }

        /// <summary>
        /// The path pattern condition for the ALB listener rule (e.g., "/api/orders/*").
        /// ALB supports wildcard path patterns using "*" and "?" characters.
        /// </summary>
        public string PathPattern { get; set; }

        /// <summary>
        /// The priority of the ALB listener rule. Must be between 1 and 50000.
        /// Lower numbers are evaluated first. Each rule on a listener must have a unique priority.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Whether multi-value headers are enabled on the ALB target group. Default: false.
        /// When true, the Lambda function should use <c>MultiValueHeaders</c> and
        /// <c>MultiValueQueryStringParameters</c> on the request and response objects.
        /// When false, use <c>Headers</c> and <c>QueryStringParameters</c> instead.
        /// </summary>
        public bool MultiValueHeaders
        {
            get => multiValueHeaders.GetValueOrDefault();
            set => multiValueHeaders = value;
        }
        private bool? multiValueHeaders { get; set; }
        internal bool IsMultiValueHeadersSet => multiValueHeaders.HasValue;

        /// <summary>
        /// Optional host header condition for the listener rule (e.g., "api.example.com").
        /// When specified, the rule will only match requests with this host header value.
        /// </summary>
        public string HostHeader { get; set; }

        /// <summary>
        /// Optional HTTP method condition for the listener rule (e.g., "GET", "POST").
        /// When specified, the rule will only match requests with this HTTP method.
        /// Leave null to match all HTTP methods.
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// The CloudFormation resource name prefix for the generated ALB resources
        /// (TargetGroup, ListenerRule, Permission). Defaults to "{LambdaResourceName}ALB".
        /// Must only contain alphanumeric characters.
        /// </summary>
        public string ResourceName
        {
            get => resourceName;
            set => resourceName = value;
        }
        private string resourceName { get; set; }
        internal bool IsResourceNameSet => resourceName != null;

        /// <summary>
        /// Creates an instance of the <see cref="ALBApiAttribute"/> class.
        /// </summary>
        /// <param name="listenerArn">The ARN of the ALB listener, or a "@ResourceName" reference to a template resource.</param>
        /// <param name="pathPattern">The path pattern condition (e.g., "/api/orders/*").</param>
        /// <param name="priority">The listener rule priority (1-50000).</param>
        public ALBApiAttribute(string listenerArn, string pathPattern, int priority)
        {
            ListenerArn = listenerArn;
            PathPattern = pathPattern;
            Priority = priority;
        }

        /// <summary>
        /// Validates the attribute properties and returns a list of validation error messages.
        /// </summary>
        internal List<string> Validate()
        {
            var validationErrors = new List<string>();

            if (string.IsNullOrEmpty(ListenerArn))
            {
                validationErrors.Add($"{nameof(ListenerArn)} is required and cannot be empty.");
            }
            else if (!ListenerArn.StartsWith("@"))
            {
                // If it's not a template reference, validate it looks like an ARN
                if (!ListenerArn.StartsWith("arn:"))
                {
                    validationErrors.Add($"{nameof(ListenerArn)} = {ListenerArn}. It must be a valid ARN (starting with 'arn:') or a template reference (starting with '@').");
                }
            }

            if (string.IsNullOrEmpty(PathPattern))
            {
                validationErrors.Add($"{nameof(PathPattern)} is required and cannot be empty.");
            }

            if (Priority < 1 || Priority > 50000)
            {
                validationErrors.Add($"{nameof(Priority)} = {Priority}. It must be between 1 and 50000.");
            }

            if (IsResourceNameSet && !_resourceNameRegex.IsMatch(ResourceName))
            {
                validationErrors.Add($"{nameof(ResourceName)} = {ResourceName}. It must only contain alphanumeric characters and must not be an empty string.");
            }

            if (!string.IsNullOrEmpty(HttpMethod))
            {
                var validMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
                };
                if (!validMethods.Contains(HttpMethod))
                {
                    validationErrors.Add($"{nameof(HttpMethod)} = {HttpMethod}. It must be a valid HTTP method (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS).");
                }
            }

            return validationErrors;
        }
    }
}
